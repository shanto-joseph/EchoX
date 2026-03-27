using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EchoX.Models
{
    public class AudioProfile : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _name = "New Profile";
        private string? _outputDeviceId;
        private string? _inputDeviceId;
        private string? _callInputDeviceId;
        private string? _callOutputDeviceId;
        private int _volumeLevel = 100;
        private bool _isActive;

        public string Id 
        { 
            get => _id; 
            set { if (_id != value) { _id = value; OnPropertyChanged(); } }
        }

        public string Name 
        { 
            get => _name; 
            set { if (_name != value) { _name = value; OnPropertyChanged(); } }
        }
        
        public string? OutputDeviceId 
        { 
            get => _outputDeviceId; 
            set { if (_outputDeviceId != value) { _outputDeviceId = value; OnPropertyChanged(); } }
        }

        public string? InputDeviceId 
        { 
            get => _inputDeviceId; 
            set { if (_inputDeviceId != value) { _inputDeviceId = value; OnPropertyChanged(); } }
        }

        public string? CallInputDeviceId 
        { 
            get => _callInputDeviceId; 
            set { if (_callInputDeviceId != value) { _callInputDeviceId = value; OnPropertyChanged(); } }
        }

        public string? CallOutputDeviceId 
        { 
            get => _callOutputDeviceId; 
            set { if (_callOutputDeviceId != value) { _callOutputDeviceId = value; OnPropertyChanged(); } }
        }

        public int VolumeLevel 
        { 
            get => _volumeLevel; 
            set { if (_volumeLevel != value) { _volumeLevel = value; OnPropertyChanged(); } }
        }

        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive != value) { _isActive = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}