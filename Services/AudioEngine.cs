using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using AudioSwitcher.AudioApi.CoreAudio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace EchoX.Services
{
    public class AudioEngine
    {
        private readonly Lazy<CoreAudioController> _controller =
            new Lazy<CoreAudioController>(() => new CoreAudioController());
        private SoundPlayer? _testTonePlayer;
        private DateTime _lastPlayTime = DateTime.MinValue;

        private WasapiCapture? _micCapture;
        private WasapiOut? _micOut;
        private BufferedWaveProvider? _micWaveProvider;

        public AudioEngine()
        {
            // Pre-load your custom audio.wav from the Assets folder so it's instant-fire
            string wavPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "audio.wav");
            if (System.IO.File.Exists(wavPath))
            {
                _testTonePlayer = new SoundPlayer(wavPath);
                try { _testTonePlayer.LoadAsync(); } catch { }
            }
        }

        // 1. Fetch all active Output Devices (Speakers/Headphones)
        public List<CoreAudioDevice> GetSpeakers() => _controller.Value.GetPlaybackDevices(AudioSwitcher.AudioApi.DeviceState.Active).ToList();

        // 2. Fetch all active Input Devices (Microphones)
        public List<CoreAudioDevice> GetMicrophones() => _controller.Value.GetCaptureDevices(AudioSwitcher.AudioApi.DeviceState.Active).ToList();

        public CoreAudioDevice? GetDefaultSpeaker() => _controller.Value.DefaultPlaybackDevice;
        public CoreAudioDevice? GetDefaultMicrophone() => _controller.Value.DefaultCaptureDevice;
        public CoreAudioDevice? GetDefaultCommunicationsSpeaker() => _controller.Value.DefaultPlaybackCommunicationsDevice;
        public CoreAudioDevice? GetDefaultCommunicationsMicrophone() => _controller.Value.DefaultCaptureCommunicationsDevice;

        public CoreAudioDevice? GetDeviceByGuid(Guid guid) => _controller.Value.GetDevice(guid);

        public void SwitchDevice(string deviceId)
        {
            if (!Guid.TryParse(deviceId, out var guid)) return;
            var device = _controller.Value.GetDevice(guid);
            if (device != null)
            {
                device.SetAsDefault();
                // device.SetAsDefaultCommunications(); // Removed to decouple from communication settings
            }
        }

        public void SetAsCallDevice(string deviceId)
        {
            if (!Guid.TryParse(deviceId, out var guid)) return;
            var device = _controller.Value.GetDevice(guid);
            device?.SetAsDefaultCommunications();
        }

        public double GetVolume(string deviceId)
        {
            if (!Guid.TryParse(deviceId, out var guid)) return 0;
            return _controller.Value.GetDevice(guid)?.Volume ?? 0;
        }

        public void SetVolume(string deviceId, double volume)
        {
            if (!Guid.TryParse(deviceId, out var guid)) return;
            var device = _controller.Value.GetDevice(guid);
            if (device != null) device.Volume = volume;
        }

        public IDisposable WatchVolume(string deviceId, Action<double> onChanged)
        {
            if (!Guid.TryParse(deviceId, out var guid)) return new NoopDisposable();
            var device = _controller.Value.GetDevice(guid);
            if (device == null) return new NoopDisposable();
            return device.VolumeChanged.Subscribe(new VolumeObserver(onChanged));
        }

        private class VolumeObserver : IObserver<AudioSwitcher.AudioApi.DeviceVolumeChangedArgs>
        {
            private readonly Action<double> _callback;
            public VolumeObserver(Action<double> callback) => _callback = callback;
            public void OnNext(AudioSwitcher.AudioApi.DeviceVolumeChangedArgs args) =>
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() => _callback(args.Volume)));
            public void OnError(Exception error) { }
            public void OnCompleted() { }
        }

        public IDisposable WatchPeak(string deviceId, Action<double> onChanged)
        {
            if (!Guid.TryParse(deviceId, out var guid)) return new NoopDisposable();
            var device = _controller.Value.GetDevice(guid);
            if (device == null) return new NoopDisposable();
            return device.PeakValueChanged.Subscribe(new PeakObserver(onChanged));
        }

        private class PeakObserver : IObserver<AudioSwitcher.AudioApi.DevicePeakValueChangedArgs>
        {
            private readonly Action<double> _callback;
            public PeakObserver(Action<double> callback) => _callback = callback;
            public void OnNext(AudioSwitcher.AudioApi.DevicePeakValueChangedArgs args) =>
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() => _callback(args.PeakValue)));
            public void OnError(Exception error) { }
            public void OnCompleted() { }
        }

        public IDisposable WatchDevices(Action onChanged)
        {
            return _controller.Value.AudioDeviceChanged.Subscribe(new DeviceObserver(onChanged));
        }

        private class DeviceObserver : IObserver<AudioSwitcher.AudioApi.DeviceChangedArgs>
        {
            private readonly Action _callback;
            public DeviceObserver(Action callback) => _callback = callback;
            public void OnNext(AudioSwitcher.AudioApi.DeviceChangedArgs args) =>
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() => _callback()));
            public void OnError(Exception error) { }
            public void OnCompleted() { }
        }

        private class NoopDisposable : IDisposable { public void Dispose() { } }

        public bool ToggleMuteDefaultMic()
        {
            var mic = _controller.Value.DefaultCaptureDevice;
            if (mic != null)
            {
                mic.Mute(!mic.IsMuted);
                return mic.IsMuted;
            }
            return false;
        }

        public bool IsDefaultMicMuted => _controller.Value.DefaultCaptureDevice?.IsMuted ?? false;

        public void StartMicMonitor(string deviceId)
        {
            try
            {
                StopMicMonitor();
                
                var enumerator = new MMDeviceEnumerator();
                var mic = enumerator.GetDevice(deviceId);
                
                _micCapture = new WasapiCapture(mic, true, 20); // 20ms latency
                _micWaveProvider = new BufferedWaveProvider(_micCapture.WaveFormat)
                {
                    DiscardOnBufferOverflow = true
                };

                _micCapture.DataAvailable += (s, e) =>
                    _micWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);

                _micOut = new WasapiOut(AudioClientShareMode.Shared, 20);
                _micOut.Init(_micWaveProvider);
                
                _micCapture.StartRecording();
                // User requested to remove playback feedback (pf sound) from mic test
                // _micOut.Play(); 
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Mic Monitor Failed: {ex.Message}");
            }
        }

        public void StopMicMonitor()
        {
            _micCapture?.StopRecording();
            _micCapture?.Dispose();
            _micCapture = null;

            _micOut?.Stop();
            _micOut?.Dispose();
            _micOut = null;
            
            _micWaveProvider = null;
        }

        public void PlayTestTone(string deviceId)
        {
            if ((DateTime.Now - _lastPlayTime).TotalMilliseconds < 50) return;
            _lastPlayTime = DateTime.Now;

            try
            {
                if (_testTonePlayer != null)
                {
                    _testTonePlayer.Play(); 
                }
                else
                {
                    SystemSounds.Asterisk.Play();
                }
            }
            catch
            {
                SystemSounds.Beep.Play();
            }
        }
    }
}