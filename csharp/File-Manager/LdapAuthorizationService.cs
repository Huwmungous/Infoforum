// LdapAuthorizationService.cs


using System.DirectoryServices.Protocols;
using System.Net;

namespace FileManager.Api.Services
{
    public class LdapAuthorizationService : ILdapAuthorizationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LdapAuthorizationService> _logger;
        private readonly string _ldapServer;
        private readonly int _ldapPort;
        private readonly string _bindDn;
        private readonly string _bindPassword;
        private readonly string _baseDn;

        public LdapAuthorizationService(
            IConfiguration configuration,
            ILogger<LdapAuthorizationService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            _ldapServer = _configuration["Ldap:Server"];
            _ldapPort = int.Parse(_configuration["Ldap:Port"]);
            _bindDn = _configuration["Ldap:BindDn"];
            _bindPassword = _configuration["Ldap:BindPassword"];
            _baseDn = _configuration["Ldap:BaseDn"];
        }

        public async Task<bool> HasAccessAsync(string username, string path, AccessLevel level = AccessLevel.Read)
        {
            try
            {
                if (string.IsNullOrEmpty(username))
                {
                    return false;
                }

                // Normalize path for comparison
                path = NormalizePath(path);

                // Connect to LDAP
                using (var connection = new LdapConnection(new LdapDirectoryIdentifier(_ldapServer, _ldapPort)))
                {
                    connection.Bind(new NetworkCredential(_bindDn, _bindPassword));

                    // Find the user
                    var userFilter = $"(&(objectClass=person)(uid={username}))";
                    var userSearchRequest = new SearchRequest(
                        _baseDn,
                        userFilter,
                        SearchScope.Subtree
                    );
                    var userSearchResponse = (SearchResponse)connection.SendRequest(userSearchRequest);

                    if (userSearchResponse.Entries.Count == 0)
                    {
                        _logger.LogWarning($"User not found in LDAP: {username}");
                        return false;
                    }

                    var userEntry = userSearchResponse.Entries[0];

                    // Get user's groups
                    var groupFilter = $"(&(objectClass=groupOfNames)(member={userEntry.DistinguishedName}))";
                    var groupSearchRequest = new SearchRequest(
                        _baseDn,
                        groupFilter,
                        SearchScope.Subtree
                    );
                    var groupSearchResponse = (SearchResponse)connection.SendRequest(groupSearchRequest);

                    var userGroups = new List<string>();
                    foreach (SearchResultEntry entry in groupSearchResponse.Entries)
                    {
                        userGroups.Add(entry.Attributes["cn"][0].ToString());
                    }

                    // Check if user has required permissions based on path and groups
                    return CheckPermissions(userGroups, path, level);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking LDAP authorization for user: {username}, path: {path}");
                // Fall back to a secure default
                return false;
            }
        }

        private bool CheckPermissions(List<string> userGroups, string path, AccessLevel level)
        {
            // This is a simplified example - in real implementation, you might want to use a more 
            // sophisticated approach like path hierarchies, ACLs, etc.

            // Example rules (should be from config or database in real implementation)
            var rules = new Dictionary<string, Dictionary<string, HashSet<AccessLevel>>>
                    {
                        // Admins have full access to everything
                        { "FileAdmins", new Dictionary<string, HashSet<AccessLevel>> {
                            { "/", new HashSet<AccessLevel> { AccessLevel.Read, AccessLevel.Write, AccessLevel.Delete } }
                        }},
                        
                        // HR can access HR documents
                        { "HRGroup", new Dictionary<string, HashSet<AccessLevel>> {
                            { "/Documents/HR", new HashSet<AccessLevel> { AccessLevel.Read, AccessLevel.Write, AccessLevel.Delete } }
                        }},
                        
                        // Finance can access finance documents
                        { "FinanceGroup", new Dictionary<string, HashSet<AccessLevel>> {
                            { "/Documents/Finance", new HashSet<AccessLevel> { AccessLevel.Read, AccessLevel.Write, AccessLevel.Delete } }
                        }},
                        
                        // All authenticated users can read public documents
                        { "AllUsers", new Dictionary<string, HashSet<AccessLevel>> {
                            { "/Documents/Public", new HashSet<AccessLevel> { AccessLevel.Read } }
                        }}
                    };

            // Check if user belongs to any group that grants access
            foreach (var group in userGroups)
            {
                if (rules.TryGetValue(group, out var pathRules))
                {
                    // Check each path rule
                    foreach (var pathRule in pathRules)
                    {
                        // If path is under the rule path and the rule allows the required access level
                        if (path.StartsWith(pathRule.Key) && pathRule.Value.Contains(level))
                        {
                            return true;
                        }
                    }
                }
            }

            // Also check if user is in AllUsers group which applies to everyone
            if (rules.TryGetValue("AllUsers", out var allUserRules))
            {
                foreach (var pathRule in allUserRules)
                {
                    if (path.StartsWith(pathRule.Key) && pathRule.Value.Contains(level))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private string NormalizePath(string path)
        {
            // Ensure path starts with a slash
            if (!path.StartsWith("/"))
            {
                path = "/" + path;
            }

            // Remove trailing slash except for root
            if (path.Length > 1 && path.EndsWith("/"))
            {
                path = path.TrimEnd('/');
            }

            return path;
        }
    }
}