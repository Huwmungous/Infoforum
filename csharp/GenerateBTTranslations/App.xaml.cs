
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.Activation;

namespace GenerateBTTranslations
{
    // This class should derive from MauiWinUIApp on Windows.
    public partial class App : MauiWinUIApp
    {
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            base.OnLaunched(args);
            // Other launch code, if any.
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            base.OnActivated(args);

            if (args.Kind == ActivationKind.Protocol)
            {
                var protocolArgs = args as ProtocolActivatedEventArgs;
                // Pass the callback URI to your protocol handler.
                WindowsProtocolCallbackHandler.CompleteCallback(protocolArgs.Uri.AbsoluteUri);
            }
        }
    }
}
