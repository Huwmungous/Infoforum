using System.Threading.Tasks;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Browser;

namespace GenerateBTTranslations
{
    public class AuthService
    {
        public async Task<string> StartLoginAsync()
        {
            var options = new OidcClientOptions
            {
                Authority="https://longmanrd.net/auth/realms/LongmanRd",
                ClientId="53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7",
                Scope="openid profile email offline_access",
                RedirectUri = "GenerateBTTranslations://callback",
                Browser = new WindowsCustomBrowser() // Ensure WebAuthenticatorBrowser is defined and accessible
            };

            var oidcClient = new OidcClient(options);
            var result = await oidcClient.LoginAsync(new LoginRequest());
            if (result.IsError)
            {
                System.Diagnostics.Debug.WriteLine("Login error: " + result.Error);
                System.Diagnostics.Debug.WriteLine("Error description: " + result.ErrorDescription);
                return null;
            }
            return result.AccessToken;
        }
    }
}

