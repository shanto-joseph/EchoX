using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EchoX.ViewModels;
using Microsoft.Win32;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Windows.Input;

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
        private bool _isExiting = false;
        private NotifyIcon _trayIcon = null!;
        private Icon? _activeIcon;
        private Icon? _mutedIcon;
        private MuteIndicator? _muteIndicator;
        private AppVolumeMixerWindow? _appVolumeMixerWindow;
        private AboutWindow? _aboutWindow;
        private int _lastSelectedTabIndex = 0;
        private IntPtr _globalHotkeyHook = IntPtr.Zero;
        private LowLevelHookProc? _globalHotkeyProc;
        private IntPtr _windowHandle = IntPtr.Zero;
        private HwndSource? _windowSource;
        private readonly HashSet<int> _pressedKeyVks = new HashSet<int>();
        private readonly HashSet<int> _capturedGestureKeyVks = new HashSet<int>();
        private readonly HashSet<int> _recordedProfileKeyVks = new HashSet<int>();
        private readonly HashSet<string> _registeredNativeGestures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string? _lastTriggeredGesture;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;

            SetupTrayIcon();
            SetupHotkeys();
            CheckStartupStatus();

            _viewModel.ProfilesViewModel.EditAndNavigateRequested += profile =>
            {
                MainTabControl.SelectedIndex = 1; // Profiles tab
            };

            _viewModel.MicrophoneMuteChanged += OnMicrophoneMuteChanged;

            // Listen for mute changes from Windows settings (external)
            _viewModel.AudioEngine.WatchMute(isMuted =>
            {
                OnMicrophoneMuteChanged(isMuted);
                _viewModel.DevicesViewModel.UpdateDeviceMuteStates();
            });

            // Set initial tray state
            OnMicrophoneMuteChanged(_viewModel.AudioEngine.IsDefaultMicMuted);

            // Apply OS-level rounded corners (Windows 11)
            SourceInitialized += (_, _) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                _windowHandle = hwnd;
                _windowSource = HwndSource.FromHwnd(hwnd);
                _windowSource?.AddHook(WndProc);
                int pref = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));

                // Install global mouse hook for side button profile shortcuts
                _mouseProc = MouseHookCallback;
                using var mod = System.Diagnostics.Process.GetCurrentProcess().MainModule!;
                _globalMouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(mod.ModuleName!), 0);
                _globalHotkeyProc = GlobalHotkeyHookCallback;
                _globalHotkeyHook = SetWindowsHookEx(WH_KEYBOARD_LL, _globalHotkeyProc, GetModuleHandle(mod.ModuleName!), 0);
                RegisterNativeHotkeys();
            };
        }

        private void SetupTrayIcon()
        {
            try
            {
                // Task Array (Tray) Icon
                _activeIcon = new Icon(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/LOGO/EchoX.ico"));
                _mutedIcon = CreateMuteIcon(); // Keep the red mic for muted status, or branding if desired

                _trayIcon = new NotifyIcon
                {
                    Icon = _activeIcon,
                    Visible = true,
                    Text = "EchoX Audio Manager"
                };

                _trayIcon.MouseClick += TrayIcon_MouseClick;
                _trayIcon.DoubleClick += (s, e) => ShowApp();

                BuildTrayMenu();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load tray icon: {ex.Message}");
                // Fallback: use procedural icon 
                _activeIcon = CreateIconFromPath(ActiveIconPath, new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215)));
                _trayIcon = new NotifyIcon { Icon = _activeIcon, Visible = true, Text = "EchoX" };
                _trayIcon.MouseClick += TrayIcon_MouseClick;
                _trayIcon.DoubleClick += (s, e) => ShowApp();
                BuildTrayMenu();
            }
        }

        private void BuildTrayMenu()
        {
            var menu = new ContextMenuStrip
            {
                ShowImageMargin = true,
                ShowCheckMargin = false,
                BackColor = System.Drawing.Color.FromArgb(28, 28, 28),
                ForeColor = System.Drawing.Color.FromArgb(240, 242, 245),
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular),
                Padding = new Padding(0),
                Renderer = new EchoXTrayMenuRenderer()
            };
            menu.Opening += (s, e) => ApplyRoundedTrayMenuRegion(menu);
            menu.SizeChanged += (s, e) => ApplyRoundedTrayMenuRegion(menu);
            menu.MinimumSize = new System.Drawing.Size(138, 0);

            var openItem = new ToolStripMenuItem("EchoX")
            {
                Image = _activeIcon?.ToBitmap(),
                Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold),
                Padding = new Padding(6, 7, 6, 7),
                TextAlign = ContentAlignment.MiddleCenter,
                Tag = "Header"
            };
            openItem.Click += (s, e) => ShowApp();

            var mixerItem = CreateTrayMenuItem("Volume Mixer", OpenMixerPopup);
            var updateItem = CreateTrayMenuItem("Check for Update", async () =>
            {
                ShowApp();
                await _viewModel.AboutViewModel.CheckForUpdatesAsync(false);
            });
            var aboutItem = CreateTrayMenuItem("About", OpenAboutWindow);
            var exitItem = CreateTrayMenuItem("Quit EchoX", CloseApp);
            exitItem.Tag = "Danger";

            menu.Items.Add(openItem);
            menu.Items.Add(CreateTraySeparator());
            menu.Items.Add(mixerItem);
            menu.Items.Add(updateItem);
            menu.Items.Add(aboutItem);
            menu.Items.Add(CreateTraySeparator());
            menu.Items.Add(exitItem);

            _trayIcon.ContextMenuStrip = menu;
        }

        private ToolStripMenuItem CreateTrayMenuItem(string text, Action action)
        {
            var item = new ToolStripMenuItem(text)
            {
                Padding = new Padding(10, 10, 10, 10),
                Margin = new Padding(0),
                TextAlign = ContentAlignment.MiddleCenter
            };
            item.Click += (s, e) => action();
            return item;
        }

        private static ToolStripSeparator CreateTraySeparator()
        {
            return new ToolStripSeparator
            {
                Margin = new Padding(0),
                AutoSize = false,
                Height = 1
            };
        }

        private static void ApplyRoundedTrayMenuRegion(ContextMenuStrip menu)
        {
            if (menu.Width <= 0 || menu.Height <= 0)
                return;

            using var path = CreateRoundedRectanglePath(new System.Drawing.Rectangle(0, 0, menu.Width, menu.Height), 14);
            menu.Region = new Region(path);
        }

        private void TrayIcon_MouseClick(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowApp();
                return;
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(System.Drawing.Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            var arc = new System.Drawing.Rectangle(bounds.Location, new System.Drawing.Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        private sealed class EchoXTrayMenuRenderer : ToolStripProfessionalRenderer
        {
            public EchoXTrayMenuRenderer() : base(new EchoXTrayMenuColors())
            {
                RoundedEdges = false;
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(62, 62, 62));
                var rect = new System.Drawing.Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = CreateRoundedRectanglePath(rect, 14);
                e.Graphics.DrawPath(pen, path);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var isHeader = string.Equals(e.Item.Tag as string, "Header", StringComparison.Ordinal);
                var isDanger = string.Equals(e.Item.Tag as string, "Danger", StringComparison.Ordinal);
                if (e.Item is ToolStripSeparator)
                    return;

                if (!e.Item.Selected)
                    return;

                var rect = new System.Drawing.Rectangle(6, 3, e.Item.Width - 12, e.Item.Height - 6);
                using var path = CreateRoundedRectanglePath(rect, 10);
                using var brush = new SolidBrush(isDanger
                    ? System.Drawing.Color.FromArgb(70, 37, 37)
                    : System.Drawing.Color.FromArgb(39, 43, 48));
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
            }

            protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
            {
                // Suppress the default image gutter so the popup stays compact.
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                if (e.Item is ToolStripSeparator)
                    return;

                var isHeader = string.Equals(e.Item.Tag as string, "Header", StringComparison.Ordinal);
                var isDanger = string.Equals(e.Item.Tag as string, "Danger", StringComparison.Ordinal);
                var color = isDanger
                    ? System.Drawing.Color.FromArgb(237, 104, 104)
                    : System.Drawing.Color.FromArgb(240, 242, 245);
                if (isHeader)
                {
                    var textFont = e.TextFont ?? e.Item.Font;
                    const string echoText = "Echo";
                    const string xText = "X";
                    var echoSize = TextRenderer.MeasureText(e.Graphics, echoText, textFont, new System.Drawing.Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                    var xSize = TextRenderer.MeasureText(e.Graphics, xText, textFont, new System.Drawing.Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                    int iconSize = 15;
                    int gap = 3;
                    int totalWidth = iconSize + gap + echoSize.Width + xSize.Width;
                    int startX = Math.Max(4, (e.Item.Width - totalWidth) / 2);
                    int textY = (e.Item.Height - echoSize.Height) / 2;

                    TextRenderer.DrawText(
                        e.Graphics,
                        echoText,
                        textFont,
                        new System.Drawing.Point(startX + iconSize + gap, textY),
                        color,
                        TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(
                        e.Graphics,
                        xText,
                        textFont,
                        new System.Drawing.Point(startX + iconSize + gap + echoSize.Width, textY),
                        System.Drawing.Color.FromArgb(83, 192, 40),
                        TextFormatFlags.NoPadding);
                    return;
                }

                TextRenderer.DrawText(
                    e.Graphics,
                    e.Text,
                    e.TextFont ?? e.Item.Font,
                    new System.Drawing.Rectangle(0, 0, e.Item.Width, e.Item.Height),
                    color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
            {
                var isHeader = string.Equals(e.Item.Tag as string, "Header", StringComparison.Ordinal);
                if (!isHeader || e.Image == null)
                    return;

                var textFont = e.Item.Font;
                var text = e.Item.Text ?? string.Empty;
                var textSize = TextRenderer.MeasureText(e.Graphics, text, textFont, new System.Drawing.Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                var size = 15;
                var gap = 3;
                var totalWidth = size + gap + textSize.Width;
                var x = Math.Max(4, (e.Item.Width - totalWidth) / 2);
                var y = (e.Item.Height - size) / 2;
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.DrawImage(e.Image, new System.Drawing.Rectangle(x, y, size, size));
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(72, 72, 72));
                int y = e.Item.Bounds.Top + (e.Item.Height / 2);
                e.Graphics.DrawLine(pen, 14, y, e.Item.Width - 14, y);
            }
        }

        private sealed class EchoXTrayMenuColors : ProfessionalColorTable
        {
            public override System.Drawing.Color ToolStripDropDownBackground => System.Drawing.Color.FromArgb(28, 28, 28);
            public override System.Drawing.Color MenuBorder => System.Drawing.Color.FromArgb(62, 62, 62);
            public override System.Drawing.Color MenuItemBorder => System.Drawing.Color.FromArgb(39, 43, 48);
            public override System.Drawing.Color MenuItemSelected => System.Drawing.Color.FromArgb(39, 43, 48);
            public override System.Drawing.Color MenuItemSelectedGradientBegin => System.Drawing.Color.FromArgb(39, 43, 48);
            public override System.Drawing.Color MenuItemSelectedGradientEnd => System.Drawing.Color.FromArgb(39, 43, 48);
            public override System.Drawing.Color MenuItemPressedGradientBegin => System.Drawing.Color.FromArgb(39, 43, 48);
            public override System.Drawing.Color MenuItemPressedGradientEnd => System.Drawing.Color.FromArgb(39, 43, 48);
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

                // Show persistent mute indicator if enabled
                if (_viewModel.SettingsViewModel.ShowMuteIndicator)
                {
                    if (_muteIndicator == null || !_muteIndicator.IsLoaded)
                    {
                        _muteIndicator = new MuteIndicator(_viewModel.SettingsViewModel.GetAppSettingsSnapshot());
                        _muteIndicator.Show();
                    }
                }
            }
            else
            {
                _trayIcon.Icon = _activeIcon;
                _trayIcon.Text = "EchoX Audio Manager";

                // Hide mute indicator
                _muteIndicator?.Close();
                _muteIndicator = null;
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

                var renderTarget = new System.Windows.Media.Imaging.RenderTargetBitmap(32, 32, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
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
            var kb = _viewModel.KeyBindsViewModel;
            kb.HotkeysChanged += () =>
            {
                _lastTriggeredGesture = null;
                RegisterNativeHotkeys();
            };
        }

        private void UpdateHotkeyStatus(bool cycleRegistered, bool muteRegistered) { }

        // When the user clicks the standard minimize button [-]
        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            if (WindowState == WindowState.Maximized)
            {
                // Constrain to work area so we don't go under the taskbar
                var screen = System.Windows.Forms.Screen.FromHandle(
                    new System.Windows.Interop.WindowInteropHelper(this).Handle);
                var wa = screen.WorkingArea;
                this.MaxWidth  = wa.Width;
                this.MaxHeight = wa.Height;
                // Offset the window to the work area origin
                this.Left = wa.Left;
                this.Top  = wa.Top;
            }
            else
            {
                this.MaxWidth  = double.PositiveInfinity;
                this.MaxHeight = double.PositiveInfinity;

                // Center on screen when restoring from maximized
                var screen = System.Windows.Forms.Screen.FromHandle(
                    new System.Windows.Interop.WindowInteropHelper(this).Handle);
                var wa = screen.WorkingArea;
                this.Left = wa.Left + (wa.Width  - this.Width)  / 2;
                this.Top  = wa.Top  + (wa.Height - this.Height) / 2;
            }
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
            _muteIndicator?.Close();
            UnregisterNativeHotkeys();
            if (_windowSource != null)
            {
                _windowSource.RemoveHook(WndProc);
                _windowSource = null;
            }
            if (_globalMouseHook != IntPtr.Zero) UnhookWindowsHookEx(_globalMouseHook);
            if (_globalHotkeyHook != IntPtr.Zero) UnhookWindowsHookEx(_globalHotkeyHook);
            if (_shortcutKbHook != IntPtr.Zero) UnhookWindowsHookEx(_shortcutKbHook);
            if (_globalKbCapHook != IntPtr.Zero) UnhookWindowsHookEx(_globalKbCapHook);
            _trayIcon?.Dispose();
            if (_mutedIcon != null) DestroyIcon(_mutedIcon.Handle);
            System.Windows.Application.Current.Shutdown();
        }

        // Hotkey handler: Ctrl + Alt + S cycles through profiles
        private void CycleProfiles()
        {
            _viewModel.ProfilesViewModel.CycleProfiles();
        }

        // ================= NEW: MUTE PANIC BUTTON =================
        private void ToggleMicMute()
        {
            _viewModel.ProfilesViewModel.ToggleMicMute();
        }

        private void OpenApp()
        {
            ShowApp();
        }

        private void OpenAboutWindow()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_aboutWindow != null && _aboutWindow.IsLoaded)
                {
                    _aboutWindow.Activate();
                    return;
                }

                _aboutWindow = new AboutWindow(_viewModel.AboutViewModel)
                {
                    Owner = this
                };
                _aboutWindow.Closed += (s, e) => _aboutWindow = null;
                _aboutWindow.Show();
                _aboutWindow.Activate();
            }));
        }

        private void OpenMixerPopup()
        {
            ShowOutputMixerWindow();
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
            => Close();

        private void ProfileHeading_Click(object sender, RoutedEventArgs e)
        {
            MainTabControl.SelectedIndex = 1;
            UpdateActiveNavButton(1);
        }

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
                _ => 0
            };

            MainTabControl.SelectedIndex = index;
            UpdateActiveNavButton(index);
        }

        private void MainTabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, MainTabControl))
                return;

            int newIndex = MainTabControl.SelectedIndex;
            if (newIndex != _lastSelectedTabIndex)
            {
                CleanupLeavingTab(_lastSelectedTabIndex);
                _lastSelectedTabIndex = newIndex;
            }

            UpdateActiveNavButton(newIndex);
        }

        private void CleanupLeavingTab(int tabIndex)
        {
            CloseOutputMixerWindow();

            if (tabIndex == 1)
            {
                if (_isRecordingShortcut)
                    StopRecording();

                _viewModel.ProfilesViewModel.CancelEditSession();
                return;
            }

            if (tabIndex == 2)
            {
                var kb = _viewModel.KeyBindsViewModel;
                if (kb.IsCapturingCycle || kb.IsCapturingMute || kb.IsCapturingMixer || kb.IsCapturingOpenApp)
                    kb.CancelCapture();

                if (_capturingProfile != null)
                    _capturingProfile.IsCapturingShortcut = false;

                if (_isCapturingGlobal)
                    StopCapturingGlobal();
            }
        }

        private void UpdateActiveNavButton(int activeIndex)
        {
            var navButtons = new[] { BtnNav0, BtnNav1, BtnNav2, BtnNav3 };
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

                    foreach (var viewbox in sp.Children.OfType<System.Windows.Controls.Viewbox>())
                    {
                        // Direct Path child
                        if (viewbox.Child is System.Windows.Shapes.Path path)
                            path.Fill = color;
                        // Path inside a Canvas
                        else if (viewbox.Child is System.Windows.Controls.Canvas canvas)
                            foreach (var p in canvas.Children.OfType<System.Windows.Shapes.Path>())
                                p.Fill = color;
                    }
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

        private void GitHubBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var url = (sender as System.Windows.Controls.Button)?.Tag?.ToString();
                if (!string.IsNullOrEmpty(url))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }

        private void ArrangeOverlaysBtn_Click(object sender, RoutedEventArgs e)
        {
            var arranger = new OverlayArrangementWindow(_viewModel.SettingsViewModel)
            {
                Owner = this
            };

            var applied = arranger.ShowDialog();
            if (applied == true && _muteIndicator != null && _muteIndicator.IsLoaded)
                _muteIndicator.PositionWindow(_viewModel.SettingsViewModel.GetAppSettingsSnapshot());
        }

        private void OutputMixerBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_appVolumeMixerWindow != null && _appVolumeMixerWindow.IsLoaded)
            {
                CloseOutputMixerWindow();
                return;
            }

            ShowOutputMixerWindow();
        }

        private void ShowOutputMixerWindow()
        {
            if (_appVolumeMixerWindow != null && _appVolumeMixerWindow.IsLoaded)
            {
                _appVolumeMixerWindow.Activate();
                return;
            }

            _appVolumeMixerWindow = new AppVolumeMixerWindow(_viewModel.DevicesViewModel);
            _appVolumeMixerWindow.ShowCenteredOnCurrentScreen();

            if (IsVisible)
                PreviewMouseDown += MainWindow_PreviewMouseDown;

            _appVolumeMixerWindow.Closed += (s, args) =>
            {
                PreviewMouseDown -= MainWindow_PreviewMouseDown;
                _appVolumeMixerWindow = null;
            };

            _appVolumeMixerWindow.Show();
            _appVolumeMixerWindow.Activate();
        }

        private void CloseOutputMixerWindow()
        {
            if (_appVolumeMixerWindow == null || !_appVolumeMixerWindow.IsLoaded)
                return;

            PreviewMouseDown -= MainWindow_PreviewMouseDown;
            _appVolumeMixerWindow.BeginClose();
            _appVolumeMixerWindow = null;
        }

        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_appVolumeMixerWindow != null && _appVolumeMixerWindow.IsLoaded)
                CloseOutputMixerWindow();
        }

        private bool _isRecordingShortcut = false;
        private bool _isCapturingGlobal = false;
        private EchoX.Models.AudioProfile? _capturingProfile = null;
        private IntPtr _shortcutKbHook = IntPtr.Zero;
        private IntPtr _globalKbCapHook = IntPtr.Zero;
        private IntPtr _globalMouseHook = IntPtr.Zero;

        private delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc fn, IntPtr hMod, uint threadId);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string name);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT { public System.Drawing.Point pt; public int mouseData, flags, time; public IntPtr dwExtraInfo; }

        private const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
        private const int WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;
        private const int WM_HOTKEY = 0x0312;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int XBUTTON1 = 1, XBUTTON2 = 2;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const int HotkeyIdOpenApp = 0x4501;
        private const int HotkeyIdCycle = 0x4502;
        private const int HotkeyIdMute = 0x4503;

        private LowLevelHookProc? _kbProc, _mouseProc;

        private void RecordShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecordingShortcut) { StopRecording(); return; }
            StartRecording();
        }

        private void StartRecording()
        {
            _isRecordingShortcut = true;
            _recordedProfileKeyVks.Clear();

            var btn = RecordShortcutBtn;
            var idle = FindVisualChild<System.Windows.Controls.StackPanel>(btn, "IdlePanel");
            var rec  = FindVisualChild<System.Windows.Controls.StackPanel>(btn, "RecordingPanel");
            if (idle != null) idle.Visibility = Visibility.Collapsed;
            if (rec  != null) rec.Visibility  = Visibility.Visible;

            using var mod = System.Diagnostics.Process.GetCurrentProcess().MainModule!;
            _kbProc = ProfileGestureKbHookCallback;
            _shortcutKbHook = SetWindowsHookEx(WH_KEYBOARD_LL, _kbProc, GetModuleHandle(mod.ModuleName!), 0);

            // Mouse hook stays alive globally — only install once
            if (_globalMouseHook == IntPtr.Zero)
            {
                _mouseProc = MouseHookCallback;
                _globalMouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(mod.ModuleName!), 0);
            }
        }

        private void StopRecording()
        {
            _isRecordingShortcut = false;
            _recordedProfileKeyVks.Clear();
            if (_shortcutKbHook != IntPtr.Zero) { UnhookWindowsHookEx(_shortcutKbHook); _shortcutKbHook = IntPtr.Zero; }

            var btn = RecordShortcutBtn;
            var idle = FindVisualChild<System.Windows.Controls.StackPanel>(btn, "IdlePanel");
            var rec  = FindVisualChild<System.Windows.Controls.StackPanel>(btn, "RecordingPanel");
            if (idle != null) idle.Visibility = Visibility.Visible;
            if (rec  != null) rec.Visibility  = Visibility.Collapsed;
        }

        public void StartCapturingGlobal()
        {
            _isCapturingGlobal = true;
            _capturedGestureKeyVks.Clear();
            if (_globalKbCapHook == IntPtr.Zero)
            {
                using var mod = System.Diagnostics.Process.GetCurrentProcess().MainModule!;
                _globalKbCapProc = GlobalKbCapCallback;
                _globalKbCapHook = SetWindowsHookEx(WH_KEYBOARD_LL, _globalKbCapProc, GetModuleHandle(mod.ModuleName!), 0);
            }
        }

        public void StopCapturingGlobal()
        {
            _isCapturingGlobal = false;
            _capturingProfile = null;
            _capturedGestureKeyVks.Clear();
            if (_globalKbCapHook != IntPtr.Zero) { UnhookWindowsHookEx(_globalKbCapHook); _globalKbCapHook = IntPtr.Zero; }
        }

        private LowLevelHookProc? _globalKbCapProc;

        private IntPtr GlobalKbCapCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isCapturingGlobal)
            {
                int message = wParam.ToInt32();
                bool isKeyDown = message == WM_KEYDOWN || message == WM_SYSKEYDOWN;
                bool isKeyUp = message == WM_KEYUP || message == WM_SYSKEYUP;
                if (!isKeyDown && !isKeyUp)
                    return CallNextHookEx(_globalKbCapHook, nCode, wParam, lParam);

                var info = System.Runtime.InteropServices.Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int vk = (int)info.vkCode;

                if (isKeyDown)
                    _capturedGestureKeyVks.Add(vk);
                else
                    _capturedGestureKeyVks.Remove(vk);

                if (System.Array.IndexOf(_modifierVKeys, vk) >= 0)
                    return CallNextHookEx(_globalKbCapHook, nCode, wParam, lParam);

                var profile = _capturingProfile;
                var gesture = BuildGestureFromCaptureKeys(vk);

                if (!isKeyUp)
                    return CallNextHookEx(_globalKbCapHook, nCode, wParam, lParam);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (profile != null)
                        _viewModel.ProfilesViewModel.SaveProfileShortcut(profile, gesture, null);
                    else
                        _viewModel.KeyBindsViewModel.TryCaptureGesture(gesture);
                    StopCapturingGlobal();
                }));
                return (IntPtr)1;
            }
            return CallNextHookEx(_globalKbCapHook, nCode, wParam, lParam);
        }

        private static readonly int[] _modifierVKeys = { 0x10, 0x11, 0x12, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C }; // Shift,Ctrl,Alt variants + Win
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        private IntPtr KbHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                var info = System.Runtime.InteropServices.Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int vk = (int)info.vkCode;

                // Ignore pure modifier key presses — wait for the actual key
                if (System.Array.IndexOf(_modifierVKeys, vk) >= 0)
                    return CallNextHookEx(_shortcutKbHook, nCode, wParam, lParam);

                var key  = System.Windows.Input.KeyInterop.KeyFromVirtualKey(vk);
                var mods = GetCurrentModifierKeys();
                var gesture = KeyBindsViewModel.BuildGesture(mods, key);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Check for conflict before assigning
                    string? conflict = _viewModel.ProfilesViewModel.GetShortcutConflict(gesture, null, null);
                    if (conflict != null)
                    {
                        _viewModel.ProfilesViewModel.SetShortcutFromKey(key, mods); // still set it
                        _viewModel.ProfilesViewModel.ShortcutWarning = $"Already used by \"{conflict}\"";
                    }
                    else
                    {
                        _viewModel.ProfilesViewModel.ShortcutWarning = null;
                        _viewModel.ProfilesViewModel.SetShortcutFromKey(key, mods);
                    }
                    StopRecording();
                }));
                return (IntPtr)1;
            }
            return CallNextHookEx(_shortcutKbHook, nCode, wParam, lParam);
        }

        private IntPtr ProfileGestureKbHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
                return CallNextHookEx(_shortcutKbHook, nCode, wParam, lParam);

            bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
            bool isKeyUp = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;
            if (!isKeyDown && !isKeyUp)
                return CallNextHookEx(_shortcutKbHook, nCode, wParam, lParam);

            var info = System.Runtime.InteropServices.Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int vk = (int)info.vkCode;

            if (isKeyDown)
            {
                _recordedProfileKeyVks.Add(vk);
                if (System.Array.IndexOf(_modifierVKeys, vk) >= 0)
                    return CallNextHookEx(_shortcutKbHook, nCode, wParam, lParam);
                return (IntPtr)1;
            }

            _recordedProfileKeyVks.Remove(vk);
            if (System.Array.IndexOf(_modifierVKeys, vk) >= 0)
                return CallNextHookEx(_shortcutKbHook, nCode, wParam, lParam);

            var gesture = BuildGestureFromRecordedKeys(vk);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var excludeProfileId = _viewModel.ProfilesViewModel.SelectedProfile?.Id;
                string? conflict = _viewModel.ProfilesViewModel.GetShortcutConflict(gesture, null, excludeProfileId);
                if (conflict != null)
                {
                    _viewModel.ProfilesViewModel.SetShortcutFromGesture(gesture);
                    _viewModel.ProfilesViewModel.ShortcutWarning = $"Already used by \"{conflict}\"";
                }
                else
                {
                    _viewModel.ProfilesViewModel.ShortcutWarning = null;
                    _viewModel.ProfilesViewModel.SetShortcutFromGesture(gesture);
                }
                StopRecording();
            }));
            return (IntPtr)1;
        }

        private static ModifierKeys GetCurrentModifierKeys()
        {
            var modifiers = ModifierKeys.None;

            if (IsKeyPressed(VK_CONTROL))
                modifiers |= ModifierKeys.Control;
            if (IsKeyPressed(VK_MENU))
                modifiers |= ModifierKeys.Alt;
            if (IsKeyPressed(VK_SHIFT))
                modifiers |= ModifierKeys.Shift;
            if (IsKeyPressed(VK_LWIN) || IsKeyPressed(VK_RWIN))
                modifiers |= ModifierKeys.Windows;

            return modifiers;
        }

        private static ModifierKeys GetModifierKeysFromVks(IEnumerable<int> virtualKeys)
        {
            var keySet = new HashSet<int>(virtualKeys);
            var modifiers = ModifierKeys.None;

            bool hasLeftControl = keySet.Contains(0xA2);
            bool hasRightControl = keySet.Contains(0xA3);
            bool hasGenericControl = keySet.Contains(0x11);
            bool hasLeftAlt = keySet.Contains(0xA4);
            bool hasRightAlt = keySet.Contains(0xA5);
            bool hasGenericAlt = keySet.Contains(0x12);

            bool altGrLikeChord = hasRightAlt &&
                !hasLeftAlt &&
                !hasRightControl &&
                (hasLeftControl || hasGenericControl);

            if ((hasLeftControl || hasRightControl || hasGenericControl) && !altGrLikeChord)
                modifiers |= ModifierKeys.Control;
            if (hasLeftAlt || hasRightAlt || hasGenericAlt)
                modifiers |= ModifierKeys.Alt;
            if (keySet.Contains(0x10) || keySet.Contains(0xA0) || keySet.Contains(0xA1))
                modifiers |= ModifierKeys.Shift;
            if (keySet.Contains(0x5B) || keySet.Contains(0x5C))
                modifiers |= ModifierKeys.Windows;

            return modifiers;
        }

        private static bool IsKeyPressed(int virtualKey)
        {
            return (GetKeyState(virtualKey) & 0x8000) != 0;
        }

        private void RegisterNativeHotkeys()
        {
            UnregisterNativeHotkeys();

            if (_windowHandle == IntPtr.Zero)
                return;

            TryRegisterNativeHotkey(HotkeyIdOpenApp, _viewModel.KeyBindsViewModel.IsOpenAppEnabled, _viewModel.KeyBindsViewModel.OpenAppGesture);
            TryRegisterNativeHotkey(HotkeyIdCycle, _viewModel.KeyBindsViewModel.IsCycleEnabled, _viewModel.KeyBindsViewModel.CycleGesture);
            TryRegisterNativeHotkey(HotkeyIdMute, _viewModel.KeyBindsViewModel.IsMuteEnabled, _viewModel.KeyBindsViewModel.MuteGesture);
        }

        private void UnregisterNativeHotkeys()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, HotkeyIdOpenApp);
                UnregisterHotKey(_windowHandle, HotkeyIdCycle);
                UnregisterHotKey(_windowHandle, HotkeyIdMute);
            }

            _registeredNativeGestures.Clear();
        }

        private void TryRegisterNativeHotkey(int id, bool isEnabled, string gesture)
        {
            if (!isEnabled || !TryConvertGestureToNativeHotkey(gesture, out uint modifiers, out uint virtualKey))
                return;

            if (RegisterHotKey(_windowHandle, id, modifiers, virtualKey))
                _registeredNativeGestures.Add(KeyBindsViewModel.NormalizeGesture(gesture));
        }

        private static bool TryConvertGestureToNativeHotkey(string gesture, out uint modifiers, out uint virtualKey)
        {
            modifiers = 0;
            virtualKey = 0;

            var normalized = KeyBindsViewModel.NormalizeGesture(gesture);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            Key key = Key.None;
            int keyCount = 0;

            foreach (var token in normalized.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries))
            {
                switch (token)
                {
                    case "Control":
                        modifiers |= MOD_CONTROL;
                        continue;
                    case "Alt":
                        modifiers |= MOD_ALT;
                        continue;
                    case "Shift":
                        modifiers |= MOD_SHIFT;
                        continue;
                    case "Windows":
                        modifiers |= MOD_WIN;
                        continue;
                }

                if (!Enum.TryParse(token, true, out Key parsedKey) || parsedKey == Key.None)
                    return false;

                key = parsedKey;
                keyCount++;
            }

            if (keyCount != 1)
                return false;

            virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
            return virtualKey != 0;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_HOTKEY || _isCapturingGlobal || _isRecordingShortcut || _isExiting)
                return IntPtr.Zero;

            Action? action = wParam.ToInt32() switch
            {
                HotkeyIdOpenApp => OpenApp,
                HotkeyIdCycle => CycleProfiles,
                HotkeyIdMute => ToggleMicMute,
                _ => null
            };

            if (action == null)
                return IntPtr.Zero;

            handled = true;
            Dispatcher.BeginInvoke(action);
            return IntPtr.Zero;
        }

        private IntPtr GlobalHotkeyHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
                return CallNextHookEx(_globalHotkeyHook, nCode, wParam, lParam);

            int message = wParam.ToInt32();
            bool isKeyDown = message == WM_KEYDOWN || message == WM_SYSKEYDOWN;
            bool isKeyUp = message == WM_KEYUP || message == WM_SYSKEYUP;
            if (!isKeyDown && !isKeyUp)
                return CallNextHookEx(_globalHotkeyHook, nCode, wParam, lParam);

            var info = System.Runtime.InteropServices.Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int vk = (int)info.vkCode;

            if (isKeyDown)
                _pressedKeyVks.Add(vk);
            else
                _pressedKeyVks.Remove(vk);

            if (_isCapturingGlobal || _isRecordingShortcut || _isExiting)
                return CallNextHookEx(_globalHotkeyHook, nCode, wParam, lParam);

            var gesture = BuildGestureFromPressedKeys();
            if (isKeyUp)
            {
                if (!string.Equals(_lastTriggeredGesture, gesture, StringComparison.OrdinalIgnoreCase))
                    _lastTriggeredGesture = null;
                return CallNextHookEx(_globalHotkeyHook, nCode, wParam, lParam);
            }

            if (string.IsNullOrWhiteSpace(gesture))
                return CallNextHookEx(_globalHotkeyHook, nCode, wParam, lParam);

            if (_registeredNativeGestures.Contains(KeyBindsViewModel.NormalizeGesture(gesture)))
                return CallNextHookEx(_globalHotkeyHook, nCode, wParam, lParam);

            var action = MatchGlobalGesture(gesture);
            if (action != null)
            {
                if (string.Equals(_lastTriggeredGesture, gesture, StringComparison.OrdinalIgnoreCase))
                    return (IntPtr)1;

                _lastTriggeredGesture = gesture;
                Dispatcher.BeginInvoke(action);
                return (IntPtr)1;
            }

            if (!_viewModel.ProfilesViewModel.HandleGesture(gesture))
                return CallNextHookEx(_globalHotkeyHook, nCode, wParam, lParam);

            if (string.Equals(_lastTriggeredGesture, gesture, StringComparison.OrdinalIgnoreCase))
                return (IntPtr)1;

            _lastTriggeredGesture = gesture;
            return (IntPtr)1;
        }

        private string BuildGestureFromPressedKeys(int? includeVk = null)
        {
            var allVks = _pressedKeyVks
                .Concat(includeVk.HasValue ? new[] { includeVk.Value } : Array.Empty<int>())
                .Distinct()
                .ToArray();

            var keys = allVks
                .Where(vk => Array.IndexOf(_modifierVKeys, vk) < 0)
                .Select(vk => KeyInterop.KeyFromVirtualKey(vk))
                .Where(key => key != Key.None && key != Key.System)
                .Distinct()
                .ToArray();

            return KeyBindsViewModel.BuildGesture(GetModifierKeysFromVks(allVks), keys);
        }

        private string BuildGestureFromCaptureKeys(int? includeVk = null)
        {
            var allVks = _capturedGestureKeyVks
                .Concat(includeVk.HasValue ? new[] { includeVk.Value } : Array.Empty<int>())
                .Distinct()
                .ToArray();

            var keys = allVks
                .Where(vk => Array.IndexOf(_modifierVKeys, vk) < 0)
                .Select(vk => KeyInterop.KeyFromVirtualKey(vk))
                .Where(key => key != Key.None && key != Key.System)
                .Distinct()
                .ToArray();

            return KeyBindsViewModel.BuildGesture(GetModifierKeysFromVks(allVks), keys);
        }

        private string BuildGestureFromRecordedKeys(int? includeVk = null)
        {
            var allVks = _recordedProfileKeyVks
                .Concat(includeVk.HasValue ? new[] { includeVk.Value } : Array.Empty<int>())
                .Distinct()
                .ToArray();

            var keys = allVks
                .Where(vk => Array.IndexOf(_modifierVKeys, vk) < 0)
                .Select(vk => KeyInterop.KeyFromVirtualKey(vk))
                .Where(key => key != Key.None && key != Key.System)
                .Distinct()
                .ToArray();

            return KeyBindsViewModel.BuildGesture(GetModifierKeysFromVks(allVks), keys);
        }

        private Action? MatchGlobalGesture(string gesture)
        {
            var kb = _viewModel.KeyBindsViewModel;
            gesture = KeyBindsViewModel.NormalizeGesture(gesture);

            if (kb.IsOpenAppEnabled && string.Equals(kb.OpenAppGesture, gesture, StringComparison.OrdinalIgnoreCase))
                return OpenApp;
            if (kb.IsCycleEnabled && string.Equals(kb.CycleGesture, gesture, StringComparison.OrdinalIgnoreCase))
                return CycleProfiles;
            if (kb.IsMuteEnabled && string.Equals(kb.MuteGesture, gesture, StringComparison.OrdinalIgnoreCase))
                return ToggleMicMute;
            return null;
        }

        private Action? MatchGlobalMouseButton(string mouseButton)
        {
            var kb = _viewModel.KeyBindsViewModel;

            if (kb.IsOpenAppEnabled && string.Equals(kb.OpenAppMouseButton, mouseButton, StringComparison.OrdinalIgnoreCase))
                return OpenApp;
            if (kb.IsCycleEnabled && string.Equals(kb.CycleMouseButton, mouseButton, StringComparison.OrdinalIgnoreCase))
                return CycleProfiles;
            if (kb.IsMuteEnabled && string.Equals(kb.MuteMouseButton, mouseButton, StringComparison.OrdinalIgnoreCase))
                return ToggleMicMute;

            return null;
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_XBUTTONDOWN)
            {
                var info = System.Runtime.InteropServices.Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                int btn = (info.mouseData >> 16) & 0xFFFF;
                string label = btn == XBUTTON1 ? "Mouse Button 4" : "Mouse Button 5";
                string keyName = btn == XBUTTON1 ? "XButton1" : "XButton2";
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_isRecordingShortcut)
                    {
                        _viewModel.ProfilesViewModel.SetMouseShortcut(label, keyName);
                        StopRecording();
                    }
                    else if (_isCapturingGlobal && _capturingProfile != null)
                    {
                        _viewModel.ProfilesViewModel.SaveProfileShortcut(_capturingProfile, null, keyName);
                        StopCapturingGlobal();
                    }
                    else if (_isCapturingGlobal)
                    {
                        _viewModel.KeyBindsViewModel.TryCaptureMouseButton(keyName);
                        StopCapturingGlobal();
                    }
                    else
                    {
                        var action = MatchGlobalMouseButton(keyName);
                        if (action != null)
                        {
                            action();
                            return;
                        }

                        _viewModel.ProfilesViewModel.HandleMouseButton(keyName);
                    }
                }));
                return (IntPtr)1;
            }
            return CallNextHookEx(_globalMouseHook, nCode, wParam, lParam);
        }

        private void ClearShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecordingShortcut) StopRecording();
            _viewModel.ProfilesViewModel.SetShortcutFromKey(System.Windows.Input.Key.Escape, System.Windows.Input.ModifierKeys.None);
        }

        private void CycleCaptureBtn_Click(object sender, RoutedEventArgs e)
        {
            var kb = _viewModel.KeyBindsViewModel;
            if (kb.IsCapturingCycle) { kb.CancelCapture(); StopCapturingGlobal(); return; }
            kb.StartCaptureCycleCommand.Execute(null);
            StartCapturingGlobal();
        }

        private void MuteCaptureBtn_Click(object sender, RoutedEventArgs e)
        {
            var kb = _viewModel.KeyBindsViewModel;
            if (kb.IsCapturingMute) { kb.CancelCapture(); StopCapturingGlobal(); return; }
            kb.StartCaptureMuteCommand.Execute(null);
            StartCapturingGlobal();
        }

        private void OpenAppCaptureBtn_Click(object sender, RoutedEventArgs e)
        {
            var kb = _viewModel.KeyBindsViewModel;
            if (kb.IsCapturingOpenApp) { kb.CancelCapture(); StopCapturingGlobal(); return; }
            kb.StartCaptureOpenAppCommand.Execute(null);
            StartCapturingGlobal();
        }

        private void MixerCaptureBtn_Click(object sender, RoutedEventArgs e)
        {
            var kb = _viewModel.KeyBindsViewModel;
            if (kb.IsCapturingMixer) { kb.CancelCapture(); StopCapturingGlobal(); return; }
            kb.StartCaptureMixerCommand.Execute(null);
            StartCapturingGlobal();
        }

        private void KeyBindsEditProfile_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is EchoX.Models.AudioProfile profile)
            {
                // Toggle capture for this profile
                if (_isCapturingGlobal && _capturingProfile == profile)
                {
                    profile.IsCapturingShortcut = false;
                    StopCapturingGlobal();
                    return;
                }
                // Cancel any previous capture
                if (_capturingProfile != null) _capturingProfile.IsCapturingShortcut = false;
                StopCapturingGlobal();

                profile.IsCapturingShortcut = true;
                _capturingProfile = profile;
                StartCapturingGlobal();
            }
        }

        private void KeyBindsClearProfile_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is EchoX.Models.AudioProfile profile)
            {
                if (_isCapturingGlobal && _capturingProfile == profile)
                    StopCapturingGlobal();
                _viewModel.ProfilesViewModel.ClearProfileShortcut(profile);
            }
        }

        private void ProfileOptionsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void ProfileMenuEdit_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.MenuItem)?.Tag is EchoX.Models.AudioProfile profile)
                _viewModel.ProfilesViewModel.EditCommand.Execute(profile);
        }

        private void ProfileMenuDelete_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.MenuItem)?.Tag is EchoX.Models.AudioProfile profile)
                _viewModel.ProfilesViewModel.DeleteCommand.Execute(profile);
        }

        private T? FindVisualChild<T>(System.Windows.DependencyObject parent, string name) where T : System.Windows.FrameworkElement
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T fe && fe.Name == name) return fe;
                var result = FindVisualChild<T>(child, name);
                if (result != null) return result;
            }
            return null;
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
