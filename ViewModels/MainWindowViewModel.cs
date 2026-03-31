using System;
using System.Windows.Input;
using EchoX.Services;
using EchoX.ViewModels;

namespace EchoX.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public AudioEngine AudioEngine { get; }
        private readonly StorageService _storageService;

        // Commands for window actions
        public ICommand? MinimizeCommand { get; }
        public ICommand? CloseCommand { get; }
        public ICommand? ShowCommand { get; }

        // Event for showing tray notifications
        public event Action<bool>? MicrophoneMuteChanged;

        // Tab ViewModels
        public ProfilesViewModel ProfilesViewModel { get; }
        public DevicesViewModel DevicesViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }
        public AboutViewModel AboutViewModel { get; }
        public KeyBindsViewModel KeyBindsViewModel { get; }

        public MainWindowViewModel()
        {
            // Create shared services once
            AudioEngine = new AudioEngine();
            _storageService = new StorageService();

            // Pass shared services to child ViewModels
            ProfilesViewModel  = new ProfilesViewModel(this, AudioEngine, _storageService);
            DevicesViewModel   = new DevicesViewModel(this, AudioEngine, _storageService);
            SettingsViewModel  = new SettingsViewModel(_storageService);
            AboutViewModel     = new AboutViewModel(_storageService);
            KeyBindsViewModel  = new KeyBindsViewModel(_storageService);

            _ = AboutViewModel.CheckForUpdatesAsync(true);
        }

        public void NotifyTray(string title, string message)
        {
            switch (SettingsViewModel.NotificationType)
            {
                case NotificationType.PopupScreen:
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var popup = new EchoX.NotificationPopup(title, message, SettingsViewModel.GetAppSettingsSnapshot());
                        popup.Show();
                    }));
                    if (!SettingsViewModel.MutePopupSound)
                        AudioEngine.PlayNotificationSound();
                    break;

                case NotificationType.SoundOnly:
                    AudioEngine.PlayNotificationSound();
                    break;

                case NotificationType.None:
                    // do nothing
                    break;

                case NotificationType.WindowsNotification:
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        new Microsoft.Toolkit.Uwp.Notifications.ToastContentBuilder()
                            .AddText(title)
                            .AddText(message)
                            .Show();
                    }));
                    break;
            }
        }

        public void OnMicrophoneMuteChanged(bool isMuted)
        {
            MicrophoneMuteChanged?.Invoke(isMuted);
        }
    }
}
