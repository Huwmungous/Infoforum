using System.Threading.Tasks;

namespace GenerateBTTranslations
{
    public static class WindowsProtocolCallbackHandler
    {
        private static TaskCompletionSource<string> _tcs;

        public static void Initialize(TaskCompletionSource<string> tcs)
        {
            _tcs = tcs;
        }

        public static void CompleteCallback(string callbackUri)
        {
            _tcs?.TrySetResult(callbackUri);
            _tcs = null;
        }
    }
}
