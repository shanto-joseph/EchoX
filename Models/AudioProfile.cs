using System;
using System.ComponentModel;
using System.Linq;
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
        private string? _shortcutGesture;

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

        /// <summary>Full shortcut gesture, e.g. "Control+Alt+K" or "D0+D9".</summary>
        public string? ShortcutGesture
        {
            get => _shortcutGesture;
            set { if (_shortcutGesture != value) { _shortcutGesture = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShortcutDisplay)); } }
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

                var shortcutMouseButton = _shortcutMouseButton;
                if (!string.IsNullOrEmpty(shortcutMouseButton))
                {
                    return shortcutMouseButton switch
                    {
                        "XButton1" => "Mouse Button 4",
                        "XButton2" => "Mouse Button 5",
                        _ => shortcutMouseButton!
                    };
                }

                var shortcutGesture = _shortcutGesture;
                if (!string.IsNullOrWhiteSpace(shortcutGesture))
                    return FormatGestureForDisplay(shortcutGesture!);

                var shortcutKey = _shortcutKey;
                if (string.IsNullOrEmpty(shortcutKey))
                    return "No shortcut";

                var parts = new System.Collections.Generic.List<string>();
                var shortcutModifiers = _shortcutModifiers;
                if (!string.IsNullOrEmpty(shortcutModifiers))
                {
                    if (shortcutModifiers!.Contains("Control")) parts.Add("Ctrl");
                    if (shortcutModifiers.Contains("Alt"))      parts.Add("Alt");
                    if (shortcutModifiers.Contains("Shift"))    parts.Add("Shift");
                    if (shortcutModifiers.Contains("Windows"))  parts.Add("Win");
                }
                var key = shortcutKey!;
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

        private static string FormatGestureForDisplay(string gesture)
        {
            if (string.IsNullOrWhiteSpace(gesture))
                return "No shortcut";

            var parts = gesture
                .Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .Select(FormatGesturePart)
                .ToArray();

            return parts.Length == 0 ? "No shortcut" : string.Join(" + ", parts);
        }

        private static string FormatGesturePart(string token)
        {
            return token switch
            {
                "Control" => "Ctrl",
                "Windows" => "Win",
                "OemMinus" => "-",
                "OemPlus" => "=",
                "OemComma" => ",",
                "OemPeriod" => ".",
                "Add" => "+",
                "Subtract" => "-",
                "Multiply" => "*",
                "Divide" => "/",
                _ when token.StartsWith("D") && token.Length == 2 && char.IsDigit(token[1]) => token[1].ToString(),
                _ when token.StartsWith("NumPad", StringComparison.Ordinal) => "Num" + token.Substring(6),
                _ => token
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
