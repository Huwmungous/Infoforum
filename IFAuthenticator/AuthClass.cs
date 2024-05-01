using System.DirectoryServices.Protocols;
using System.Net;
using Microsoft.Extensions.Options;
using System.Timers;
using System.Collections.Concurrent;

namespace IFAuthenticator.Controllers
{
    public class AuthClass
    {
        private readonly ILogger<AuthClass> _logger;
        private readonly LdapSettings _ldapSettings;
        private static readonly ConcurrentDictionary<string, List<string>> _claims = new();
        private static readonly ConcurrentDictionary<string, UserPass> _authenticatedUsers = new();

        private static readonly string serviceuser = Environment.GetEnvironmentVariable("LDAP_SERVICE_USER") ?? "";
        private static readonly string servicePass = Environment.GetEnvironmentVariable("LDAP_SERVICE_PASSWORD") ?? "";

        private static LdapConnection? _claimsConnection;
        private static DateTime _lastClaimsRequest;

        public LdapConnection ClaimsConnection
        {
            get 
            {
                _lastClaimsRequest = DateTime.UtcNow;

                _claimsConnection = _claimsConnection ?? GetLdapConnection(serviceuser, servicePass);

                return _claimsConnection!;
            }
        }

        public AuthClass(ILogger<AuthClass> logger, IOptions<LdapSettings> ldapSettings)
        {
            _logger = logger;

            _ldapSettings = ldapSettings.Value;

            InitializeCleanupTimer();
        }

        private static System.Timers.Timer? _cleanupTimer;

        private void InitializeCleanupTimer()
        {
            if (_cleanupTimer is null)
            {
                _cleanupTimer = new System.Timers.Timer(59999); // Set the interval to about 60000 milliseconds (1 minute)
                _cleanupTimer.Elapsed += (object? sender, ElapsedEventArgs e) =>
                    {
                        // Close Claims ldap connection if it has been unused for 5 minutes, or so
                        if ((_lastClaimsRequest.AddMinutes(5) < DateTime.UtcNow) && _claimsConnection is not null)
                        {
                            _claimsConnection.Dispose();
                            _claimsConnection = null;
                            _logger.LogInformation("Inactive Claims Connection Closed.");
                        }

                        // Remove expired authenticated users and their claims
                        var expiredTokens = _authenticatedUsers.Where(kvp => kvp.Value.Expires <= DateTime.UtcNow)
                                                               .Select(kvp => kvp.Key)
                                                               .ToList(); // To avoid collection modification during enumeration

                        foreach (var token in expiredTokens)
                        {
                            _authenticatedUsers.TryRemove(token, out var removedUser);
                            _claims.TryRemove(token, out var removedClaims);
                            _logger.LogInformation($"Removed Expired User {removedUser!.User} and Claims.");
                        }
                    };
                _cleanupTimer.AutoReset = true;
                _cleanupTimer.Enabled = true;
            }
        }

        public async Task<(bool isAuthenticated, string token)> AuthenticateUserAsync(string username, string password)
        {
            var result = (false, "");

            await Task.Run(() =>
            {
                try
                {
                    using var conn = GetLdapConnection(username, password);
                    if (conn is not null)
                    {
                        string token = Guid.NewGuid().ToString().Replace("-", "");
                        _authenticatedUsers[token] = new() { User = username, Pass = password };
                        _claims[token] = [];
                        result = (true, token);
                    }
                }
                catch (LdapException ex)
                {
                    _logger.LogError($"Login Failed: {ex.Message}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"An error occurred: {ex.Message}");
                }
            }).ConfigureAwait(false);

            return result;
        }

        public async Task<bool> AuthoriseAsync(string token, string claim)
        {
                try
                {
                    return _authenticatedUsers.ContainsKey(token) && 
                          (CacheHasClaim(token, claim) || await LdapHasClaim(token, claim));
                }
                catch(Exception ex)
                {
                    _logger.LogError($"An error occurred: {ex.Message}");

                    return false;
                }
 
        }

        private async Task<bool> LdapHasClaim(string token, string claim)
        {
            var result = false;

            await Task.Run(() =>
            {
                try
                {
                    var userPass = _authenticatedUsers[token];

                    if (userPass is null)
                        throw new Exception($"Token not found {token}");

                    if (serviceuser is null || servicePass is null)
                        throw new ArgumentException("Failed to Identify the ldap Service Account or Password");
                    
                    SearchRequest searchRequest = new("DC=longmanrd,DC=infoforum,DC=co,DC=uk", $"(&(objectClass=group)(cn={claim}))", SearchScope.Subtree, "member");

                    var response = (SearchResponse)ClaimsConnection.SendRequest(searchRequest);

                    result = CheckMemberships(token, claim, result, userPass, response);
                }
                catch (LdapException ex)
                {
                    _logger.LogError($"LDAP error occurred: {ex.Message}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"An error occurred: {ex.Message}");
                }
            });

            // If no entries are found or the user is not a member of the group, return false.
            return result;
        }

        private static bool CheckMemberships(string token, string claim, bool result, UserPass? userPass, SearchResponse response)
        {
            // Check each group found (though typically should be just one) for user membership
            foreach (SearchResultEntry entry in response.Entries)
            {
                // Check if the user is listed as a member of the group
                foreach (string member in entry.Attributes["member"].GetValues(typeof(string)))
                {
                    if (member.Contains(userPass!.User, StringComparison.OrdinalIgnoreCase))
                    {
                        // Optionally, cache the claim
                        if (!_claims[token].Contains(claim))
                        {
                            _claims[token].Add(claim);
                        }
                        result = true; // User is a member of the group

                        break;
                    }
                }
            }

            return result;
        }

        private bool CacheHasClaim(string token, string claim)
        {
            return _claims[token].Contains(claim);
        }

        private LdapConnection? GetLdapConnection(string username, string password)
        {
            _logger.LogInformation($"{_ldapSettings.Server}:{_ldapSettings.Port} user {username}@{_ldapSettings.Domain} attempting to connect");
            try
            {
                var connection = new LdapConnection(
                    new LdapDirectoryIdentifier(_ldapSettings.Server, _ldapSettings.Port),
                    new NetworkCredential($"{username}@{_ldapSettings.Domain}", password),
                    AuthType.Kerberos
                );

                connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
                connection.SessionOptions.SecureSocketLayer = _ldapSettings.UseSSL;
                connection.Bind();
                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Connection Failed for user {username}@{_ldapSettings.Domain} : {ex}");

                return null;
            }
        }

        // Implement cleanup routines for expired tokens and claims
    }

}
