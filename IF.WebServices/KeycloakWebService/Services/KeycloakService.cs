using KeycloakWebService.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace KeycloakWebService.Services
{

    /// <summary>
    /// Service for communicating with Keycloak Admin API
    /// </summary>
    public class KeycloakService(HttpClient httpClient, IConfiguration configuration)
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly IConfiguration _configuration = configuration;
        private string? _accessToken;
        private DateTime _tokenExpiresAt = DateTime.MinValue;


        private readonly Dictionary<string, (string Token, DateTime ExpiresAt)> _realmTokens = new();

        private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };

        private async Task EnsureValidToken()
        {
            if (string.IsNullOrEmpty(_accessToken) || DateTime.UtcNow >= _tokenExpiresAt)
            {
                await RefreshAccessToken();
            }
        }

        private async Task RefreshAccessToken()
        {
            var keycloakUrl = _configuration["Keycloak:Url"];
            var realm = _configuration["Keycloak:Realm"];
            var clientId = _configuration["Keycloak:ClientId"];
            var clientSecret = _configuration["Keycloak:ClientSecret"];

            var tokenUrl = $"{keycloakUrl}/realms/{realm}/protocol/openid-connect/token";

            using var content = new FormUrlEncodedContent([
                new("grant_type", "client_credentials"),
            new("client_id", clientId ?? ""),
            new("client_secret", clientSecret ?? "")
            ]);

            var response = await _httpClient.PostAsync(tokenUrl, content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            _accessToken = root.GetProperty("access_token").GetString();
            var expiresIn = root.GetProperty("expires_in").GetInt32();
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 10);
        }

        private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            return request;
        }

        private string? _masterAccessToken;
        private DateTime _masterTokenExpiresAt = DateTime.MinValue;

        private async Task EnsureValidMasterToken()
        {
            if (string.IsNullOrEmpty(_masterAccessToken) ||
                DateTime.UtcNow >= _masterTokenExpiresAt)
            {
                await RefreshMasterAccessToken();
            }
        }

        private async Task RefreshMasterAccessToken()
        {
            var keycloakUrl = _configuration["Keycloak:Url"];
            var username = _configuration["Keycloak:AdminUsername"];
            var password = _configuration["Keycloak:AdminPassword"];

            var tokenUrl =
                $"{keycloakUrl}/realms/master/protocol/openid-connect/token";

            using var content = new FormUrlEncodedContent(new[]
            {
        new KeyValuePair<string,string>("grant_type", "password"),
        new KeyValuePair<string,string>("client_id", "admin-cli"),
        new KeyValuePair<string,string>("username", username!),
        new KeyValuePair<string,string>("password", password!)
    });

            var response = await _httpClient.PostAsync(tokenUrl, content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Master token failed: {body}");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            _masterAccessToken = root.GetProperty("access_token").GetString();
            var expiresIn = root.GetProperty("expires_in").GetInt32();
            _masterTokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 10);
        }

        private async Task<string> GetRealmToken(string realmName)
        {
            if (_realmTokens.TryGetValue(realmName, out var tokenInfo) &&
                DateTime.UtcNow < tokenInfo.ExpiresAt)
                return tokenInfo.Token;

            var keycloakUrl = _configuration["Keycloak:Url"];
            var clientId = _configuration["Keycloak:ClientId"];
            var clientSecret = _configuration["Keycloak:ClientSecret"];
            var tokenUrl = $"{keycloakUrl}/realms/{realmName}/protocol/openid-connect/token";

            using var content = new FormUrlEncodedContent(new[]
            {
        new KeyValuePair<string,string>("grant_type","client_credentials"),
        new KeyValuePair<string,string>("client_id", clientId ?? ""),
        new KeyValuePair<string,string>("client_secret", clientSecret ?? "")
    });

            var response = await _httpClient.PostAsync(tokenUrl, content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var token = doc.RootElement.GetProperty("access_token").GetString()!;
            var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();

            _realmTokens[realmName] = (token, DateTime.UtcNow.AddSeconds(expiresIn - 10));
            return token;
        }



        private HttpRequestMessage CreateMasterAuthorizedRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _masterAccessToken);
            return request;
        }

        public async Task CreateRealmAsync(KeycloakRealm realm)
        {
            await EnsureValidMasterToken();

            var keycloakUrl = _configuration["Keycloak:Url"];
            var url = $"{keycloakUrl}/admin/realms";

            var payload = new
            {
                realm = realm.Realm,
                enabled = realm.Enabled
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _masterAccessToken);

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            using var response = await _httpClient.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new InvalidOperationException($"Realm '{realm.Realm}' already exists");

            response.EnsureSuccessStatusCode();
        }




        public async Task<List<KeycloakUser>> GetUsersAsync(string? username)
        {
            await EnsureValidToken();

            var keycloakUrl = _configuration["Keycloak:Url"];
            var realm = _configuration["Keycloak:Realm"];
            var url = $"{keycloakUrl}/admin/realms/{realm}/users";

            if (username != null)
            {

                url = url + "?search=" + username;

            }

            using var request = CreateAuthorizedRequest(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<List<KeycloakUser>>(json, JsonOptions) ?? [];
        }

        public async Task<KeycloakUser?> GetUserByIdAsync(string userId)
        {
            await EnsureValidToken();

            var keycloakUrl = _configuration["Keycloak:Url"];
            var realm = _configuration["Keycloak:Realm"];
            var url = $"{keycloakUrl}/admin/realms/{realm}/users/{userId}";

            using var request = CreateAuthorizedRequest(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<KeycloakUser>(json, JsonOptions);
        }

        public async Task<List<KeycloakGroup>> GetGroupsAsync()
        {
            await EnsureValidToken();

            var keycloakUrl = _configuration["Keycloak:Url"];
            var realm = _configuration["Keycloak:Realm"];
            var url = $"{keycloakUrl}/admin/realms/{realm}/groups";

            using var request = CreateAuthorizedRequest(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<List<KeycloakGroup>>(json, JsonOptions) ?? [];
        }

        public async Task<List<KeycloakGroup>> GetChildGroupsAsync(string groupId)
        {
            await EnsureValidToken();

            var keycloakUrl = _configuration["Keycloak:Url"];
            var realm = _configuration["Keycloak:Realm"];
            var url = $"{keycloakUrl}/admin/realms/{realm}/groups/{groupId}/children";

            using var request = CreateAuthorizedRequest(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            List<KeycloakGroup>? groupList = null;

            try
            {
                // parse, standard list
                groupList = JsonSerializer.Deserialize<List<KeycloakGroup>>(json, JsonOptions);
            }
            catch (JsonException)
            {
                // fail, check if single obj
                var single = JsonSerializer.Deserialize<KeycloakGroup>(json, JsonOptions);
                if (single != null)
                    groupList = [single];
            }

            // return list
            return groupList ?? [];
        }

        public async Task<List<KeycloakClient>> GetRealmClientsAsync(string realmName)
        {
            var keycloakUrl = _configuration["Keycloak:Url"];
            var url = $"{keycloakUrl}/admin/realms/{realmName}/clients";

            HttpResponseMessage response;

            try
            {
                // Try realm token first
                var token = await GetRealmToken(realmName);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                response = await _httpClient.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new Exception("Realm token unauthorized");
                }
            }
            catch
            {
                // Fallback: use master token
                await EnsureValidMasterToken();
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _masterAccessToken);
                response = await _httpClient.SendAsync(request);
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            List<KeycloakClient>? clientList = null;
            try
            {
                clientList = JsonSerializer.Deserialize<List<KeycloakClient>>(json, JsonOptions);
            }
            catch (JsonException)
            {
                var single = JsonSerializer.Deserialize<KeycloakClient>(json, JsonOptions);
                if (single != null)
                    clientList = new List<KeycloakClient> { single };
            }

            return clientList ?? new List<KeycloakClient>();
        }



        public async Task<List<KeycloakUser>> GetGroupMembersAsync(string groupId, string? username)
        {
            await EnsureValidToken();

            var keycloakUrl = _configuration["Keycloak:Url"];
            var realm = _configuration["Keycloak:Realm"];
            var url = $"{keycloakUrl}/admin/realms/{realm}/groups/{groupId}/members";

            if (username != null)
            {

                url = url + "?search=" + username;

            }

            using var request = CreateAuthorizedRequest(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<List<KeycloakUser>>(json, JsonOptions) ?? [];
        }

        public async Task<List<KeycloakGroup>> GetUserGroupsAsync(string userId)
        {
            await EnsureValidToken();

            var keycloakUrl = _configuration["Keycloak:Url"];
            var realm = _configuration["Keycloak:Realm"];
            var url = $"{keycloakUrl}/admin/realms/{realm}/users/{userId}/groups";

            using var request = CreateAuthorizedRequest(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<List<KeycloakGroup>>(json, JsonOptions) ?? [];
        }

        public async Task AddUserToGroupAsync(string userId, string groupId)
        {
            await EnsureValidToken();

            var keycloakUrl = _configuration["Keycloak:Url"];
            var realm = _configuration["Keycloak:Realm"];
            var url = $"{keycloakUrl}/admin/realms/{realm}/users/{userId}/groups/{groupId}";

            using var request = CreateAuthorizedRequest(HttpMethod.Put, url);
            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        public async Task RemoveUserFromGroupAsync(string userId, string groupId)
        {
            await EnsureValidToken();

            var keycloakUrl = _configuration["Keycloak:Url"];
            var realm = _configuration["Keycloak:Realm"];
            var url = $"{keycloakUrl}/admin/realms/{realm}/users/{userId}/groups/{groupId}";

            using var request = CreateAuthorizedRequest(HttpMethod.Delete, url);
            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<KeycloakRealm>> GetAllRealmsAsync()
        {
            // Use master token to see all realms
            await EnsureValidMasterToken();

            var keycloakUrl = _configuration["Keycloak:Url"];
            var url = $"{keycloakUrl}/admin/realms";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _masterAccessToken);

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<List<KeycloakRealm>>(json, JsonOptions) ?? new List<KeycloakRealm>();
        }



        public async Task CreateGroupAsync(string realm, string groupName)
        {
            await EnsureValidToken();

            var keycloakUrl = _configuration["Keycloak:Url"];
            var url = $"{keycloakUrl}/admin/realms/{realm}/groups";

            var payload = new
            {
                name = groupName
            };

            using var request = CreateAuthorizedRequest(HttpMethod.Post, url);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            using var response = await _httpClient.SendAsync(request);

            // 409 = already exists → usually OK in this scenario
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                return;

            response.EnsureSuccessStatusCode();
        }

        public async Task CreateGroupInAllRealmsAsync(string groupName)
        {
            var realms = await GetAllRealmsAsync();

            foreach (var realm in realms)
            {
                // Skip master if you don’t want groups there
                if (realm.Realm.Equals("master", StringComparison.OrdinalIgnoreCase))
                    continue;

                await CreateGroupAsync(realm.Realm, groupName);
            }
        }



        public async Task ProvisionRealmAsync(string realmName)
        {
            await CreateRealmAsync(new KeycloakRealm
            {
                Realm = realmName,
                Enabled = true
            });

            await CreateClientAsync(realmName, new CreateKeycloakClient
            {
                ClientId = realmName+"-svc",
                Name = "svc client",
                Protocol = "openid-connect",
                ServiceAccountsEnabled = true,
                PublicClient = false
            });

            await CreateClientAsync(realmName, new CreateKeycloakClient
            {
                ClientId = realmName + "-urs",
                Name = "usr client",
                Protocol = "openid-connect",
                ServiceAccountsEnabled = true,
                PublicClient = false
            });

            await CreateClientAsync(realmName, new CreateKeycloakClient
            {
                ClientId = realmName + "-pps",
                Name = "pps client",
                Protocol = "openid-connect",
                ServiceAccountsEnabled = true,
                PublicClient = false
            });

            //await CreateGroupAsync(realmName, "users");
            //await CreateGroupAsync(realmName, "admins");
        }



        public async Task CreateClientAsync(string realmName, CreateKeycloakClient client)
        {
            await EnsureValidMasterToken();

            var keycloakUrl = _configuration["Keycloak:Url"];

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{keycloakUrl}/admin/realms/{realmName}/clients"
            )
            {
                Content = JsonContent.Create(client)
            };

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _masterAccessToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new Exception(
                    $"Failed to create client in realm '{realmName}': {response.StatusCode} - {body}"
                );
            }
        }




    }





}

