using System.DirectoryServices.Protocols;
using System.Net;
using Microsoft.Extensions.Options;

namespace IFAuthenticator.Controllers
{
    public class AuthClass
    {
        private readonly ILogger<AuthClass> _logger;
        private readonly LdapSettings _ldapSettings;
        private static readonly Dictionary<string, List<string>> _claims = new();
        private static readonly Dictionary<string, UserPass> _authenticatedUsers = new();

        public AuthClass(ILogger<AuthClass> logger, IOptions<LdapSettings> ldapSettings)
        {
            _logger = logger;

            _ldapSettings = ldapSettings.Value;
        }

        public async Task<(bool isAuthenticated, string token)> AuthenticateUserAsync(string username, string password)
        {
            string token = string.Empty;
            try
            {
                await Task.Run(() => GetLdapConnection(username, password)).ConfigureAwait(false);

                // If we reach here, authentication was successful
                token = Guid.NewGuid().ToString().Replace("-", "");
                _authenticatedUsers[token] = new() { User = username, Pass = password };
                _claims[token] = [];

                return (true, token);
            }
            catch(LdapException ex)
            {
                _logger.LogError($"Login Failed: {ex.Message}");
                return (false, token);
            }
            catch(Exception ex)
            {
                _logger.LogError($"An error occurred: {ex.Message}");

                return (false, token);
            }
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

                    if(userPass is null)
                        throw new Exception($"Token not found {token}");

                    var serviceuser = Environment.GetEnvironmentVariable("LDAP_SERVICE_USER");
                    var servicePass = Environment.GetEnvironmentVariable("LDAP_SERVICE_PASSWORD");

                    if(serviceuser is null || servicePass is null)
                        throw new ArgumentException("Failed to Identify the ldap Service Account or Password");

                    using var connection = GetLdapConnection(serviceuser, servicePass);

                    SearchRequest searchRequest = new("DC=longmanrd,DC=infoforum,DC=co,DC=uk", $"(&(objectClass=group)(cn={claim}))", SearchScope.Subtree, "member");

                    // Perform the search
                    var response = (SearchResponse)connection.SendRequest(searchRequest);

                    // Check each group found (though typically should be just one) for user membership
                    foreach(SearchResultEntry entry in response.Entries)
                    {
                        // Check if the user is listed as a member of the group
                        foreach(string member in entry.Attributes["member"].GetValues(typeof(string)))
                        {
                            if(member.Contains(userPass.User, StringComparison.OrdinalIgnoreCase))
                            {
                                // Optionally, cache the claim
                                if(!_claims[token].Contains(claim))
                                {
                                    _claims[token].Add(claim);
                                }
                                result = true; // User is a member of the group

                                break;
                            }
                        }
                    }
                }
                catch(LdapException ex)
                {
                    _logger.LogError($"LDAP error occurred: {ex.Message}");
                }
                catch(Exception ex)
                {
                    _logger.LogError($"An error occurred: {ex.Message}");
                }
            });

            // If no entries are found or the user is not a member of the group, return false.
            return result;
        }

        private bool CacheHasClaim(string token, string claim)
        {
            return _claims[token].Contains(claim);
        }

        private LdapConnection GetLdapConnection(string username, string password)
        {
            try
            {
                LdapDirectoryIdentifier identifier = new LdapDirectoryIdentifier(_ldapSettings.Server, _ldapSettings.Port);
                LdapConnection connection = new LdapConnection(identifier);
                connection.SessionOptions.SecureSocketLayer = _ldapSettings.UseSSL;
                connection.SessionOptions.ProtocolVersion = 3;
                NetworkCredential credential = new NetworkCredential($"{username}@{_ldapSettings.Domain}", password);
                connection.Credential = credential;
                connection.Bind();
                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogError($"user '{username}' attempting to connect : {ex.Message}");

                if(ex.InnerException is not null)
                    _logger.LogError(ex.InnerException.Message);

                throw;
            }
        }

        // Implement cleanup routines for expired tokens and claims
    }

}
