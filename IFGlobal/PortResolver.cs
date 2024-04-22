namespace IFGlobal
{
    public class PortResolver
    {
        static string? _appName;

        private static string AppName 
        {
            get 
            { 
                if(_appName is null) 
                    _appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                return _appName!;
            }
        }
        public static int GetPort()
        {
            return GetPort(AppName);
        }

        public static int GetPort(string appname)
        {

            switch (appname)
            {
                case "BTAuthenticator": return 5003;
                case "IFAuthenticator": return 5004;
            }

            return 5000;
        }
    }
}
