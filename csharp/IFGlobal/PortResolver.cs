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
                case "ConfigWebService": return 5000;
                case "LoggerWebService": return 5001;
                case "KeycloakWebService": return 5002;

                // Core applications (5000-5011)

                case "BTAuthenticator": return 5003;
                case "IFAuthenticator": return 5004;
                case "SvgApi": return 5005;
                case "KeyCloak_Reserved_1": return 5006;
                case "KeyCloak_Reserved_2": return 5007;
                case "IFOllama": return 5008;
                case "File-Manager": return 5009;
                case "ClipboardSyncService": return 5010;
                case "SampleWebService": return 5011;

                // MCP Servers (5012-5027)
                case "BraveSearchMcpServer": return 5012;
                case "CodeAnalysisMcpServer": return 5013;
                case "CodeFormatterMcpServer": return 5014;
                case "ConfigManagementMcpServer": return 5015;
                case "DatabaseCompareMcpServer": return 5016;
                case "DocumentationMcpServer": return 5017;
                case "DotNetBuildMcpServer": return 5018;
                case "FileSystemMcpServer": return 5019;
                case "FileTransferMcpServer": return 5020;
                case "FirebirdMcpServer": return 5021;
                case "GitMcpServer": return 5022;
                case "PlaywrightMcpServer": return 5023;
                case "SqlGeneratorMcpServer": return 5024;
                case "SqliteMcpServer": return 5025;
                case "TestGeneratorMcpServer": return 5026;
                case "UiComponentConverterMcpServer": return 5027;

                // IFOllama WebService and React server (5028-5029)
                case "IFOllama.WebService": return 5028;
                case "IFOllama.React": return 5029;

                case "BreakTackle": return 5030;
                case "BreakTackleAPI": return 5031;
                case "JibberJabber": return 5032;

                // IF WebServices (5030-5039)


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
