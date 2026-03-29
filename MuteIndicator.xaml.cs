using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using EchoX.Services;

namespace EchoX
{
    public partial class MuteIndicator : Window
    {
        private const int GWL_EXSTYLE       = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED     = 0x00080000;
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        public MuteIndicator(AppSettings? settings = null)
        {
            InitializeComponent();
            PositionWindow(settings ?? new StorageService().LoadAppSettings());
            SourceInitialized += (s, e) => SetClickThrough();
        }

        public void PositionWindow(AppSettings? settings = null)
        {
            var appSettings = settings ?? new StorageService().LoadAppSettings();
            var screen = OverlayLayoutService.GetPreferredScreen();
            var position = OverlayLayoutService.GetPosition(appSettings, Models.OverlayIds.MuteIndicator, Width, Height, screen);
            Left = position.X;
            Top = position.Y;
        }

        private void SetClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            // WS_EX_TRANSPARENT + WS_EX_LAYERED = click-through
            // WS_EX_TOOLWINDOW = hide from Alt+Tab
            SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_LAYERED | 0x00000080);
        }
    }
}
