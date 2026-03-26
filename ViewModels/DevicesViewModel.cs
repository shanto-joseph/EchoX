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

        private AudioDevice? _currentCallInputDevice;
        private AudioDevice? _currentCallOutputDevice;
        private IDisposable? _inputVolumeWatcher;
        private IDisposable? _outputVolumeWatcher;

        public DevicesViewModel(AudioEngine audioEngine, StorageService storageService)
        {
            _audioEngine = audioEngine;
            _storageService = storageService;

            TestOutputCommand    = new RelayCommand<AudioDevice>(d => _audioEngine.PlayTestTone(d.Id));
            SwitchInputCommand   = new RelayCommand<AudioDevice>(SwitchDevice);
            SwitchOutputCommand  = new RelayCommand<AudioDevice>(SwitchDevice);
            SetCallInputCommand  = new RelayCommand<AudioDevice>(SetCallInput);
            SetCallOutputCommand = new RelayCommand<AudioDevice>(SetCallOutput);

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
        }

        public ObservableCollection<AudioDevice> InputDevices  { get; } = new ObservableCollection<AudioDevice>();
        public ObservableCollection<AudioDevice> OutputDevices { get; } = new ObservableCollection<AudioDevice>();

        private AudioDevice? _currentInputDevice;
        public AudioDevice? CurrentInputDevice
        {
            get => _currentInputDevice;
            set
            {
                if (SetProperty(ref _currentInputDevice, value) && value != null)
                {
                    _audioEngine.SwitchDevice(value.Id);
                    UpdateVolumeAndWatchers(value, true);
                    RefreshActiveProfile();
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
                    _audioEngine.SwitchDevice(value.Id);
                    UpdateVolumeAndWatchers(value, false);
                    RefreshActiveProfile();
                }
            }
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

                    _currentInputDevice = defaultMic != null
                        ? InputDevices.FirstOrDefault(d => d.Id == defaultMic.Id.ToString()) ?? InputDevices.FirstOrDefault()
                        : InputDevices.FirstOrDefault();

                    _currentOutputDevice = defaultSpeaker != null
                        ? OutputDevices.FirstOrDefault(d => d.Id == defaultSpeaker.Id.ToString()) ?? OutputDevices.FirstOrDefault()
                        : OutputDevices.FirstOrDefault();

                    _currentCallInputDevice = callMic != null
                        ? InputDevices.FirstOrDefault(d => d.Id == callMic.Id.ToString()) ?? InputDevices.FirstOrDefault()
                        : InputDevices.FirstOrDefault();

                    _currentCallOutputDevice = callSpeaker != null
                        ? OutputDevices.FirstOrDefault(d => d.Id == callSpeaker.Id.ToString()) ?? OutputDevices.FirstOrDefault()
                        : OutputDevices.FirstOrDefault();

                    if (_currentCallInputDevice != null) _currentCallInputDevice.IsCallDevice = true;
                    if (_currentCallOutputDevice != null) _currentCallOutputDevice.IsCallDevice = true;

                    // Read real volumes
                    _inputVolume = _currentInputDevice != null ? _audioEngine.GetVolume(_currentInputDevice.Id) : 100;
                    _outputVolume = _currentOutputDevice != null ? _audioEngine.GetVolume(_currentOutputDevice.Id) : 100;

                    SetupWatchers();

                    OnPropertyChanged(nameof(CurrentInputDevice));
                    OnPropertyChanged(nameof(CurrentOutputDevice));
                    OnPropertyChanged(nameof(InputVolume));
                    OnPropertyChanged(nameof(OutputVolume));

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
            // Simple replacement for now, could be smarter diff
            collection.Clear();
            foreach (var item in newList) collection.Add(item);
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
            _audioEngine.SetAsCallDevice(device.Id);
            if (_currentCallInputDevice != null) _currentCallInputDevice.IsCallDevice = false;
            device.IsCallDevice = true;
            _currentCallInputDevice = device;
        }

        private void SetCallOutput(AudioDevice device)
        {
            _audioEngine.SetAsCallDevice(device.Id);
            if (_currentCallOutputDevice != null) _currentCallOutputDevice.IsCallDevice = false;
            device.IsCallDevice = true;
            _currentCallOutputDevice = device;
        }

        private void RefreshActiveProfile()
        {
            var profiles = _storageService.LoadProfiles();
            var match = profiles.FirstOrDefault(p =>
                p.InputDeviceId  == _currentInputDevice?.Id &&
                p.OutputDeviceId == _currentOutputDevice?.Id);
            ActiveProfileName = match?.Name;
        }

        private void SwitchDevice(AudioDevice device)
        {
            _audioEngine.SwitchDevice(device.Id);
            if (InputDevices.Contains(device))
                CurrentInputDevice = device;
            else if (OutputDevices.Contains(device))
                CurrentOutputDevice = device;
        }
    }
}
