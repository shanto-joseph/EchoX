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
        private IDisposable? _deviceWatcher;

        public ProfilesViewModel(MainWindowViewModel mainWindowViewModel, AudioEngine audioEngine, StorageService storageService)
        {
            _mainWindowViewModel = mainWindowViewModel;
            _audioEngine = audioEngine;
            _storageService = storageService;

            SaveCommand = new RelayCommand(SaveProfile);
            CancelEditCommand = new RelayCommand(CancelEdit);
            ActivateCommand = new RelayCommand<AudioProfile>(ActivateProfile);
            EditCommand = new RelayCommand<AudioProfile>(EditProfile);
            DeleteCommand = new RelayCommand<AudioProfile>(DeleteProfile);
            NewCommand = new RelayCommand(() => {
                CancelEdit();
                IsEditorVisible = true;
            });

            // Step 1: Load cached data immediately
            LoadFromCache();

            // Step 2: Refresh asynchronously
            System.Threading.Tasks.Task.Run(() => InitialLoad());

            // Step 3: Watch for new devices (USB plug/unplug) in realtime
            _deviceWatcher = _audioEngine.WatchDevices(() => {
                System.Threading.Tasks.Task.Run(() => InitialLoad());
            });

            Profiles.CollectionChanged += (s, e) => {
                OnPropertyChanged(nameof(FilteredProfiles));
                OnPropertyChanged(nameof(IsEmpty));
            };
        }

        private void LoadFromCache()
        {
            var profiles = _storageService.LoadProfiles();
            Profiles.Clear();
            foreach (var p in profiles) Profiles.Add(p);
            OnPropertyChanged(nameof(FilteredProfiles));
            OnPropertyChanged(nameof(IsEmpty));
        }

        private void InitialLoad()
        {
            try
            {
                var profiles = _storageService.LoadProfiles();
                var activeId = _storageService.LoadActiveProfileId();

                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var currentIds = profiles.Select(p => p.Id).ToList();
                    var existingProfiles = Profiles.ToList();

                    // Remove gone
                    foreach (var existing in existingProfiles)
                        if (!currentIds.Contains(existing.Id)) Profiles.Remove(existing);

                    // Add new OR Update existing
                    foreach (var profile in profiles)
                    {
                        var match = Profiles.FirstOrDefault(p => p.Id == profile.Id);
                        if (match == null)
                        {
                            Profiles.Add(profile);
                        }
                        else
                        {
                            // Sync properties on existing object to keep UI bindings alive
                            match.Name = profile.Name;
                            match.InputDeviceId = profile.InputDeviceId;
                            match.OutputDeviceId = profile.OutputDeviceId;
                            match.CallInputDeviceId = profile.CallInputDeviceId;
                            match.CallOutputDeviceId = profile.CallOutputDeviceId;
                        }
                    }

                    if (!string.IsNullOrEmpty(activeId))
                    {
                        ActiveProfile = Profiles.FirstOrDefault(p => p.Id == activeId);
                    }
                    
                    OnPropertyChanged(nameof(FilteredProfiles));
                    OnPropertyChanged(nameof(IsEmpty));
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ProfilesViewModel InitialLoad: {ex.Message}");
            }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    OnPropertyChanged(nameof(FilteredProfiles));
                    OnPropertyChanged(nameof(IsEmpty));
                }
            }
        }

        public System.Collections.Generic.IEnumerable<AudioProfile> FilteredProfiles
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SearchText)) return Profiles;
                return Profiles.Where(p => p.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);
            }
        }

        public bool IsEmpty => !FilteredProfiles.Any();

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

        private AudioDevice? _selectedCallInputDevice;
        public AudioDevice? SelectedCallInputDevice
        {
            get => _selectedCallInputDevice;
            set => SetProperty(ref _selectedCallInputDevice, value);
        }

        private AudioDevice? _selectedCallOutputDevice;
        public AudioDevice? SelectedCallOutputDevice
        {
            get => _selectedCallOutputDevice;
            set => SetProperty(ref _selectedCallOutputDevice, value);
        }

        public ObservableCollection<AudioDevice> InputDevices  => _mainWindowViewModel.DevicesViewModel.InputDevices;
        public ObservableCollection<AudioDevice> OutputDevices => _mainWindowViewModel.DevicesViewModel.OutputDevices;
        public ObservableCollection<AudioProfile> Profiles     { get; } = new ObservableCollection<AudioProfile>();

        public ICommand SaveCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand ActivateCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }

        private AudioProfile? _activeProfile;
        public AudioProfile? ActiveProfile
        {
            get => _activeProfile;
            set
            {
                if (_activeProfile != null) _activeProfile.IsActive = false;
                if (SetProperty(ref _activeProfile, value))
                {
                    // Update the visual flag on all objects (or just the new one if we're careful)
                    foreach (var p in Profiles)
                    {
                        p.IsActive = (p.Id == _activeProfile?.Id);
                    }
                }
            }
        }

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (SetProperty(ref _isEditing, value))
                    OnPropertyChanged(nameof(SaveButtonText));
            }
        }

        public string SaveButtonText => IsEditing ? "Update" : "Save";

        private bool _isEditorVisible;
        public bool IsEditorVisible
        {
            get => _isEditorVisible;
            set => SetProperty(ref _isEditorVisible, value);
        }

        public ICommand NewCommand { get; }

        private void SaveProfile()
        {
            if (string.IsNullOrWhiteSpace(ProfileName) || SelectedInputDevice == null || SelectedOutputDevice == null)
                return;

            var profiles = _storageService.LoadProfiles();

            // Validation: Duplicate name check (excluding the one being edited)
            if (profiles.Any(p => p.Name.Equals(ProfileName, StringComparison.OrdinalIgnoreCase) && 
                (!IsEditing || p.Id != Profiles.ElementAtOrDefault(_currentProfileIndex)?.Id)))
            {
                // In a real app, show a message box. For now, we'll just suffix it.
                ProfileName += " (Copy)";
            }

            var profile = new AudioProfile
            {
                Name               = ProfileName,
                InputDeviceId      = SelectedInputDevice.Id,
                OutputDeviceId     = SelectedOutputDevice.Id,
                CallInputDeviceId  = SelectedCallInputDevice?.Id,
                CallOutputDeviceId = SelectedCallOutputDevice?.Id
            };
            
            AudioProfile? savedProfile = null;
            if (IsEditing && _currentProfileIndex >= 0 && _currentProfileIndex < Profiles.Count)
            {
                var existing = Profiles[_currentProfileIndex];
                existing.Name = ProfileName;
                existing.InputDeviceId = SelectedInputDevice.Id;
                existing.OutputDeviceId = SelectedOutputDevice.Id;
                existing.CallInputDeviceId = SelectedCallInputDevice?.Id;
                existing.CallOutputDeviceId = SelectedCallOutputDevice?.Id;

                int storageIndex = profiles.FindIndex(p => p.Id == existing.Id);
                if (storageIndex >= 0) profiles[storageIndex] = existing;
                savedProfile = existing;
            }
            else
            {
                profiles.Add(profile);
                savedProfile = profile;
            }

            _storageService.SaveProfiles(profiles);
            
            // Auto-sync hardware if this was the active profile
            if (ActiveProfile != null && savedProfile != null && ActiveProfile.Id == savedProfile.Id)
            {
                ActivateProfile(savedProfile);
            }

            CancelEdit();
            IsEditorVisible = false;
            
            // Re-sync but don't clear full list
            InitialLoad();
        }

        private void CancelEdit()
        {
            ProfileName = string.Empty;
            SelectedInputDevice = null;
            SelectedOutputDevice = null;
            SelectedCallInputDevice = null;
            SelectedCallOutputDevice = null;
            IsEditing = false;
            IsEditorVisible = false;
            _currentProfileIndex = -1;
        }

        private void EditProfile(AudioProfile profile)
        {
            IsEditing = true;
            IsEditorVisible = true;
            ProfileName = profile.Name;
            _currentProfileIndex = Profiles.IndexOf(profile);
            
            SelectedInputDevice = InputDevices.FirstOrDefault(d => d.Id == profile.InputDeviceId);
            SelectedOutputDevice = OutputDevices.FirstOrDefault(d => d.Id == profile.OutputDeviceId);
            SelectedCallInputDevice = InputDevices.FirstOrDefault(d => d.Id == profile.CallInputDeviceId);
            SelectedCallOutputDevice = OutputDevices.FirstOrDefault(d => d.Id == profile.CallOutputDeviceId);
        }

        private void ActivateProfile(AudioProfile profile)
        {
            if (profile == null) return;
            try
            {
                ActiveProfile = profile;
                _storageService.SaveActiveProfileId(profile.Id);
                var devVm = _mainWindowViewModel.DevicesViewModel;

                // 1. Primary Devices (Setter handles hardware switch)
                if (!string.IsNullOrEmpty(profile.InputDeviceId))
                {
                    var match = devVm.InputDevices.FirstOrDefault(d => d.Id == profile.InputDeviceId);
                    if (match != null) devVm.CurrentInputDevice = match;
                }

                if (!string.IsNullOrEmpty(profile.OutputDeviceId))
                {
                    var match = devVm.OutputDevices.FirstOrDefault(d => d.Id == profile.OutputDeviceId);
                    if (match != null) devVm.CurrentOutputDevice = match;
                }

                // 2. Communication Devices (Command handles hardware switch)
                if (!string.IsNullOrEmpty(profile.CallInputDeviceId))
                {
                    var match = devVm.InputDevices.FirstOrDefault(d => d.Id == profile.CallInputDeviceId);
                    if (match != null) devVm.SetCallInputCommand.Execute(match);
                }

                if (!string.IsNullOrEmpty(profile.CallOutputDeviceId))
                {
                    var match = devVm.OutputDevices.FirstOrDefault(d => d.Id == profile.CallOutputDeviceId);
                    if (match != null) devVm.SetCallOutputCommand.Execute(match);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ActivateProfile failed: {ex.Message}");
            }
        }


        private void DeleteProfile(AudioProfile profile)
        {
            // If the deleted profile was being edited, clear the form
            if (IsEditing && _currentProfileIndex >= 0 && _currentProfileIndex < Profiles.Count)
            {
                if (Profiles[_currentProfileIndex].Id == profile.Id)
                    CancelEdit();
            }

            var profiles = _storageService.LoadProfiles();
            profiles.RemoveAll(p => p.Id == profile.Id);
            _storageService.SaveProfiles(profiles);

            // If deleted the active one, clear active
            if (ActiveProfile?.Id == profile.Id)
            {
                ActiveProfile = null;
                _storageService.SaveActiveProfileId("");
            }

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
            
            // Invoke the event for MainWindow tray icon/GUI
            _mainWindowViewModel.OnMicrophoneMuteChanged(isNowMuted);
            
            // Notify via tray balloon
            _mainWindowViewModel.NotifyTray("Microphone", isNowMuted ? "Muted" : "Unmuted");

            // Update UI list for devices
            _mainWindowViewModel.DevicesViewModel.UpdateDeviceMuteStates();
        }
    }
}