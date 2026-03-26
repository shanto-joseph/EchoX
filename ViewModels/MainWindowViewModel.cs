using System;
using System.Windows.Input;
using EchoX.Services;
using EchoX.ViewModels;

namespace EchoX.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly AudioEngine _audioEngine;
        private readonly StorageService _storageService;

        // Commands for window actions
        public ICommand? MinimizeCommand { get; }
        public ICommand? CloseCommand { get; }
        public ICommand? ShowCommand { get; }

        // Event for showing tray notifications
        public event Action<string, string>? ShowTrayNotification;

        // Tab ViewModels
        public ProfilesViewModel ProfilesViewModel { get; }
        public DevicesViewModel DevicesViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }
        public AboutViewModel AboutViewModel { get; }

        public MainWindowViewModel()
        {
            // Create shared services once
            _audioEngine = new AudioEngine();
            _storageService = new StorageService();

            // Pass shared services to child ViewModels
            ProfilesViewModel = new ProfilesViewModel(this, _audioEngine, _storageService);
            DevicesViewModel = new DevicesViewModel(_audioEngine, _storageService);
            SettingsViewModel = new SettingsViewModel();
            AboutViewModel = new AboutViewModel();
        }

        public void NotifyTray(string title, string message)
        {
            ShowTrayNotification?.Invoke(title, message);
        }
    }
}