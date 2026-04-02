using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Input;
using EchoX.Models;
using EchoX.Services;
using Microsoft.Win32;

namespace EchoX.ViewModels
{
    public enum UpdatePreference { AutoInstall, NotifyOnly, NoUpdates }
    public enum NotificationType { PopupScreen, SoundOnly, WindowsNotification, None }

    public class SettingsViewModel : ViewModelBase
    {
        private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupApprovedRunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string AppName = "EchoX";
        private static readonly byte[] StartupApprovedEnabledValue = { 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        private bool _launchWithWindows;
        private UpdatePreference _updatePreference = UpdatePreference.NotifyOnly;
        private NotificationType _notificationType = NotificationType.PopupScreen;
        private bool _showMuteIndicator = true;
        private bool _mutePopupSound;
        private readonly StorageService? _storageService;

        public SettingsViewModel(StorageService? storageService = null)
        {
            _storageService = storageService;

            if (_storageService != null)
            {
                var settings = _storageService.LoadAppSettings();
                _notificationType = settings.NotificationType;
                _showMuteIndicator = settings.ShowMuteIndicator;
                _mutePopupSound = settings.MutePopupSound;
            }

            _launchWithWindows = GetStartupStatus();
            SetNotifyPopupCommand = new RelayCommand(() => NotifyPopupScreen = true);
            SetNotifySoundCommand = new RelayCommand(() => NotifySoundOnly = true);
            SetNotifyWindowsCommand = new RelayCommand(() => NotifyWindows = true);
            SetNotifyNoneCommand = new RelayCommand(() => NotifyNone = true);
        }

        public ICommand SetNotifyPopupCommand { get; }
        public ICommand SetNotifySoundCommand { get; }
        public ICommand SetNotifyWindowsCommand { get; }
        public ICommand SetNotifyNoneCommand { get; }

        public bool LaunchWithWindows
        {
            get => _launchWithWindows;
            set
            {
                if (SetProperty(ref _launchWithWindows, value))
                    SetStartup(value);
            }
        }

        public UpdatePreference UpdatePreference
        {
            get => _updatePreference;
            set => SetProperty(ref _updatePreference, value);
        }

        public bool UpdateAutoInstall
        {
            get => _updatePreference == UpdatePreference.AutoInstall;
            set
            {
                if (!value) return;
                UpdatePreference = UpdatePreference.AutoInstall;
                OnPropertyChanged(nameof(UpdateNotifyOnly));
                OnPropertyChanged(nameof(UpdateNoUpdates));
            }
        }

        public bool UpdateNotifyOnly
        {
            get => _updatePreference == UpdatePreference.NotifyOnly;
            set
            {
                if (!value) return;
                UpdatePreference = UpdatePreference.NotifyOnly;
                OnPropertyChanged(nameof(UpdateAutoInstall));
                OnPropertyChanged(nameof(UpdateNoUpdates));
            }
        }

        public bool UpdateNoUpdates
        {
            get => _updatePreference == UpdatePreference.NoUpdates;
            set
            {
                if (!value) return;
                UpdatePreference = UpdatePreference.NoUpdates;
                OnPropertyChanged(nameof(UpdateAutoInstall));
                OnPropertyChanged(nameof(UpdateNotifyOnly));
            }
        }

        public NotificationType NotificationType
        {
            get => _notificationType;
            set
            {
                if (SetProperty(ref _notificationType, value))
                    SaveSettings();
            }
        }

        public bool NotifyPopupScreen
        {
            get => _notificationType == NotificationType.PopupScreen;
            set
            {
                if (!value) return;
                NotificationType = NotificationType.PopupScreen;
                OnPropertyChanged(nameof(NotifySoundOnly));
                OnPropertyChanged(nameof(NotifyWindows));
                OnPropertyChanged(nameof(NotifyNone));
            }
        }

        public bool NotifySoundOnly
        {
            get => _notificationType == NotificationType.SoundOnly;
            set
            {
                if (!value) return;
                NotificationType = NotificationType.SoundOnly;
                OnPropertyChanged(nameof(NotifyPopupScreen));
                OnPropertyChanged(nameof(NotifyWindows));
                OnPropertyChanged(nameof(NotifyNone));
            }
        }

        public bool NotifyWindows
        {
            get => _notificationType == NotificationType.WindowsNotification;
            set
            {
                if (!value) return;
                NotificationType = NotificationType.WindowsNotification;
                OnPropertyChanged(nameof(NotifyPopupScreen));
                OnPropertyChanged(nameof(NotifySoundOnly));
                OnPropertyChanged(nameof(NotifyNone));
            }
        }

        public bool NotifyNone
        {
            get => _notificationType == NotificationType.None;
            set
            {
                if (!value) return;
                NotificationType = NotificationType.None;
                OnPropertyChanged(nameof(NotifyPopupScreen));
                OnPropertyChanged(nameof(NotifySoundOnly));
                OnPropertyChanged(nameof(NotifyWindows));
            }
        }

        public bool ShowMuteIndicator
        {
            get => _showMuteIndicator;
            set
            {
                if (SetProperty(ref _showMuteIndicator, value))
                    SaveSettings();
            }
        }

        public bool MutePopupSound
        {
            get => _mutePopupSound;
            set
            {
                if (SetProperty(ref _mutePopupSound, value))
                    SaveSettings();
            }
        }

        public AppSettings GetAppSettingsSnapshot()
        {
            var settings = _storageService?.LoadAppSettings() ?? new AppSettings();
            settings.NotificationType = _notificationType;
            settings.ShowMuteIndicator = _showMuteIndicator;
            settings.MutePopupSound = _mutePopupSound;
            settings.OverlayPlacements ??= new List<OverlayPlacement>();
            return settings;
        }

        public void ApplyOverlaySettings(AppSettings settings)
        {
            if (_storageService == null)
                return;

            settings.NotificationType = _notificationType;
            settings.ShowMuteIndicator = _showMuteIndicator;
            settings.MutePopupSound = _mutePopupSound;
            settings.OverlayPlacements ??= new List<OverlayPlacement>();
            _storageService.SaveAppSettings(settings);
        }

        private void SaveSettings()
        {
            if (_storageService == null)
                return;

            var settings = _storageService.LoadAppSettings();
            settings.NotificationType = _notificationType;
            settings.ShowMuteIndicator = _showMuteIndicator;
            settings.MutePopupSound = _mutePopupSound;
            settings.OverlayPlacements ??= new List<OverlayPlacement>();
            _storageService.SaveAppSettings(settings);
        }

        private bool GetStartupStatus()
        {
            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                if (runKey?.GetValue(AppName) == null)
                    return false;

                using var approvedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedRunKeyPath, false);
                if (approvedKey?.GetValue(AppName) is not byte[] approvedValue || approvedValue.Length == 0)
                    return true;

                return approvedValue[0] == 0x02;
            }
            catch
            {
                return false;
            }
        }

        private void SetStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
                using var approvedKey = Registry.CurrentUser.CreateSubKey(StartupApprovedRunKeyPath);
                if (enable)
                {
                    key?.SetValue(AppName, $"\"{Assembly.GetExecutingAssembly().Location}\" --tray");
                    approvedKey?.SetValue(AppName, StartupApprovedEnabledValue, RegistryValueKind.Binary);
                }
                else
                {
                    key?.DeleteValue(AppName, false);
                    approvedKey?.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set startup: {ex.Message}");
            }
        }
    }
}
