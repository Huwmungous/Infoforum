// Hidden form for clipboard monitoring
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System;
using ClipboardSyncService;

namespace ClipboardSyncService
{
    public class ClipboardForm : Form
    {
        private readonly ClipboardSyncManager manager;
        private IntPtr nextClipboardViewer;
        private const int WM_DRAWCLIPBOARD = 0x308;
        private const int WM_CHANGECBCHAIN = 0x030D;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint SetClipboardViewer(nint hWndNewViewer);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ChangeClipboardChain(nint hWndRemove, nint hWndNewNext);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint SendMessage(nint hWnd, uint Msg, nint wParam, nint lParam);


        public ClipboardForm(ClipboardSyncManager manager)
        {
            this.manager = manager;
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Visible = false;

            nextClipboardViewer = SetClipboardViewer(this.Handle);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_DRAWCLIPBOARD:
                    manager.OnClipboardChanged();
                    SendMessage(nextClipboardViewer, (uint)m.Msg, m.WParam, m.LParam);
                    break;

                case WM_CHANGECBCHAIN:
                    if (m.WParam == nextClipboardViewer)
                        nextClipboardViewer = m.LParam;
                    else
                        SendMessage(nextClipboardViewer, (uint)m.Msg, m.WParam, m.LParam);
                    break;

                default:
                    base.WndProc(ref m);
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            ChangeClipboardChain(this.Handle, nextClipboardViewer);
            base.Dispose(disposing);
        }
    }
}