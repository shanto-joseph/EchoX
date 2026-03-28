using System;
using System.Reflection;
using System.Windows.Input;
using Microsoft.Win32;

namespace EchoX.ViewModels
{
    public enum UpdatePreference { AutoInstall, NotifyOnly, NoUpdates }
    public enum NotificationType  { PopupScreen, SoundOnly, WindowsNotification, None }

    public class SettingsViewModel : ViewModelBase
    {
        private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName    = "EchoX";

        private bool _launchWithWindows;
        private UpdatePreference  _updatePreference  = UpdatePreference.NotifyOnly;
        private NotificationType  _notificationType  = NotificationType.PopupScreen;
        private bool _showMuteIndicator = true;

        public SettingsViewModel()
        {
            _launchWithWindows = GetStartupStatus();
            SetNotifyPopupCommand   = new RelayCommand(() => NotifyPopupScreen = true);
            SetNotifySoundCommand   = new RelayCommand(() => NotifySoundOnly   = true);
            SetNotifyWindowsCommand = new RelayCommand(() => NotifyWindows     = true);
            SetNotifyNoneCommand    = new RelayCommand(() => NotifyNone        = true);
        }

        public ICommand SetNotifyPopupCommand   { get; }
        public ICommand SetNotifySoundCommand   { get; }
        public ICommand SetNotifyWindowsCommand { get; }
        public ICommand SetNotifyNoneCommand    { get; }

        // ── Startup ──
        public bool LaunchWithWindows
        {
            get => _launchWithWindows;
            set { if (SetProperty(ref _launchWithWindows, value)) SetStartup(value); }
        }

        // ── Updates ──
        public UpdatePreference UpdatePreference
        {
            get => _updatePreference;
            set => SetProperty(ref _updatePreference, value);
        }

        public bool UpdateAutoInstall
        {
            get => _updatePreference == UpdatePreference.AutoInstall;
            set { if (value) { UpdatePreference = UpdatePreference.AutoInstall; OnPropertyChanged(nameof(UpdateNotifyOnly)); OnPropertyChanged(nameof(UpdateNoUpdates)); } }
        }
        public bool UpdateNotifyOnly
        {
            get => _updatePreference == UpdatePreference.NotifyOnly;
            set { if (value) { UpdatePreference = UpdatePreference.NotifyOnly; OnPropertyChanged(nameof(UpdateAutoInstall)); OnPropertyChanged(nameof(UpdateNoUpdates)); } }
        }
        public bool UpdateNoUpdates
        {
            get => _updatePreference == UpdatePreference.NoUpdates;
            set { if (value) { UpdatePreference = UpdatePreference.NoUpdates; OnPropertyChanged(nameof(UpdateAutoInstall)); OnPropertyChanged(nameof(UpdateNotifyOnly)); } }
        }

        // ── Notifications ──
        public NotificationType NotificationType
        {
            get => _notificationType;
            set => SetProperty(ref _notificationType, value);
        }

        public bool NotifyPopupScreen
        {
            get => _notificationType == NotificationType.PopupScreen;
            set { if (value) { NotificationType = NotificationType.PopupScreen; OnPropertyChanged(nameof(NotifySoundOnly)); OnPropertyChanged(nameof(NotifyWindows)); OnPropertyChanged(nameof(NotifyNone)); } }
        }
        public bool NotifySoundOnly
        {
            get => _notificationType == NotificationType.SoundOnly;
            set { if (value) { NotificationType = NotificationType.SoundOnly; OnPropertyChanged(nameof(NotifyPopupScreen)); OnPropertyChanged(nameof(NotifyWindows)); OnPropertyChanged(nameof(NotifyNone)); } }
        }
        public bool NotifyWindows
        {
            get => _notificationType == NotificationType.WindowsNotification;
            set { if (value) { NotificationType = NotificationType.WindowsNotification; OnPropertyChanged(nameof(NotifyPopupScreen)); OnPropertyChanged(nameof(NotifySoundOnly)); OnPropertyChanged(nameof(NotifyNone)); } }
        }
        public bool NotifyNone
        {
            get => _notificationType == NotificationType.None;
            set { if (value) { NotificationType = NotificationType.None; OnPropertyChanged(nameof(NotifyPopupScreen)); OnPropertyChanged(nameof(NotifySoundOnly)); OnPropertyChanged(nameof(NotifyWindows)); } }
        }

        // ── Mute indicator ──
        public bool ShowMuteIndicator
        {
            get => _showMuteIndicator;
            set => SetProperty(ref _showMuteIndicator, value);
        }

        // ── Registry helpers ──
        private bool GetStartupStatus()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch { return false; }
        }

        private void SetStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (enable)
                    key?.SetValue(AppName, $"\"{Assembly.GetExecutingAssembly().Location}\"");
                else
                    key?.DeleteValue(AppName, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set startup: {ex.Message}");
            }
        }
    }
}
