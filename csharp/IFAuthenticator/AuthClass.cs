﻿using Microsoft.Extensions.Options;
using Novell.Directory.Ldap;
using System.Collections.Concurrent;

namespace IFAuthenticator.Controllers
{
    public class AuthClass
    {
        private readonly ILogger<AuthClass> _logger;
        private readonly LdapSettings _ldapSettings;
        private static readonly ConcurrentDictionary<string, List<string>> _claims = new();
        private static readonly ConcurrentDictionary<string, UserPass> _authenticatedUsers = new();

        private static readonly string serviceUser = Environment.GetEnvironmentVariable("LDAP_SERVICE_USER") ?? "";
        private static readonly string servicePass = Environment.GetEnvironmentVariable("LDAP_SERVICE_PASSWORD") ?? "";

        private static LdapConnection? _claimsConnection;
        private static DateTime _lastClaimsRequest;

        public LdapConnection? ClaimsConnection
        {
            get
            {
                _lastClaimsRequest = DateTime.UtcNow;

                if (_claimsConnection == null || !_claimsConnection.Connected)
                {
                    _claimsConnection = GetLdapConnection(serviceUser, servicePass);
                }

                return _claimsConnection;
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
            if (_cleanupTimer == null)
            {
                _cleanupTimer = new System.Timers.Timer(60000);
                _cleanupTimer.Elapsed += (sender, e) =>
                {
                    if ((_lastClaimsRequest.AddMinutes(5) < DateTime.UtcNow) && _claimsConnection != null)
                    {
                        _claimsConnection.Dispose();
                        _claimsConnection = null;
                        _logger.LogInformation("Inactive Claims Connection Closed.");
                    }

                    var expiredTokens = _authenticatedUsers.Where(kvp => kvp.Value.Expires <= DateTime.UtcNow).ToList();
                    foreach (var token in expiredTokens)
                    {
                        _authenticatedUsers.TryRemove(token.Key, out var removedUser);
                        _claims.TryRemove(token.Key, out var removedClaims);
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
                    if (conn is not null && conn.Connected)
                    {
                        string token = Guid.NewGuid().ToString().Replace("-", "");
                        _authenticatedUsers[token] = new UserPass { User = username, Pass = password };
                        _claims[token] = new List<string>();
                        result = (true, token);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Authentication error: {ex.Message}");
                }
            });
            return result;
        }

        public async Task<bool> AuthoriseAsync(string token, string claim)
        {
            return await Task.Run(() =>
            {
                try
                {
                    return _authenticatedUsers.ContainsKey(token) && (CacheHasClaim(token, claim) || LdapHasClaim(token, claim).Result);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Authorization error: {ex.Message}");
                    return false;
                }
            });
        }

        private string _searchBase = string.Empty;
        public string SearchBase
        {
            get
            {
                if (string.IsNullOrEmpty(_searchBase))
                    _searchBase = _ldapSettings.SearchBase;

                return _searchBase;
            }
        }

        private async Task<bool> LdapHasClaim(string token, string claim)
        {
            bool result = false;

            await Task.Run(() =>
            {
                try
                {
                    var userPass = _authenticatedUsers[token];

                    if (userPass is null)
                        throw new Exception($"Token not found {token}");

                    if (string.IsNullOrEmpty(serviceUser) || string.IsNullOrEmpty(servicePass))
                        throw new ArgumentException("Service account credentials are not set.");

                    var searchFilter = $"(&(objectClass=group)(cn={claim}))";

                    var search = ClaimsConnection!.Search(SearchBase, LdapConnection.ScopeSub, searchFilter, ["member"], false);

                    while (search.HasMore())
                    {
                        var members = search.Next().GetAttributeSet()["member"];

                        if (members is not null)
                        {
                            foreach (var member in members.StringValueArray)
                            {
                                if (member.Contains(userPass.User, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!_claims[token].Contains(claim))
                                        _claims[token].Add(claim);

                                    result = true;

                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"LDAP query error: {ex.Message}");
                }
            });

            return result;
        }

        private bool CacheHasClaim(string token, string claim)
        {
            return _claims[token].Contains(claim);
        }

        private LdapConnection? GetLdapConnection(string username, string password)
        {
            try
            {
                var conn = new LdapConnection() { SecureSocketLayer = _ldapSettings.UseSSL };

                if (_ldapSettings.UseSSL)
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    conn.UserDefinedServerCertValidationDelegate += (sender, certificate, chain, sslPolicyErrors) => { return true; };
#pragma warning restore CS0618 // Type or member is obsolete

                    conn.Connect(_ldapSettings.Server, _ldapSettings.Port);
                    conn.Bind(LdapConnection.LdapV3, $"{username}@{_ldapSettings.Domain}", password);
                }
                else
                {
                    conn.Connect(_ldapSettings.Server, _ldapSettings.Port);
                    conn.Bind($"{username}@{_ldapSettings.Domain}", password);
                }

                return conn;
            }
            catch (Exception ex)
            {
                _logger.LogError($"LDAP connection error: {ex.Message}");
                return null;
            }
        }

    }
}