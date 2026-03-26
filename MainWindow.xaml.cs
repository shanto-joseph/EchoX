using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
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

        // Add our tray icon variable
        private NotifyIcon _trayIcon = null!;

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
            _trayIcon = new NotifyIcon
            {
                // We will use the default Windows settings icon for now
                Icon = SystemIcons.Information,
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
            _trayIcon.Dispose(); // Clean up the icon
            System.Windows.Application.Current.Shutdown(); // Kill the app completely
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
                "Profiles"      => 1,
                "Shortcuts"     => 2,
                "Settings"      => 3,
                "About"         => 4,
                _               => 0
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
                    foreach (var tb in sp.Children.OfType<System.Windows.Controls.TextBlock>())
                        tb.Foreground = color;
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
            // TODO: Move to SettingsViewModel
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
    }
}