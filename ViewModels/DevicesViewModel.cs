using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using EchoX.Models;
using EchoX.Services;

namespace EchoX.ViewModels
{
    public class DevicesViewModel : ViewModelBase
    {
        private readonly AudioEngine _audioEngine;
        private readonly StorageService _storageService;
        private readonly MainWindowViewModel _mainWindowViewModel;

        private AudioDevice? _currentCallInputDevice;
        public AudioDevice? CurrentCallInputDevice
        {
            get => _currentCallInputDevice;
            private set => SetProperty(ref _currentCallInputDevice, value);
        }

        private AudioDevice? _currentCallOutputDevice;
        public AudioDevice? CurrentCallOutputDevice
        {
            get => _currentCallOutputDevice;
            private set => SetProperty(ref _currentCallOutputDevice, value);
        }
        private IDisposable? _inputVolumeWatcher;
        private IDisposable? _outputVolumeWatcher;
        private IDisposable? _deviceWatcher;
        private double _targetPeak;
        private double _currentPeak;
        private System.Windows.Threading.DispatcherTimer? _smoothingTimer;

        public DevicesViewModel(MainWindowViewModel mainWindowViewModel, AudioEngine audioEngine, StorageService storageService)
        {
            _mainWindowViewModel = mainWindowViewModel;
            _audioEngine = audioEngine;
            _storageService = storageService;

            TestOutputCommand    = new RelayCommand<AudioDevice>(d => _audioEngine.PlayTestTone(d.Id));
            SwitchInputCommand   = new RelayCommand<AudioDevice>(SwitchDevice);
            SwitchOutputCommand  = new RelayCommand<AudioDevice>(SwitchDevice);
            SetCallInputCommand  = new RelayCommand<AudioDevice>(SetCallInput);
            SetCallOutputCommand = new RelayCommand<AudioDevice>(SetCallOutput);
            ToggleMicTestCommand = new RelayCommand(() => IsMicTesting = !IsMicTesting);

            // Step 1: Load cache immediately for "instant" feel
            var cache = _storageService.LoadDeviceCache();
            if (cache.InputDevices.Any() || cache.OutputDevices.Any())
            {
                foreach (var d in cache.InputDevices) InputDevices.Add(d);
                foreach (var d in cache.OutputDevices) OutputDevices.Add(d);
                
                _currentInputDevice = InputDevices.FirstOrDefault(d => d.Id == cache.LastInputId) ?? InputDevices.FirstOrDefault();
                _currentOutputDevice = OutputDevices.FirstOrDefault(d => d.Id == cache.LastOutputId) ?? OutputDevices.FirstOrDefault();
                
                OnPropertyChanged(nameof(CurrentInputDevice));
                OnPropertyChanged(nameof(CurrentOutputDevice));
            }

            // Step 2: Refresh the list in the background
            System.Threading.Tasks.Task.Run(() => LoadDevices());

            // Step 3: Watch for new devices (USB plug/unplug) in realtime
            _deviceWatcher = _audioEngine.WatchDevices(() => {
                System.Threading.Tasks.Task.Run(() => LoadDevices());
            });
        }

        public ObservableCollection<AudioDevice> InputDevices  { get; } = new ObservableCollection<AudioDevice>();
        public ObservableCollection<AudioDevice> OutputDevices { get; } = new ObservableCollection<AudioDevice>();
        public ObservableCollection<AudioDevice> AllDevices    { get; } = new ObservableCollection<AudioDevice>();

        internal int _pendingSwitches = 0;

