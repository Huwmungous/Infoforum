using Duende.IdentityModel.OidcClient.Browser;
using IBrowser = Duende.IdentityModel.OidcClient.Browser.IBrowser;

namespace GenerateBTTranslations
{
    public class WebAuthenticatorBrowser : IBrowser
    {
        public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
        {
            try
            {
                var authResult = await WebAuthenticator.AuthenticateAsync(
                    new Uri(options.StartUrl),
                    new Uri(options.EndUrl)
                );

                var responseParameters = authResult.Properties.Select(kvp => $"{kvp.Key}={kvp.Value}");
                var responseString = string.Join("&", responseParameters);

                return new BrowserResult
                {
                    Response = responseString,
                    ResultType = BrowserResultType.Success
                };
            }
            catch (Exception ex)
            {
                return new BrowserResult
                {
                    Error = ex.Message,
                    ResultType = BrowserResultType.HttpError
                };
            }
        }
    }

}
