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
                _appName ??= Assembly.GetEntryAssembly()?.GetName().Name;
                return _appName!;
            }
        }
    }
}