        private AudioDevice? _currentInputDevice;
        public AudioDevice? CurrentInputDevice
        {
            get => _currentInputDevice;
            set
            {
                if (SetProperty(ref _currentInputDevice, value) && value != null)
                {
                    var captured = value;
                    bool fromProfile = _pendingSwitches > 0;
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        _audioEngine.SwitchDevice(captured.Id);
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            UpdateVolumeAndWatchers(captured, true);
                            UpdateDeviceMuteStates();
                            if (fromProfile)
                                CompleteProfileSwitchStep();
                            else
                                RefreshActiveProfile();
                            if (IsMicTesting) UpdateMicTest();
                        }));
                    });
                }
            }
        }

        private AudioDevice? _currentOutputDevice;
        public AudioDevice? CurrentOutputDevice
        {
            get => _currentOutputDevice;
            set
            {
                if (SetProperty(ref _currentOutputDevice, value) && value != null)
                {
                    var captured = value;
                    bool fromProfile = _pendingSwitches > 0;
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        _audioEngine.SwitchDevice(captured.Id);
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            UpdateVolumeAndWatchers(captured, false);
                            UpdateDeviceMuteStates();
                            if (fromProfile)
                                CompleteProfileSwitchStep();
                            else
                                RefreshActiveProfile();
                        }));
                    });
                }
            }
        }

        private void CompleteProfileSwitchStep()
        {
            if (_pendingSwitches <= 0)
                return;

            _pendingSwitches--;
            if (_pendingSwitches == 0)
                RefreshActiveProfile();
        }

        private void UpdateVolumeAndWatchers(AudioDevice device, bool isInput)
        {
            if (isInput) {
                _inputVolume = _audioEngine.GetVolume(device.Id);
                OnPropertyChanged(nameof(InputVolume));
                _inputVolumeWatcher?.Dispose();
                _inputVolumeWatcher = _audioEngine.WatchVolume(device.Id, v => {
                    _inputVolume = v;
                    OnPropertyChanged(nameof(InputVolume));
                });
            } else {
                _outputVolume = _audioEngine.GetVolume(device.Id);
                OnPropertyChanged(nameof(OutputVolume));
                _outputVolumeWatcher?.Dispose();
                _outputVolumeWatcher = _audioEngine.WatchVolume(device.Id, v => {
                    _outputVolume = v;
                    OnPropertyChanged(nameof(OutputVolume));
                });
            }
        }

        private string? _activeProfileName;
        public string? ActiveProfileName
        {
            get => _activeProfileName;
            private set => SetProperty(ref _activeProfileName, value);
        }

        private double _inputVolume = 100;
        public double InputVolume
        {
            get => _inputVolume;
            set
            {
                if (SetProperty(ref _inputVolume, value) && _currentInputDevice != null)
                    _audioEngine.SetVolume(_currentInputDevice.Id, value);
            }
        }

        private double _outputVolume = 100;
        public double OutputVolume
        {
            get => _outputVolume;
            set
            {
                if (SetProperty(ref _outputVolume, value) && _currentOutputDevice != null)
                    _audioEngine.SetVolume(_currentOutputDevice.Id, value);
            }
        }

        public ICommand TestOutputCommand    { get; }
        public ICommand SwitchInputCommand   { get; }
        public ICommand SwitchOutputCommand  { get; }
        public ICommand SetCallInputCommand  { get; }
        public ICommand SetCallOutputCommand { get; }
        public ICommand ToggleMicTestCommand { get; }

        private bool _isMicTestExpanded;
        public bool IsMicTestExpanded
        {
            get => _isMicTestExpanded;
            set => SetProperty(ref _isMicTestExpanded, value);
        }

        private bool _isMicTesting;
        public bool IsMicTesting
        {
            get => _isMicTesting;
            set { if (SetProperty(ref _isMicTesting, value)) UpdateMicTest(); }
        }

        private bool _isMicLoopback = true; // Hear yourself by default
        public bool IsMicLoopback
        {
            get => _isMicLoopback;
            set { if (SetProperty(ref _isMicLoopback, value)) { if (IsMicTesting) UpdateMicTest(); } }
        }

        private double _micTestLevel;
        public double MicTestLevel
        {
            get => _micTestLevel;
            set => SetProperty(ref _micTestLevel, value);
        }

        private void UpdateMicTest()
        {
            if (IsMicTesting && CurrentInputDevice != null)
            {
                _audioEngine.MicTestPeakUpdated -= OnMicTestPeakUpdated;
                _audioEngine.MicTestPeakUpdated += OnMicTestPeakUpdated;

                // Run the blocking audio driver init off the UI thread
                var deviceId = CurrentInputDevice.Id;
                var loopback = IsMicLoopback;
                System.Threading.Tasks.Task.Run(() => _audioEngine.StartMicTest(deviceId, loopback));

                // Initialize smoothing timer for "analog-like" meter
                _smoothingTimer?.Stop();
                _smoothingTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(16) // ~60fps
                };
                _smoothingTimer.Tick += (s, e) =>
                {
                    if (_targetPeak > _currentPeak)
                        _currentPeak = (_currentPeak * 0.4) + (_targetPeak * 0.6);
                    else
                        _currentPeak = (_currentPeak * 0.85);

                    MicTestLevel = Math.Min(100, Math.Pow(_currentPeak, 0.7) * 100.0);
                };
                _smoothingTimer.Start();
            }
            else
            {
                _smoothingTimer?.Stop();
                _audioEngine.MicTestPeakUpdated -= OnMicTestPeakUpdated;
                System.Threading.Tasks.Task.Run(() => _audioEngine.StopMicTest());
                MicTestLevel = 0;
                _targetPeak = 0;
                _currentPeak = 0;
            }
        }

        private void OnMicTestPeakUpdated(double peak)
        {
            _targetPeak = peak;
        }

        private void LoadDevices()
        {
            try
            {
                var mics = _audioEngine.GetMicrophones()
                                      .Select(m => new AudioDevice(m))
                                      .OrderBy(m => m.Name)
                                      .ToList();

                var speakers = _audioEngine.GetSpeakers()
                                          .Select(s => new AudioDevice(s))
                                          .OrderBy(s => s.Name)
                                          .ToList();

                var defaultMic     = _audioEngine.GetDefaultMicrophone();
                var defaultSpeaker = _audioEngine.GetDefaultSpeaker();
                var callMic        = _audioEngine.GetDefaultCommunicationsMicrophone();
                var callSpeaker    = _audioEngine.GetDefaultCommunicationsSpeaker();

                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Update only if necessary to avoid UI flicker
                    UpdateCollection(InputDevices, mics);
                    UpdateCollection(OutputDevices, speakers);

                    // Combine for AllDevices
                    var combined = mics.Concat(speakers).ToList();
                    UpdateCollection(AllDevices, combined);

                    // Only update current devices from OS if no profile switch is in progress
                    if (_pendingSwitches == 0)
                    {
                        // Use saved active profile's devices if available, else OS defaults
                        var activeProfileId = _storageService.LoadActiveProfileId();
                        AudioProfile? activeProfile = null;
                        if (!string.IsNullOrEmpty(activeProfileId))
                        {
                            var allProfiles = _storageService.LoadProfiles();
                            activeProfile = allProfiles.FirstOrDefault(p => p.Id == activeProfileId);
                        }

                        if (activeProfile != null)
                        {
                            _currentInputDevice  = InputDevices.FirstOrDefault(d => d.Id == activeProfile.InputDeviceId)
                                                ?? (defaultMic != null ? InputDevices.FirstOrDefault(d => d.Id == defaultMic.Id.ToString()) : null)
                                                ?? InputDevices.FirstOrDefault();
                            _currentOutputDevice = OutputDevices.FirstOrDefault(d => d.Id == activeProfile.OutputDeviceId)
                                                ?? (defaultSpeaker != null ? OutputDevices.FirstOrDefault(d => d.Id == defaultSpeaker.Id.ToString()) : null)
                                                ?? OutputDevices.FirstOrDefault();
                        }
                        else
                        {
                            _currentInputDevice  = defaultMic != null
                                ? InputDevices.FirstOrDefault(d => d.Id == defaultMic.Id.ToString()) ?? InputDevices.FirstOrDefault()
                                : InputDevices.FirstOrDefault();
                            _currentOutputDevice = defaultSpeaker != null
                                ? OutputDevices.FirstOrDefault(d => d.Id == defaultSpeaker.Id.ToString()) ?? OutputDevices.FirstOrDefault()
                                : OutputDevices.FirstOrDefault();
                        }
                    }
                    foreach (var d in InputDevices) d.IsCallDevice = false;
                    foreach (var d in OutputDevices) d.IsCallDevice = false;

                    CurrentCallInputDevice = callMic != null
                        ? InputDevices.FirstOrDefault(d => d.Id == callMic.Id.ToString()) ?? InputDevices.FirstOrDefault()
                        : InputDevices.FirstOrDefault();

                    CurrentCallOutputDevice = callSpeaker != null
                        ? OutputDevices.FirstOrDefault(d => d.Id == callSpeaker.Id.ToString()) ?? OutputDevices.FirstOrDefault()
                        : OutputDevices.FirstOrDefault();

                    if (CurrentCallInputDevice != null) CurrentCallInputDevice.IsCallDevice = true;
                    if (CurrentCallOutputDevice != null) CurrentCallOutputDevice.IsCallDevice = true;

                    // Read real volumes
                    _inputVolume = _currentInputDevice != null ? _audioEngine.GetVolume(_currentInputDevice.Id) : 100;
                    _outputVolume = _currentOutputDevice != null ? _audioEngine.GetVolume(_currentOutputDevice.Id) : 100;

                    SetupWatchers();

                    OnPropertyChanged(nameof(CurrentInputDevice));
                    OnPropertyChanged(nameof(CurrentOutputDevice));
                    OnPropertyChanged(nameof(InputVolume));
                    OnPropertyChanged(nameof(OutputVolume));

                    // Only refresh active profile from LoadDevices if no profile switch is pending
                    if (_pendingSwitches == 0)
                        RefreshActiveProfile();

                    // Step 3: Save results to cache for next run
                    _storageService.SaveDeviceCache(new DeviceCache {
                        InputDevices = mics,
                        OutputDevices = speakers,
                        LastInputId = _currentInputDevice?.Id,
                        LastOutputId = _currentOutputDevice?.Id
                    });
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading devices: {ex.Message}");
            }
        }

        private void UpdateCollection(ObservableCollection<AudioDevice> collection, List<AudioDevice> newList)
        {
            var oldList = collection.ToList();
            var newIds = newList.Select(d => d.Id).ToList();

            // Remove gone
            foreach (var old in oldList)
                if (!newIds.Contains(old.Id)) collection.Remove(old);

            // Add new or update existing
            foreach (var updated in newList)
            {
                var existing = collection.FirstOrDefault(d => d.Id == updated.Id);
                if (existing == null)
                {
                    collection.Add(updated);
                }
                else
                {
                    // Minimal updates to existing objects
                    existing.Name = updated.Name;
                    existing.IsDefault = updated.IsDefault;
                    // Note: Volume and Peak are handled by watchers
                }
            }
        }

        private void SetupWatchers()
        {
            _inputVolumeWatcher?.Dispose();
            if (_currentInputDevice != null)
                _inputVolumeWatcher = _audioEngine.WatchVolume(_currentInputDevice.Id, v => {
                    _inputVolume = v;
                    OnPropertyChanged(nameof(InputVolume));
                });

            _outputVolumeWatcher?.Dispose();
            if (_currentOutputDevice != null)
                _outputVolumeWatcher = _audioEngine.WatchVolume(_currentOutputDevice.Id, v => {
                    _outputVolume = v;
                    OnPropertyChanged(nameof(OutputVolume));
                });
        }

        private void SetCallInput(AudioDevice device)
        {
            System.Threading.Tasks.Task.Run(() => {
                _audioEngine.SetAsCallDevice(device.Id);
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() => {
                    foreach(var d in InputDevices) d.IsCallDevice = (d.Id == device.Id);
                    CurrentCallInputDevice = device;
                }));
            });
        }

        private void SetCallOutput(AudioDevice device)
        {
            System.Threading.Tasks.Task.Run(() => {
                _audioEngine.SetAsCallDevice(device.Id);
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() => {
                    foreach (var d in OutputDevices) d.IsCallDevice = (d.Id == device.Id);
                    CurrentCallOutputDevice = device;
                }));
            });
        }

        private void RefreshActiveProfile()
        {
            var profiles = _storageService.LoadProfiles();

            var match = profiles.FirstOrDefault(p =>
                p.InputDeviceId  == _currentInputDevice?.Id &&
                p.OutputDeviceId == _currentOutputDevice?.Id);

            if (match != null)
                _storageService.SaveActiveProfileId(match.Id);
            else
                _storageService.SaveActiveProfileId("");

            ActiveProfileName = match?.Name;

            var profVm = _mainWindowViewModel.ProfilesViewModel;
            profVm.ActiveProfile = match != null
                ? profVm.Profiles.FirstOrDefault(p => p.Id == match.Id)
                : null;
        }

        public void UpdateDeviceMuteStates()
        {
            // Refresh mute states of all input devices from hardware
            foreach (var device in InputDevices)
            {
                if (Guid.TryParse(device.Id, out var guid))
                {
                    try {
                        var realDevice = _audioEngine.GetDeviceByGuid(guid);
                        if (realDevice != null) device.IsMuted = realDevice.IsMuted;
                    } catch { }
                }
            }
        }

        private void SwitchDevice(AudioDevice device)
        {
            // The setter for CurrentInput/OutputDevice already triggers the background switch.
            // Calling _audioEngine.SwitchDevice(device.Id) here would create a duplicate task.
            if (InputDevices.Contains(device))
                CurrentInputDevice = device;
            else if (OutputDevices.Contains(device))
                CurrentOutputDevice = device;
        }
    }
}
