using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using EchoX.Models;
using EchoX.Services;
using EchoX.ViewModels;

namespace EchoX.ViewModels
{
    public class ProfilesViewModel : ViewModelBase
    {
        private readonly AudioEngine _audioEngine;
        private readonly StorageService _storageService;
        private readonly MainWindowViewModel _mainWindowViewModel;
        private int _currentProfileIndex = -1;

        public ProfilesViewModel(MainWindowViewModel mainWindowViewModel, AudioEngine audioEngine, StorageService storageService)
        {
            _mainWindowViewModel = mainWindowViewModel;
            _audioEngine = audioEngine;
            _storageService = storageService;

            SaveCommand = new RelayCommand(SaveProfile);
            ActivateCommand = new RelayCommand<AudioProfile>(ActivateProfile);
            EditCommand = new RelayCommand<AudioProfile>(EditProfile);
            DeleteCommand = new RelayCommand<AudioProfile>(DeleteProfile);

            // Step 1: Load cached data immediately
            LoadFromCache();

            // Step 2: Refresh asynchronously
            System.Threading.Tasks.Task.Run(() => InitialLoad());
        }

        private void LoadFromCache()
        {
            var profiles = _storageService.LoadProfiles();
            var cache = _storageService.LoadDeviceCache();

            foreach (var p in profiles) Profiles.Add(p);
            foreach (var d in cache.InputDevices) InputDevices.Add(d);
            foreach (var d in cache.OutputDevices) OutputDevices.Add(d);
            foreach (var d in InputDevices.Concat(OutputDevices)) AllDevices.Add(d);
        }

        private void InitialLoad()
        {
            try
            {
                var profiles = _storageService.LoadProfiles();
                var mics = _audioEngine.GetMicrophones().Select(m => new AudioDevice(m)).ToList();
                var speakers = _audioEngine.GetSpeakers().Select(s => new AudioDevice(s)).ToList();

                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Profiles
                    Profiles.Clear();
                    foreach (var p in profiles) Profiles.Add(p);

                    // Devices
                    InputDevices.Clear();
                    foreach (var m in mics) InputDevices.Add(m);

                    OutputDevices.Clear();
                    foreach (var s in speakers) OutputDevices.Add(s);

                    AllDevices.Clear();
                    foreach (var d in InputDevices.Concat(OutputDevices))
                        AllDevices.Add(d);
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ProfilesViewModel InitialLoad: {ex.Message}");
            }
        }

        private string _profileName = string.Empty;
        public string ProfileName
        {
            get => _profileName;
            set => SetProperty(ref _profileName, value);
        }

        private AudioDevice? _selectedInputDevice;
        public AudioDevice? SelectedInputDevice
        {
            get => _selectedInputDevice;
            set => SetProperty(ref _selectedInputDevice, value);
        }

        private AudioDevice? _selectedOutputDevice;
        public AudioDevice? SelectedOutputDevice
        {
            get => _selectedOutputDevice;
            set => SetProperty(ref _selectedOutputDevice, value);
        }

        private AudioDevice? _selectedCallDevice;
        public AudioDevice? SelectedCallDevice
        {
            get => _selectedCallDevice;
            set => SetProperty(ref _selectedCallDevice, value);
        }

        public ObservableCollection<AudioDevice> InputDevices  { get; } = new ObservableCollection<AudioDevice>();
        public ObservableCollection<AudioDevice> OutputDevices { get; } = new ObservableCollection<AudioDevice>();
        public ObservableCollection<AudioDevice> AllDevices    { get; } = new ObservableCollection<AudioDevice>();
        public ObservableCollection<AudioProfile> Profiles     { get; } = new ObservableCollection<AudioProfile>();

        public ICommand SaveCommand { get; }
        public ICommand ActivateCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }

        private void SaveProfile()
        {
            if (string.IsNullOrWhiteSpace(ProfileName) || SelectedInputDevice == null || SelectedOutputDevice == null)
                return;

            var profile = new AudioProfile
            {
                Name            = ProfileName,
                InputDeviceId   = SelectedInputDevice.Id,
                OutputDeviceId  = SelectedOutputDevice.Id,
                CallDeviceId    = SelectedCallDevice?.Id
            };

            var profiles = _storageService.LoadProfiles();
            profiles.Add(profile);
            _storageService.SaveProfiles(profiles);

            ProfileName          = string.Empty;
            SelectedInputDevice  = null;
            SelectedOutputDevice = null;
            SelectedCallDevice   = null;
            
            InitialLoad(); // Re-sync
        }

        private void ActivateProfile(AudioProfile profile)
        {
            try
            {
                if (!string.IsNullOrEmpty(profile.InputDeviceId))
                    _audioEngine.SwitchDevice(profile.InputDeviceId!);
                if (!string.IsNullOrEmpty(profile.OutputDeviceId))
                    _audioEngine.SwitchDevice(profile.OutputDeviceId!);
                if (!string.IsNullOrEmpty(profile.CallDeviceId))
                    _audioEngine.SetAsCallDevice(profile.CallDeviceId!);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ActivateProfile failed: {ex.Message}");
            }
        }

        private void EditProfile(AudioProfile profile)
        {
            ProfileName          = profile.Name;
            SelectedInputDevice  = InputDevices.FirstOrDefault(d => d.Id == profile.InputDeviceId);
            SelectedOutputDevice = OutputDevices.FirstOrDefault(d => d.Id == profile.OutputDeviceId);
            SelectedCallDevice   = AllDevices.FirstOrDefault(d => d.Id == profile.CallDeviceId);
        }

        private void DeleteProfile(AudioProfile profile)
        {
            var profiles = _storageService.LoadProfiles();
            profiles.RemoveAll(p => p.Id == profile.Id);
            _storageService.SaveProfiles(profiles);
            InitialLoad();
        }

        public void CycleProfiles()
        {
            if (Profiles.Count == 0) return;

            _currentProfileIndex++;
            if (_currentProfileIndex >= Profiles.Count)
                _currentProfileIndex = 0;

            var nextProfile = Profiles[_currentProfileIndex];
            ActivateProfile(nextProfile);

            _mainWindowViewModel.NotifyTray("Profile Switched", $"Activated: {nextProfile.Name}");
        }

        public void ToggleMicMute()
        {
            bool isNowMuted = _audioEngine.ToggleMuteDefaultMic();
            _mainWindowViewModel.NotifyTray("Microphone", isNowMuted ? "Muted" : "Unmuted");
        }
    }
}