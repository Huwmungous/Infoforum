
using Duende.IdentityModel.OidcClient.Browser;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;  // Windows API

namespace GenerateBTTranslations
{
    public class WindowsCustomBrowser : IBrowser
    {
        public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<string>();
            WindowsProtocolCallbackHandler.Initialize(tcs);

            bool launched = await Launcher.LaunchUriAsync(new Uri(options.StartUrl));
            if (!launched)
            {
                return new BrowserResult
                {
                    ResultType = BrowserResultType.HttpError,
                    Error = "Failed to launch the system browser."
                };
            }

            string callbackUri;
            try
            {
                callbackUri = await tcs.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new BrowserResult
                {
                    ResultType = BrowserResultType.UserCancel,
                    Error = "Authentication canceled."
                };
            }

            return new BrowserResult
            {
                ResultType = BrowserResultType.Success,
                Response = callbackUri
            };
        }
    }
}
