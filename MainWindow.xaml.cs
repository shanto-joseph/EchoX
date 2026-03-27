using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EchoX.ViewModels;
using Microsoft.Win32;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Input;
using NHotkey;
using NHotkey.Wpf;

namespace EchoX
{
    public partial class MainWindow : Window
    {
        // DWM rounded corners
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;
        private readonly MainWindowViewModel _viewModel;
        private bool _isExiting = false; // track explicit exit
        private NotifyIcon _trayIcon = null!;
        private Icon? _activeIcon;
        private Icon? _mutedIcon;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;

            _viewModel.ShowTrayNotification += (title, message) =>
                _trayIcon.ShowBalloonTip(2000, title, message, ToolTipIcon.Info);

            SetupTrayIcon();
            SetupHotkeys();
            CheckStartupStatus();

            _viewModel.MicrophoneMuteChanged += OnMicrophoneMuteChanged;

            // Set initial tray state
            OnMicrophoneMuteChanged(_viewModel.AudioEngine.IsDefaultMicMuted);

            // Apply OS-level rounded corners (Windows 11)
            SourceInitialized += (_, _) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                int pref = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
            };
        }

        private void SetupTrayIcon()
        {
            _activeIcon = CreateIconFromPath(ActiveIconPath, new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215)));
            _mutedIcon = CreateMuteIcon();

            _trayIcon = new NotifyIcon
            {
                Icon = _activeIcon,
                Visible = true,
                Text = "EchoX Audio Manager"
            };

            // Double-clicking the tray icon brings the app back up
            _trayIcon.DoubleClick += (s, e) => ShowApp();

            // Right-click menu
            _trayIcon.ContextMenuStrip = new ContextMenuStrip();
            _trayIcon.ContextMenuStrip.Items.Add("Open EchoX", null, (s, e) => ShowApp());
            _trayIcon.ContextMenuStrip.Items.Add("Exit", null, (s, e) => CloseApp());
        }

        private const string MuteIconPath = "M10.949933333333334 11.892666666666665c-0.688 0.3892 -1.4605333333333332 0.6464666666666666 -2.2831333333333332 0.7374V15.333333333333332h-1.3333333333333333v-2.7032666666666665C4.55236 12.322599999999998 2.3441533333333333 10.1144 2.0367266666666666 7.333333333333333h1.3439733333333335c0.32348 2.2615333333333334 2.26842 4 4.619433333333333 4 0.7000666666666666 0 1.3641333333333332 -0.15413333333333332 1.9601333333333333 -0.43039999999999995l-1.0334666666666665 -1.0334666666666665c-0.2942 0.08499999999999999 -0.6051333333333333 0.13053333333333333 -0.9266666666666665 0.13053333333333333 -1.84098 0 -3.33336 -1.4924 -3.33336 -3.333333333333333V5.609473333333334l-3.7377399999999996 -3.7377333333333334L1.8718466666666667 0.9289333333333333l13.199353333333331 13.199333333333332 -0.9428666666666665 0.9427999999999999 -3.1784 -3.1784Zm1.9665333333333335 -1.7857333333333332 -0.9615999999999999 -0.9616666666666666c0.3389333333333333 -0.5396666666666666 0.5704666666666667 -1.1536666666666666 0.6646666666666666 -1.8119333333333334h1.3439333333333332c-0.1132 1.0244 -0.48433333333333334 1.971 -1.047 2.7736Zm-1.9392666666666667 -1.9393333333333331L5.123833333333333 2.31426C5.702846666666667 1.3284533333333333 6.7742 0.6666666666666666 8.000133333333332 0.6666666666666666c1.8409333333333333 0 3.333333333333333 1.4923866666666665 3.333333333333333 3.333333333333333v2.6666666666666665c0 0.5399333333333333 -0.1284 1.0498666666666665 -0.3562666666666666 1.5009333333333332Z";
        private const string ActiveIconPath = "M8.000133333333332 0.6666666666666666c1.8409333333333333 0 3.333333333333333 1.4923866666666665 3.333333333333333 3.333333333333333v2.6666666666666665c0 1.8409466666666665 -1.4924 3.3333333333333334 -3.333333333333333 3.3333333333333334s-3.3333333333333334 -1.4923866666666665 -3.3333333333333334 -3.3333333333333334V4C4.6668 2.159053333333333 6.1592 0.6666666666666666 8.000133333333332 0.6666666666666666Zm6 6.666666666666667h-1.3333333333333333C12.6668 10.066666666666666 10.583466666666666 12 8.000133333333332 12c-2.583333333333333 0 -4.666666666666667 -1.9333333333333333 -4.666666666666667 -4.666666666666667H2C2 10.276 4.390666666666666 12.910666666666667 7.333466666666666 13.266666666666666V15.333333333333332h1.3333333333333333V13.266666666666666C11.609466666666667 12.910666666666667 14.000133333333332 10.276 14.000133333333332 7.333333333333333Z";

        private void OnMicrophoneMuteChanged(bool isMuted)
        {
            if (_trayIcon == null) return;

            if (isMuted)
            {
                _trayIcon.Icon = _mutedIcon;
                _trayIcon.Text = "EchoX (MUTED)";
            }
            else
            {
                _trayIcon.Icon = _activeIcon;
                _trayIcon.Text = "EchoX Audio Manager";
            }
        }

        private System.Drawing.Icon CreateMuteIcon()
        {
            return CreateIconFromPath(MuteIconPath, System.Windows.Media.Brushes.Crimson);
        }

        private System.Drawing.Icon CreateIconFromPath(string pathData, System.Windows.Media.Brush brush)
        {
            try
            {
                var geometry = System.Windows.Media.Geometry.Parse(pathData);
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    // Scale from 16x16 root viewBox to a clear 32x32 tray icon
                    dc.PushTransform(new ScaleTransform(2, 2));
                    dc.DrawGeometry(brush, null, geometry);
                }

                var renderTarget = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
                renderTarget.Render(visual);

                // Convert RenderTargetBitmap to System.Drawing.Icon
                var pixels = new byte[32 * 32 * 4];
                renderTarget.CopyPixels(pixels, 32 * 4, 0);

                using (var bitmap = new System.Drawing.Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                {
                    var data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, 32, 32), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
                    Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                    bitmap.UnlockBits(data);

                    IntPtr hIcon = bitmap.GetHicon();
                    return System.Drawing.Icon.FromHandle(hIcon);
                }
            }
            catch
            {
                // Fallback circle if rendering fails
                using (var bitmap = new System.Drawing.Bitmap(32, 32))
                {
                    using (var g = System.Drawing.Graphics.FromImage(bitmap))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        g.FillEllipse(System.Drawing.Brushes.Crimson, 2, 2, 28, 28);
                    }
                    return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
                }
            }
        }

        private void SetupHotkeys()
        {
            bool cycleRegistered = TryRegisterHotkey("CycleAudio", Key.S, ModifierKeys.Control | ModifierKeys.Alt, CycleProfiles);
            bool muteRegistered = TryRegisterHotkey("MuteMic", Key.K, ModifierKeys.Control | ModifierKeys.Alt, ToggleMicMute); // Changed from 'M' (common conflict) to 'K'

            UpdateHotkeyStatus(cycleRegistered, muteRegistered);
        }

        private bool TryRegisterHotkey(string name, Key key, ModifierKeys modifiers, EventHandler<HotkeyEventArgs> handler)
        {
            try
            {
                HotkeyManager.Current.AddOrReplace(name, key, modifiers, handler);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to register hotkey {name}: {ex.Message}");
                _trayIcon?.ShowBalloonTip(2000, "EchoX Hotkey", $"Failed to register {name}. It may be in use by another app.", ToolTipIcon.Warning);
                return false;
            }
        }

        private void UpdateHotkeyStatus(bool cycleRegistered, bool muteRegistered)
        {
            // TODO: Update ViewModel for hotkey status
            // HotkeyStatusText.Text = $"Hotkeys: Ctrl+Alt+S={(cycleRegistered ? "Ready" : "Unavailable")}; Ctrl+Alt+M={(muteRegistered ? "Ready" : "Unavailable")}";
            // HotkeyStatusText.Foreground = (cycleRegistered && muteRegistered) ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.Orange;
        }

        // When the user clicks the standard minimize button [-]
        protected override void OnStateChanged(EventArgs e)
        {
            // Allow normal minimization to taskbar
            base.OnStateChanged(e);
        }

        private void ShowApp()
        {
            // Guard: if the window was closed (not just hidden), don't attempt Show
            if (!this.IsLoaded || _isExiting)
                return;

            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            this.Show();
            this.Activate(); // Bring it to the front
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isExiting)
            {
                // Hide instead of close when user hits the X: keep tray application running
                e.Cancel = true;
                this.Hide();
                return;
            }
            base.OnClosing(e);
        }

        private void CloseApp()
        {
            _isExiting = true;
            _trayIcon?.Dispose();
            if (_mutedIcon != null) DestroyIcon(_mutedIcon.Handle);
            System.Windows.Application.Current.Shutdown();
        }

        // Hotkey handler: Ctrl + Alt + S cycles through profiles
        private void CycleProfiles(object sender, HotkeyEventArgs e)
        {
            _viewModel.ProfilesViewModel.CycleProfiles();
            e.Handled = true; // Tell Windows we handled the key press
        }

        // ================= NEW: MUTE PANIC BUTTON =================
        private void ToggleMicMute(object sender, HotkeyEventArgs e)
        {
            _viewModel.ProfilesViewModel.ToggleMicMute();
            e.Handled = true;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                MaximizeBtn_Click(sender, e);
            else
                this.DragMove();
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
            => CloseApp();

        private void SidebarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button || button.Tag is not string tag)
                return;

            int index = tag switch
            {
                "Communication" => 0,
                "Profiles" => 1,
                "Shortcuts" => 2,
                "Settings" => 3,
                "About" => 4,
                _ => 0
            };

            MainTabControl.SelectedIndex = index;
            UpdateActiveNavButton(index);
        }

        private void UpdateActiveNavButton(int activeIndex)
        {
            var navButtons = new[] { BtnNav0, BtnNav1, BtnNav2, BtnNav3, BtnNav4 };
            var active = (Style)FindResource("NavBtnActive");
            var normal = (Style)FindResource("NavBtn");

            for (int i = 0; i < navButtons.Length; i++)
            {
                navButtons[i].Style = i == activeIndex ? active : normal;
                var color = i == activeIndex
                    ? (System.Windows.Media.Brush)FindResource("Accent")
                    : (System.Windows.Media.Brush)FindResource("TextMid");

                if (navButtons[i].Content is System.Windows.Controls.StackPanel sp)
                {
                    foreach (var tb in sp.Children.OfType<System.Windows.Controls.TextBlock>())
                        tb.Foreground = color;

                    // Handle vector icons (Path) as well 
                    foreach (var viewbox in sp.Children.OfType<System.Windows.Controls.Viewbox>())
                        if (viewbox.Child is System.Windows.Shapes.Path path)
                            path.Fill = color;
                }
            }
        }
        private void CheckStartupStatus()
        {
            // TODO: Move to SettingsViewModel
            // using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            // bool hasStartup = key?.GetValue("EchoX") != null;
            // StartupCheckbox.IsChecked = hasStartup;
            // StartupStatusText.Text = hasStartup ? "Startup: Enabled" : "Startup: Disabled";
            // StartupStatusText.Foreground = hasStartup ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.Gray;
        }

        private void StartupCheckbox_Click(object sender, RoutedEventArgs e)
        {
            // string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            // using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

            // bool enabled = StartupCheckbox.IsChecked == true;
            // if (enabled)
            // {
            //     key?.SetValue("EchoX", appPath); // Add it to Windows Startup
            // }
            // else
            // {
            //     key?.DeleteValue("EchoX", false); // Remove it from Windows Startup
            // }

            // StartupStatusText.Text = enabled ? "Startup: Enabled" : "Startup: Disabled";
            // StartupStatusText.Foreground = enabled ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.Gray;
        }

        private void VolumeSlider_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var slider = (sender as System.Windows.Controls.Slider) ?? FindVisualChild<System.Windows.Controls.Slider>(sender as DependencyObject);
            if (slider != null)
            {
                double step = 2;
                double newValue = slider.Value + (e.Delta > 0 ? step : -step);
                slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, newValue));
                e.Handled = true;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch { }
        }

        private T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }
    }
}