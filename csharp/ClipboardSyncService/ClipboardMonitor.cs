// Clipboard monitor using Win32 API
using ClipboardSyncService;
using System.Runtime.InteropServices;
using System.Threading;
using System;
using System.Windows.Forms;

public class ClipboardMonitor
{
    private readonly ClipboardSyncManager manager;
    private Thread monitorThread;
    private volatile bool running;

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
        running = true;
        monitorThread = new Thread(MonitorClipboard);
        monitorThread.IsBackground = true;
#pragma warning disable CA1416 // Validate platform compatibility
        monitorThread.SetApartmentState(ApartmentState.STA);
#pragma warning restore CA1416 // Validate platform compatibility
        monitorThread.Start();
    }

    public void Stop()
    {
        running = false;
        monitorThread?.Join(5000);
    }

    private void MonitorClipboard()
    {
        Application.SetCompatibleTextRenderingDefault(false);
        var form = new ClipboardForm(manager);
        Application.Run(form);
    }
}
