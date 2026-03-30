using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using EchoX.ViewModels;
using Forms = System.Windows.Forms;

namespace EchoX
{
    public partial class AppVolumeMixerWindow : Window
    {
        private readonly DevicesViewModel _devicesViewModel;
        private readonly ObservableCollection<AppVolumeSessionViewModel> _sessions = new ObservableCollection<AppVolumeSessionViewModel>();
        private readonly System.Windows.Threading.DispatcherTimer _refreshTimer;
        private bool _isClosing;
        private bool _isRefreshing;

        public AppVolumeMixerWindow(DevicesViewModel devicesViewModel)
        {
            InitializeComponent();
            _devicesViewModel = devicesViewModel;
            DataContext = this;
            _devicesViewModel.PropertyChanged += DevicesViewModel_PropertyChanged;
            Loaded += AppVolumeMixerWindow_Loaded;

            _refreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1200)
            };
            _refreshTimer.Tick += async (s, e) => await RefreshSessionsAsync();

            Loaded += async (s, e) =>
            {
                await RefreshSessionsAsync();
                _refreshTimer.Start();
            };

            Closed += (s, e) =>
            {
                _refreshTimer.Stop();
                _devicesViewModel.PropertyChanged -= DevicesViewModel_PropertyChanged;
            };
        }

        public ObservableCollection<AppVolumeSessionViewModel> Sessions => _sessions;

        public async Task RefreshSessionsAsync()
        {
            if (_isRefreshing || _isClosing)
                return;

            _isRefreshing = true;
            try
            {
                var currentOutputDevice = _devicesViewModel.CurrentOutputDevice;
                DeviceText.Text = currentOutputDevice?.FullName ?? "No output device selected";

                if (currentOutputDevice == null)
                {
                    _sessions.Clear();
                    EmptyStateText.Visibility = Visibility.Visible;
                    return;
                }

                var snapshots = await Task.Run(() => _devicesViewModel.GetCurrentOutputSessions());
                if (_isClosing)
                    return;

                var sessionIds = snapshots.Select(s => s.SessionId).ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var stale in _sessions.Where(s => !sessionIds.Contains(s.SessionId)).ToList())
                    _sessions.Remove(stale);

                foreach (var snapshot in snapshots)
                {
                    var existing = _sessions.FirstOrDefault(s => string.Equals(s.SessionId, snapshot.SessionId, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        _sessions.Add(new AppVolumeSessionViewModel(
                            snapshot,
                            _devicesViewModel.SetOutputSessionVolume,
                            _devicesViewModel.SetOutputSessionMute));
                    }
                    else
                    {
                        existing.ApplySnapshot(snapshot);
                    }
                }

                var ordered = _sessions.OrderBy(s => s.AppName, StringComparer.OrdinalIgnoreCase).ToList();
                _sessions.Clear();
                foreach (var session in ordered)
                    _sessions.Add(session);

                EmptyStateText.Visibility = _sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        public void BeginClose()
        {
            if (_isClosing)
                return;

            _isClosing = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (IsLoaded)
                    Close();
            }));
        }

        public void ShowCenteredOnCurrentScreen()
        {
            var cursor = Forms.Cursor.Position;
            var screen = Forms.Screen.FromPoint(cursor);
            var area = screen.WorkingArea;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = area.Left + Math.Max(0, (area.Width - Width) / 2.0);
            Top = area.Top + Math.Max(0, (area.Height - Height) / 2.0);
        }

        private void AppVolumeMixerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshSystemVolumeUi();
        }

        private void DevicesViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DevicesViewModel.OutputVolume))
            {
                Dispatcher.BeginInvoke(new Action(RefreshSystemVolumeUi));
                return;
            }

            if (e.PropertyName == nameof(DevicesViewModel.CurrentOutputDevice))
            {
                Dispatcher.BeginInvoke(new Action(RefreshSystemVolumeUi));
            }
        }

        private void VolumeSlider_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var slider = sender as System.Windows.Controls.Slider
                ?? FindAncestor<System.Windows.Controls.Slider>(e.OriginalSource as DependencyObject)
                ?? FindDescendant<System.Windows.Controls.Slider>(sender as DependencyObject);

            if (slider == null)
                return;

            double step = 2;
            double newValue = slider.Value + (e.Delta > 0 ? step : -step);
            slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, newValue));
            e.Handled = true;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            DragMove();
        }

        private void RefreshSystemVolumeUi()
        {
            if (!IsLoaded)
                return;

            SystemOutputText.Text = _devicesViewModel.CurrentOutputDevice?.FullName ?? "No output device selected";

            var volume = Math.Max(0, Math.Min(100, _devicesViewModel.OutputVolume));
            if (Math.Abs(SystemVolumeSlider.Value - volume) > 0.01)
                SystemVolumeSlider.Value = volume;

            SystemVolumeTextBlock.Text = $"{Math.Round(volume)}%";
        }

        private void SystemVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded)
                return;

            var value = Math.Max(0, Math.Min(100, e.NewValue));
            if (Math.Abs(_devicesViewModel.OutputVolume - value) > 0.01)
                _devicesViewModel.OutputVolume = value;

            SystemVolumeTextBlock.Text = $"{Math.Round(value)}%";
        }

        private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
        {
            while (element != null)
            {
                if (element is T match)
                    return match;

                element = VisualTreeHelper.GetParent(element);
            }

            return null;
        }

        private static T? FindDescendant<T>(DependencyObject? root) where T : DependencyObject
        {
            if (root == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                    return match;

                var nested = FindDescendant<T>(child);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => BeginClose();
    }
}
