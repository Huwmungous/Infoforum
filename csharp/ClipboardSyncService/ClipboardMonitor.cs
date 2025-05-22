// Clipboard monitor using Win32 API
using ClipboardSyncService;
using System.Runtime.InteropServices;
using System.Threading;
using System;
using System.Windows.Forms;

namespace ClipboardSyncService
{
    public class ClipboardMonitor
    {
        private readonly ClipboardSyncManager manager;
        private Thread monitorThread;

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

        public ClipboardMonitor(ClipboardSyncManager manager)
        {
            this.manager = manager;
        }

        public void Start()
        {
            monitorThread = new Thread(MonitorClipboard) { IsBackground = true }; 
            monitorThread.SetApartmentState(ApartmentState.STA); 
            monitorThread.Start();
        }

        public void Stop()
        {
            monitorThread?.Join(5000);
        }

        private void MonitorClipboard()
        {
            Application.SetCompatibleTextRenderingDefault(false);
            var form = new ClipboardForm(manager);
            Application.Run(form);
        }
    }

}