using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using AudioSwitcher.AudioApi.CoreAudio;

namespace EchoX.Services
{
    public class AudioEngine
    {
        private readonly Lazy<CoreAudioController> _lazyController = 
            new Lazy<CoreAudioController>(() => new CoreAudioController());

        private CoreAudioController _controller => _lazyController.Value;

        public AudioEngine()
        {
        }

        // 1. Fetch all active Output Devices (Speakers/Headphones)
        public List<CoreAudioDevice> GetSpeakers()
        {
            return _controller.GetPlaybackDevices(AudioSwitcher.AudioApi.DeviceState.Active).ToList();
        }

        // 2. Fetch all active Input Devices (Microphones)
        public List<CoreAudioDevice> GetMicrophones()
        {
            return _controller.GetCaptureDevices(AudioSwitcher.AudioApi.DeviceState.Active).ToList();
        }

        // 3. Get the current Windows default playback device
        public CoreAudioDevice? GetDefaultSpeaker() => _controller.DefaultPlaybackDevice;

        // 4. Get the current Windows default capture device
        public CoreAudioDevice? GetDefaultMicrophone() => _controller.DefaultCaptureDevice;

        // Get the current default communications playback device
        public CoreAudioDevice? GetDefaultCommunicationsSpeaker() => _controller.DefaultPlaybackCommunicationsDevice;

        // Get the current default communications capture device
        public CoreAudioDevice? GetDefaultCommunicationsMicrophone() => _controller.DefaultCaptureCommunicationsDevice;

        // 3. The Magic Function: Force Windows to switch devices
        public void SwitchDevice(string deviceId)
        {
            if (!Guid.TryParse(deviceId, out var guid)) return;
            var device = _controller.GetDevice(guid);
            if (device != null)
            {
                device.SetAsDefault();
                device.SetAsDefaultCommunications();
            }
        }

        // Set only as the default communications (call) device
        public void SetAsCallDevice(string deviceId)
        {
            if (!Guid.TryParse(deviceId, out var guid)) return;
            var device = _controller.GetDevice(guid);
            device?.SetAsDefaultCommunications();
        }

        // Get volume (0-100) for a device
        public double GetVolume(string deviceId)
        {
            if (!Guid.TryParse(deviceId, out var guid)) return 0;
            return _controller.GetDevice(guid)?.Volume ?? 0;
        }

        // Set volume (0-100) for a device
        public void SetVolume(string deviceId, double volume)
        {
            if (!Guid.TryParse(deviceId, out var guid)) return;
            var device = _controller.GetDevice(guid);
            if (device != null) device.Volume = volume;
        }

        // Subscribe to real-time volume changes for a device. Returns IDisposable to unsubscribe.
        public IDisposable WatchVolume(string deviceId, Action<double> onChanged)
        {
            if (!Guid.TryParse(deviceId, out var guid)) return new NoopDisposable();
            var device = _controller.GetDevice(guid);
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

        private class NoopDisposable : IDisposable { public void Dispose() { } }

        // 4. The Panic Button: Mute the current default microphone
        public bool ToggleMuteDefaultMic()
        {
            // Grab whichever mic is currently active in Windows
            var mic = _controller.DefaultCaptureDevice;
            
            if (mic != null)
            {
                // Flip the mute state (if unmuted, mute it. If muted, unmute it)
                mic.Mute(!mic.IsMuted);
                return mic.IsMuted; // Return the new state so we can notify the user
            }
            return false;
        }

        // Play a test tone through a specific output device
        public void PlayTestTone(string deviceId)
        {
            if (!Guid.TryParse(deviceId, out var guid)) return;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var target = _controller.GetDevice(guid);
                    if (target == null) return;

                    var previous = _controller.DefaultPlaybackDevice;
                    target.SetAsDefault();

                    var wavPath = System.IO.Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory, "Assets", "audio.wav");

                    if (System.IO.File.Exists(wavPath))
                    {
                        using var player = new System.Media.SoundPlayer(wavPath);
                        player.PlaySync(); // blocks until done
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(500);
                    }

                    if (previous != null && previous.Id != target.Id)
                        previous.SetAsDefault();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"PlayTestTone failed: {ex.Message}");
                }
            });
        }
    }
}