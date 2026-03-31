using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using EchoX.Models;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi.Session;
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

        private IWaveIn? _micCapture;
        private IWavePlayer? _micOut;
        private BufferedWaveProvider? _micWaveProvider;
        public event Action<double>? MicTestPeakUpdated;

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

        public List<AppAudioSessionInfo> GetOutputSessions(string deviceId)
        {
            if (!Guid.TryParse(deviceId, out var guid))
                return new List<AppAudioSessionInfo>();

            var device = _controller.Value.GetDevice(guid);
            if (device == null)
                return new List<AppAudioSessionInfo>();

            try
            {
                return device.SessionController.ActiveSessions()
                    .Select(session => new AppAudioSessionInfo
                    {
                        SessionId = session.Id ?? string.Empty,
                        DeviceId = deviceId,
                        DisplayName = ResolveSessionDisplayName(session),
                        ExecutablePath = session.ExecutablePath ?? string.Empty,
                        IconPath = session.IconPath ?? string.Empty,
                        ProcessId = session.ProcessId,
                        Volume = session.Volume,
                        IsMuted = session.IsMuted,
                        IsSystemSession = session.IsSystemSession
                    })
                    .Where(session => !string.IsNullOrWhiteSpace(session.SessionId))
                    .OrderBy(session => session.DisplayName)
                    .ToList();
            }
            catch
            {
                return new List<AppAudioSessionInfo>();
            }
        }

        public void SetOutputSessionVolume(string deviceId, string sessionId, double volume)
        {
            var session = GetSession(deviceId, sessionId);
            if (session == null)
                return;

            try
            {
                session.Volume = volume;
            }
            catch
            {
            }
        }

        public void SetOutputSessionMute(string deviceId, string sessionId, bool isMuted)
        {
            var session = GetSession(deviceId, sessionId);
            if (session == null)
                return;

            try
            {
                session.IsMuted = isMuted;
            }
            catch
            {
            }
        }

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

        private IAudioSession? GetSession(string deviceId, string sessionId)
        {
            if (!Guid.TryParse(deviceId, out var guid))
                return null;

            var device = _controller.Value.GetDevice(guid);
            if (device == null)
                return null;

            try
            {
                return device.SessionController.All()
                    .FirstOrDefault(session => string.Equals(session.Id, sessionId, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return null;
            }
        }

        private static string ResolveSessionDisplayName(IAudioSession session)
        {
            if (!string.IsNullOrWhiteSpace(session.DisplayName))
                return session.DisplayName;

            if (session.IsSystemSession)
                return "System Sounds";

            string? executableName = null;
            string? bestWindowTitle = null;
            try
            {
                if (session.ProcessId > 0)
                {
                    var process = Process.GetProcessById(session.ProcessId);
                    bestWindowTitle = NormalizeWindowTitle(process.MainWindowTitle);

                    if (string.IsNullOrWhiteSpace(bestWindowTitle))
                    {
                        bestWindowTitle = Process.GetProcessesByName(process.ProcessName)
                            .Select(p =>
                            {
                                try { return NormalizeWindowTitle(p.MainWindowTitle); }
                                catch { return string.Empty; }
                            })
                            .FirstOrDefault(title => !string.IsNullOrWhiteSpace(title));
                    }
                }
            }
            catch
            {
            }

            if (!string.IsNullOrWhiteSpace(bestWindowTitle))
                return bestWindowTitle!;

            try
            {
                if (!string.IsNullOrWhiteSpace(session.ExecutablePath))
                {
                    executableName = System.IO.Path.GetFileNameWithoutExtension(session.ExecutablePath);
                    var versionInfo = FileVersionInfo.GetVersionInfo(session.ExecutablePath);
                    if (!string.IsNullOrWhiteSpace(versionInfo.FileDescription))
                        return HumanizeAppName(versionInfo.FileDescription);

                    if (!string.IsNullOrWhiteSpace(versionInfo.ProductName))
                        return HumanizeAppName(versionInfo.ProductName);
                }
            }
            catch
            {
            }

            try
            {
                if (session.ProcessId > 0)
                {
                    var process = Process.GetProcessById(session.ProcessId);
                    var processName = HumanizeAppName(process.ProcessName ?? string.Empty);
                    return processName;
                }
            }
            catch
            {
            }

            if (!string.IsNullOrWhiteSpace(executableName))
                return HumanizeAppName(executableName!);

            return "Unknown App";
        }

        private static string HumanizeAppName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return "Unknown App";

            var withSpaces = Regex.Replace(rawName, "([a-z0-9])([A-Z])", "$1 $2");
            withSpaces = Regex.Replace(withSpaces, "([A-Z])([A-Z][a-z])", "$1 $2");
            withSpaces = withSpaces.Replace("_", " ").Replace("-", " ").Trim();
            return string.IsNullOrWhiteSpace(withSpaces) ? rawName : withSpaces;
        }

        private static string NormalizeWindowTitle(string rawTitle)
        {
            if (string.IsNullOrWhiteSpace(rawTitle))
                return string.Empty;

            var title = rawTitle.Trim();
            if (title.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                title = System.IO.Path.GetFileNameWithoutExtension(title);

            var separators = new[] { " - ", " | ", " — " };
            foreach (var separator in separators)
            {
                var pieces = title.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
                if (pieces.Length > 1 && pieces[0].Length >= 3)
                    return pieces[0].Trim();
            }

            return title;
        }

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

        public IDisposable WatchMute(Action<bool> onMuteChanged)
        {
            return _controller.Value.DefaultCaptureDevice?
                .MuteChanged.Subscribe(new MuteObserver(onMuteChanged))
                ?? new NoopDisposable();
        }

        public IDisposable WatchMute(string deviceId, Action<bool> onMuteChanged)
        {
            if (!Guid.TryParse(deviceId, out var guid)) return new NoopDisposable();
            var device = _controller.Value.GetDevice(guid);
            if (device == null) return new NoopDisposable();
            return device.MuteChanged.Subscribe(new MuteObserver(onMuteChanged));
        }

        public IDisposable WatchDefaultDeviceChanged(Action onChanged)
        {
            return _controller.Value.AudioDeviceChanged.Subscribe(new DefaultDeviceObserver(onChanged));
        }

        private class MuteObserver : IObserver<AudioSwitcher.AudioApi.DeviceMuteChangedArgs>
        {
            private readonly Action<bool> _callback;
            public MuteObserver(Action<bool> callback) => _callback = callback;
            public void OnNext(AudioSwitcher.AudioApi.DeviceMuteChangedArgs args) =>
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() => _callback(args.IsMuted)));
            public void OnError(Exception error) { }
            public void OnCompleted() { }
        }

        private class DefaultDeviceObserver : IObserver<AudioSwitcher.AudioApi.DeviceChangedArgs>
        {
            private readonly Action _callback;
            public DefaultDeviceObserver(Action callback) => _callback = callback;
            public void OnNext(AudioSwitcher.AudioApi.DeviceChangedArgs args)
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() => _callback()));
            }
            public void OnError(Exception error) { }
            public void OnCompleted() { }
        }

        public void StartMicTest(string deviceId, bool loopback)
        {
            try
            {
                StopMicTest();

                // 1. Identify hardware by name matching
                int deviceIndex = -1;
                string deviceName = "";
                if (!string.IsNullOrEmpty(deviceId) && Guid.TryParse(deviceId, out var guid))
                {
                   var dev = _controller.Value.GetDevice(guid);
                   if (dev != null) deviceName = dev.Name;
                }

                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var caps = WaveIn.GetCapabilities(i);
                    // Match by first 15 chars to account for NAudio's truncation (31 char limit)
                    string searchName = caps.ProductName.Trim();
                    
                    //  Windows often append " (Microphone)" or similar, so check if names overlap significantly
                    if (!string.IsNullOrEmpty(deviceName) && 
                        (deviceName.Contains(searchName) || searchName.Contains(deviceName.Substring(0, Math.Min(deviceName.Length, 15)))))
                    {
                        deviceIndex = i;
                        break;
                    }
                }
                
                if (deviceIndex == -1) deviceIndex = 0; // Fallback to first

                // 2. Initialize Standard WaveIn
                _micCapture = new WaveInEvent
                {
                    DeviceNumber = deviceIndex,
                    WaveFormat = new WaveFormat(44100, 16, 1), // Safe 16-bit Mono format
                    BufferMilliseconds = 50 // Low latency capture
                };

                // 3. Initialize Provider
                _micWaveProvider = new BufferedWaveProvider(_micCapture.WaveFormat)
                {
                    DiscardOnBufferOverflow = true
                };

                // 4. Initialize Playback
                if (loopback) 
                {
                    var waveOut = new WaveOutEvent { DesiredLatency = 50 }; // Lowered from 100 to 50 for better responsiveness
                    waveOut.Init(_micWaveProvider);
                    waveOut.Play();
                    _micOut = waveOut;
                }

                // 5. Data Handler
                _micCapture.DataAvailable += (s, e) =>
                {
                    if (_micWaveProvider != null)
                    {
                        if (loopback) _micWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
                        
                        double peak = CalculatePeak(e.Buffer, e.BytesRecorded);
                        MicTestPeakUpdated?.Invoke(peak);
                    }
                };

                _micCapture.StartRecording();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Mic Test Fail: {ex.Message}");
            }
        }

        public void StopMicTest()
        {
            try
            {
                if (_micOut != null)
                {
                    _micOut.Stop();
                    _micOut.Dispose();
                    _micOut = null;
                }
                if (_micCapture != null)
                {
                    _micCapture.StopRecording();
                    _micCapture.Dispose();
                    _micCapture = null;
                }
                _micWaveProvider = null;
            }
            catch { }
        }

        private double CalculatePeak(byte[] buffer, int length)
        {
            if (_micCapture == null) return 0;
            
            float max = 0;
            var format = _micCapture.WaveFormat;

            try 
            {
                // High-performance direct buffer reading based on bit-depth
                if (format.BitsPerSample == 32)
                {
                    // Most 32-bit devices map to IEEE Float (even in Extensible format)
                    for (int i = 0; i < length; i += 4)
                    {
                        if (i + 4 <= length)
                        {
                            float sample = BitConverter.ToSingle(buffer, i);
                            float abs = Math.Abs(sample);
                            if (abs > max) max = abs;
                        }
                    }
                }
                else if (format.BitsPerSample == 16)
                {
                    // Standard PCM
                    for (int i = 0; i < length; i += 2)
                    {
                        if (i + 2 <= length)
                        {
                            short sample = BitConverter.ToInt16(buffer, i);
                            float sample32 = sample / 32768f;
                            float abs = Math.Abs(sample32);
                            if (abs > max) max = abs;
                        }
                    }
                }
                else if (format.BitsPerSample == 24)
                {
                    // 24-bit PCM (3 bytes per sample)
                    for (int i = 0; i < length; i += 3)
                    {
                        if (i + 3 <= length)
                        {
                            int sample = (buffer[i+2] << 16) | (buffer[i+1] << 8) | buffer[i];
                            // Handle signed 24-bit
                            if ((sample & 0x800000) != 0) sample |= unchecked((int)0xff000000);
                            float sample32 = sample / 8388608f;
                            float abs = Math.Abs(sample32);
                            if (abs > max) max = abs;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error in CalculatePeak: {ex.Message}");
            }
            
            return Math.Min(1.0, (double)max);
        }

        public void PlayTestTone(string deviceId)
        {
            if ((DateTime.Now - _lastPlayTime).TotalMilliseconds < 50) return;
            _lastPlayTime = DateTime.Now;

            try
            {
                if (_testTonePlayer != null)
                    _testTonePlayer.Play();
                else
                    SystemSounds.Asterisk.Play();
            }
            catch { }
        }

        public void PlayNotificationSound()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    string wavPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "audio.wav");
                    if (System.IO.File.Exists(wavPath))
                    {
                        using var reader = new AudioFileReader(wavPath);
                        using var output = new WaveOutEvent { DesiredLatency = 100 };
                        output.Init(reader);
                        output.Play();
                        while (output.PlaybackState == PlaybackState.Playing)
                            System.Threading.Thread.Sleep(10);
                    }
                    else
                    {
                        SystemSounds.Asterisk.Play();
                    }
                }
                catch { SystemSounds.Asterisk.Play(); }
            });
        }
    }
}
