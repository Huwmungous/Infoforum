using System;
using System.Reflection;

namespace IFGlobal
{
    public class PortResolver
    {
        static string _appName = "";

        public static int GetPort() { return GetPort(AppName); }

        public static int GetPort(string appname)
        {
            switch (appname)
            {
                case "BreakTackle": return 5000;
                case "BreakTackleAPI": return 5001;
                case "JibberJabber": return 5002;
                case "BTAuthenticator": return 5003;
                case "IFAuthenticator": return 5004;
                case "SvgApi": return 5005;
                case "KeyCloak_Reserved_1": return 5006;
                case "KeyCloak_Reserved_2": return 5007;
                case "IFOllama": return 5008;
                case "File-Manager": return 5009;
                case "ClipboardSyncService": return 5010;

                default:
                    string msg = $"PortResolver Does Not Recognise '{appname}'";

                    Console.WriteLine(msg);

                    throw new ArgumentException(msg);
            }
        }
        private static string AppName
        {
            get
            {
                if (string.IsNullOrEmpty(_appName))
                {
                    var entryAssembly = Assembly.GetEntryAssembly() ?? throw new InvalidOperationException("Entry assembly is null. Unable to determine application name.");
                    _appName = entryAssembly.GetName().Name ?? throw new InvalidOperationException("Application name is null.");
                }
                return _appName;
            }
        }
    }
}
