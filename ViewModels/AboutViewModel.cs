using System;
using System.Windows.Input;
using EchoX.Services;

namespace EchoX.ViewModels
{
    public class AboutViewModel : ViewModelBase
    {
        public string AppVersion => "1.0.0";

        private string _lastChecked = "Never";
        public string LastChecked
        {
            get => _lastChecked;
            set => SetProperty(ref _lastChecked, value);
        }

        public ICommand CheckForUpdateCommand { get; }

        private readonly StorageService? _storageService;

        public AboutViewModel(StorageService? storageService = null)
        {
            _storageService = storageService;

            if (_storageService != null)
                _lastChecked = _storageService.LoadAppSettings().LastUpdateChecked;

            CheckForUpdateCommand = new RelayCommand(() =>
            {
                LastChecked = DateTime.Now.ToString("MMM d, yyyy  h:mm tt");
                if (_storageService != null)
                {
                    var s = _storageService.LoadAppSettings();
                    s.LastUpdateChecked = LastChecked;
                    _storageService.SaveAppSettings(s);
                }
            });
        }
    }
}
