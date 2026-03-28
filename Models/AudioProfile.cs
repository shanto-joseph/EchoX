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
            set { if (_shortcutKey != value) { _shortcutKey = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShortcutDisplay)); } }
        }

        private string? _shortcutModifiers;
        /// <summary>Modifier flags as string, e.g. "Control,Alt"</summary>
        public string? ShortcutModifiers
        {
            get => _shortcutModifiers;
            set { if (_shortcutModifiers != value) { _shortcutModifiers = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShortcutDisplay)); } }
        }

        private string? _shortcutMouseButton;
        /// <summary>"XButton1" or "XButton2" if shortcut is a mouse button</summary>
        public string? ShortcutMouseButton
        {
            get => _shortcutMouseButton;
            set { if (_shortcutMouseButton != value) { _shortcutMouseButton = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShortcutDisplay)); } }
        }

        /// <summary>Human-readable shortcut string for display, e.g. "Ctrl + Alt + 1"</summary>
        public string ShortcutDisplay
        {
            get
            {
                if (_isCapturingShortcut) return "Press keys…";

                if (!string.IsNullOrEmpty(_shortcutMouseButton))
                {
                    return _shortcutMouseButton switch
                    {
                        "XButton1" => "Mouse Button 4",
                        "XButton2" => "Mouse Button 5",
                        _ => _shortcutMouseButton
                    };
                }

                if (string.IsNullOrEmpty(_shortcutKey))
                    return "No shortcut";

                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(_shortcutModifiers))
                {
                    if (_shortcutModifiers.Contains("Control")) parts.Add("Ctrl");
                    if (_shortcutModifiers.Contains("Alt"))     parts.Add("Alt");
                    if (_shortcutModifiers.Contains("Shift"))   parts.Add("Shift");
                    if (_shortcutModifiers.Contains("Windows")) parts.Add("Win");
                }
                var key = _shortcutKey;
                if (key.StartsWith("D") && key.Length == 2 && char.IsDigit(key[1])) key = key[1].ToString();
                else if (key.StartsWith("NumPad")) key = "Num" + key.Substring(6);
                parts.Add(key);
                return string.Join(" + ", parts);
            }
        }

        /// <summary>Transient — not serialized. True while the Key Binds tab is capturing a new shortcut for this profile.</summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool IsCapturingShortcut
        {
            get => _isCapturingShortcut;
            set { if (_isCapturingShortcut != value) { _isCapturingShortcut = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShortcutDisplay)); } }
        }
        private bool _isCapturingShortcut;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
