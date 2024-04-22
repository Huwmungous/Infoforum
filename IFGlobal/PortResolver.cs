using Microsoft.Extensions.Logging;

namespace IFGlobal
{
    public class PortResolver
    {
        static string? _appName;

        public static int GetPort()
        {
            return GetPort(AppName);
        }

        public static int GetPort(string appname)
        {
            switch (appname)
            {
                case "BreakTackleAPI": return 5001;
                case "JibberJabber": return 5002;
                case "BTAuthenticator": return 5003;
                case "IFAuthenticator": return 5004;

                default:
                    string msg = $"PortResolver Does Not Recognise {appname}";

                    Console.WriteLine(msg);
                    
                    throw new ArgumentException(msg);
            }
        }

        private static string AppName
        {
            get
            {
                if (_appName == null)
                    _appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                return _appName!;
            }
        }
    }
}
