using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using EchoX.Services;

namespace EchoX
{
    public partial class NotificationPopup : Window
    {
        private readonly System.Windows.Threading.DispatcherTimer _timer;

        // Make the window click-through at the Win32 level
        private const int GWL_EXSTYLE       = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED     = 0x00080000;
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        [DllImport("winmm.dll")] static extern bool PlaySound(string? sound, IntPtr hmod, uint fdwSound);
        private const uint SND_PURGE = 0x0040;

        public NotificationPopup(string title, string message, AppSettings? settings = null)
        {
            InitializeComponent();

            // Kill any pending system sound immediately
            PlaySound(null, IntPtr.Zero, SND_PURGE);
            TitleText.Text   = title;

            // For profile switch: "Activated: ProfileName" — highlight the name in green
            if (title.Equals("Profile Switched", StringComparison.OrdinalIgnoreCase) &&
                message.StartsWith("Activated: ", StringComparison.OrdinalIgnoreCase))
            {
                LogoIcon.Visibility = Visibility.Collapsed;
                NotificationIcon.Visibility = Visibility.Visible;
                string profileName = message.Substring("Activated: ".Length);
                MessageText.Inlines.Add(new System.Windows.Documents.Run("Activated: ")
                    { Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8A9199")) });
                MessageText.Inlines.Add(new System.Windows.Documents.Run(profileName)
                    { Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#53C028")),
                      FontWeight = System.Windows.FontWeights.SemiBold });
            }
            else
            {
                LogoIcon.Visibility = Visibility.Collapsed;
                NotificationIcon.Visibility = Visibility.Visible;
                MessageText.Inlines.Add(new System.Windows.Documents.Run(message)
                    { Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8A9199")) });
            }

            if (title.Equals("Microphone", StringComparison.OrdinalIgnoreCase))
            {
                LogoIcon.Visibility = Visibility.Collapsed;
                NotificationIcon.Visibility = Visibility.Collapsed;
                bool isMuted = message.Equals("Muted", StringComparison.OrdinalIgnoreCase);
                if (isMuted)
                {
                    MicMutedIcon.Visibility = Visibility.Visible;
                }
                else
                {
                    MicIcon.Visibility = Visibility.Visible;
                    MicIcon.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#53C028"));
                    MicIcon.Text = "\uE720";
                }
            }

            _timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _timer.Tick += (s, e) => { _timer.Stop(); FadeOut(); };

            SourceInitialized += (s, e) => SetClickThrough();

            Loaded += (s, e) =>
            {
                var appSettings = settings ?? new StorageService().LoadAppSettings();
                UpdateLayout();
                var popupHeight = ActualHeight > 0 ? ActualHeight : 64;
                var screen = OverlayLayoutService.GetPreferredScreen();
                var position = OverlayLayoutService.GetPosition(appSettings, Models.OverlayIds.NotificationPopup, Width, popupHeight, screen);
                Left = position.X;
                Top = position.Y;

                Opacity = 0;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                BeginAnimation(OpacityProperty, fadeIn);
                _timer.Start();
            };
        }

        private void SetClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_LAYERED | 0x00000080);
        }

        private void FadeOut()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s, e) => Close();
            BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}
