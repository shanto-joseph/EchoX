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
        private string? _shortcutKey;

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

        /// <summary>Shortcut key, e.g. "D1", "F1". Null = no shortcut.</summary>
        public string? ShortcutKey
        {
            get => _shortcutKey;
            set { if (_shortcutKey != value) { _shortcutKey = value; OnPropertyChanged(); } }
        }

        private string? _shortcutModifiers;
        /// <summary>Modifier flags as string, e.g. "Control,Alt"</summary>
        public string? ShortcutModifiers
        {
            get => _shortcutModifiers;
            set { if (_shortcutModifiers != value) { _shortcutModifiers = value; OnPropertyChanged(); } }
        }

        private string? _shortcutMouseButton;
        /// <summary>"XButton1" or "XButton2" if shortcut is a mouse button</summary>
        public string? ShortcutMouseButton
        {
            get => _shortcutMouseButton;
            set { if (_shortcutMouseButton != value) { _shortcutMouseButton = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}