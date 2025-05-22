using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;
using Microsoft.Extensions.DependencyInjection;

namespace ClipboardSyncService
{
    // Service installer
    [RunInstaller(true)]
    public partial class ClipboardSyncInstaller : Installer
    {
        public ClipboardSyncInstaller()
        {
            var processInstaller = new ServiceProcessInstaller();
            var serviceInstaller = new ServiceInstaller();

            processInstaller.Account = ServiceAccount.LocalSystem;
            serviceInstaller.DisplayName = "gRPC Clipboard Sync Service";
            serviceInstaller.StartType = ServiceStartMode.Automatic;
            serviceInstaller.ServiceName = "ClipboardSyncService";

            Installers.Add(serviceInstaller);
            Installers.Add(processInstaller);
        }
    }
}