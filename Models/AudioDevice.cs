using System.ComponentModel;
using System.Runtime.CompilerServices;
using AudioSwitcher.AudioApi.CoreAudio;

namespace EchoX.Models
{
    public class AudioDevice : INotifyPropertyChanged
    {
        public string Id { get; set; } = string.Empty;
        
        private string _name = string.Empty;
        public string Name 
        { 
            get => _name; 
            set { _name = value; OnPropertyChanged(); } 
        }

        private string _fullName = string.Empty;
        public string FullName 
        { 
            get => _fullName; 
            set { _fullName = value; OnPropertyChanged(); } 
        }

        private bool _isDefault;
        public bool IsDefault 
        { 
            get => _isDefault; 
            set { _isDefault = value; OnPropertyChanged(); } 
        }

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
