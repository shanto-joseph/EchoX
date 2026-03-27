using System.ComponentModel;
using System.Runtime.CompilerServices;
using AudioSwitcher.AudioApi.CoreAudio;

namespace EchoX.Models
{
    public class AudioDevice : INotifyPropertyChanged
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsDefault { get; set; }

        private bool _isCallDevice;
        public bool IsCallDevice
        {
            get => _isCallDevice;
            set { _isCallDevice = value; OnPropertyChanged(); }
        }

        private bool _isMuted;
        public bool IsMuted
        {
            get => _isMuted;
            set { _isMuted = value; OnPropertyChanged(); }
        }
        
        private double _peak;
        public double Peak
        {
            get => _peak;
            set { _peak = value; OnPropertyChanged(); }
        }

        public AudioDevice(CoreAudioDevice device)
        {
            Id = device.Id.ToString();
            Name = device.Name;
            FullName = device.FullName;
            IsDefault = device.IsDefaultDevice;
            IsMuted = device.IsMuted;
        }

        public AudioDevice() { }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
