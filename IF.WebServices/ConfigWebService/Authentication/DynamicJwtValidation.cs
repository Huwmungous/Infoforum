using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;

namespace ConfigWebService.Authentication;

/// <summary>
/// Provides dynamic JWT validation for multi-realm Keycloak authentication.
/// Tokens from any realm under the configured Keycloak authority are accepted,
/// with signing keys fetched dynamically from each realm's JWKS endpoint.
/// </summary>
public class DynamicJwtValidation
{
    private readonly string _keycloakBaseUrl;
    private readonly ILogger<DynamicJwtValidation> _logger;
    
    // Cache configuration managers per realm to avoid repeated discovery
    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _configManagers = new();
    
    // Cache expiry for OIDC configuration (keys are refreshed automatically by ConfigurationManager)
    private static readonly TimeSpan ConfigurationCacheExpiry = TimeSpan.FromHours(24);

    public DynamicJwtValidation(string keycloakBaseUrl, ILogger<DynamicJwtValidation> logger)
    {
        // Normalise: ensure no trailing slash
        _keycloakBaseUrl = keycloakBaseUrl.TrimEnd('/');
        _logger = logger;
        
        _logger.LogInformation("DynamicJwtValidation initialised with Keycloak base URL: {BaseUrl}", _keycloakBaseUrl);
    }

    /// <summary>
    /// Expected issuer format for Keycloak realms.
    /// e.g., "https://longmanrd.net/auth/realms/MyRealm"
    /// </summary>
    private string GetExpectedIssuerPrefix() => $"{_keycloakBaseUrl}/auth/realms/";

    /// <summary>
    /// Validates that the issuer is from our trusted Keycloak instance.
    /// </summary>
    public string ValidateIssuer(
        string issuer,
        SecurityToken securityToken,
        TokenValidationParameters validationParameters)
    {
        var expectedPrefix = GetExpectedIssuerPrefix();
        
        if (string.IsNullOrEmpty(issuer))
        {
            _logger.LogWarning("Token has no issuer claim");
            throw new SecurityTokenInvalidIssuerException("Token has no issuer claim");
        }

        if (!issuer.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Invalid issuer: {Issuer}. Expected issuer to start with: {ExpectedPrefix}",
                issuer, expectedPrefix);
            throw new SecurityTokenInvalidIssuerException(
                $"Invalid issuer: {issuer}. Token must be from a realm under {_keycloakBaseUrl}");
        }

        // Extract realm name for logging
        var realmName = issuer.Substring(expectedPrefix.Length);
        _logger.LogDebug("Validated issuer for realm: {Realm}", realmName);

        return issuer;
    }

    /// <summary>
    /// Resolves signing keys dynamically based on the token's issuer.
    /// Fetches from the appropriate realm's JWKS endpoint.
    /// </summary>
    public IEnumerable<SecurityKey> ResolveSigningKeys(
        string token,
        SecurityToken securityToken,
        string kid,
        TokenValidationParameters validationParameters)
    {
        // Extract issuer from the token to determine which realm's keys to fetch
        var issuer = GetIssuerFromToken(token);
        
        if (string.IsNullOrEmpty(issuer))
        {
            _logger.LogWarning("Cannot resolve signing keys: token has no issuer");
            return Enumerable.Empty<SecurityKey>();
        }

        var expectedPrefix = GetExpectedIssuerPrefix();
        if (!issuer.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Cannot resolve signing keys: issuer {Issuer} is not from trusted Keycloak", issuer);
            return Enumerable.Empty<SecurityKey>();
        }

        try
        {
            // Get or create configuration manager for this issuer (realm)
            var configManager = _configManagers.GetOrAdd(issuer, iss =>
            {
                var metadataAddress = $"{iss}/.well-known/openid-configuration";
                _logger.LogInformation("Creating OIDC configuration manager for: {MetadataAddress}", metadataAddress);
                
                return new ConfigurationManager<OpenIdConnectConfiguration>(
                    metadataAddress,
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever { RequireHttps = false }); // Set to true in production if using HTTPS
            });

            // Fetch the configuration (cached, with automatic refresh)
            var config = configManager.GetConfigurationAsync(CancellationToken.None).GetAwaiter().GetResult();
            
            _logger.LogDebug(
                "Retrieved {KeyCount} signing keys for issuer: {Issuer}",
                config.SigningKeys.Count, issuer);

            return config.SigningKeys;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve signing keys for issuer: {Issuer}", issuer);
            return Enumerable.Empty<SecurityKey>();
        }
    }

    /// <summary>
    /// Extracts the issuer claim from a JWT without validating it.
    /// Used to determine which realm's JWKS endpoint to query.
    /// </summary>
    private static string? GetIssuerFromToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            
            // Read token without validation to extract claims
            if (handler.CanReadToken(token))
            {
                var jwtToken = handler.ReadJwtToken(token);
                return jwtToken.Issuer;
            }
        }
        catch
        {
            // Ignore parse errors - validation will fail anyway
        }

        return null;
    }

    /// <summary>
    /// Creates TokenValidationParameters configured for dynamic multi-realm validation.
    /// </summary>
    public TokenValidationParameters CreateTokenValidationParameters()
    {
        return new TokenValidationParameters
        {
            // Use our custom issuer validator
            ValidateIssuer = true,
            IssuerValidator = ValidateIssuer,

            // Use our custom signing key resolver
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = ResolveSigningKeys,

            // Standard validation settings
            ValidateAudience = false, // Keycloak doesn't always set audience consistently
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),

            // Don't require specific signing key upfront - we resolve dynamically
            RequireSignedTokens = true,
        };
    }
}
