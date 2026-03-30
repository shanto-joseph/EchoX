using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using EchoX.Services;

namespace EchoX.ViewModels
{
    public class KeyBindsViewModel : ViewModelBase
    {
        private readonly StorageService _storageService;

        private static readonly Key DefaultOpenAppKey = Key.X;
        private static readonly ModifierKeys DefaultOpenAppMods = ModifierKeys.Shift | ModifierKeys.Alt;
        private static readonly Key DefaultCycleKey = Key.S;
        private static readonly ModifierKeys DefaultCycleMods = ModifierKeys.Control | ModifierKeys.Alt;
        private static readonly Key DefaultMuteKey = Key.K;
        private static readonly ModifierKeys DefaultMuteMods = ModifierKeys.Control | ModifierKeys.Alt;
        private static readonly Key DefaultMixerKey = Key.V;
        private static readonly ModifierKeys DefaultMixerMods = ModifierKeys.Control | ModifierKeys.Alt;

        private Key _openAppKey = DefaultOpenAppKey;
        private ModifierKeys _openAppMods = DefaultOpenAppMods;
        private Key _cycleKey = DefaultCycleKey;
        private ModifierKeys _cycleMods = DefaultCycleMods;
        private Key _muteKey = DefaultMuteKey;
        private ModifierKeys _muteMods = DefaultMuteMods;
        private Key _mixerKey = DefaultMixerKey;
        private ModifierKeys _mixerMods = DefaultMixerMods;

        private string _openAppGesture = BuildGesture(DefaultOpenAppMods, DefaultOpenAppKey);
        private string _cycleGesture = BuildGesture(DefaultCycleMods, DefaultCycleKey);
        private string _muteGesture = BuildGesture(DefaultMuteMods, DefaultMuteKey);
        private string _mixerGesture = BuildGesture(DefaultMixerMods, DefaultMixerKey);

        private bool _isOpenAppEnabled = true;
        private bool _isCycleEnabled = true;
        private bool _isMuteEnabled = true;
        private bool _isMixerEnabled = true;

        private bool _isCapturingOpenApp;
        private bool _isCapturingCycle;
        private bool _isCapturingMute;
        private bool _isCapturingMixer;

        public KeyBindsViewModel(StorageService storageService)
        {
            _storageService = storageService;
            LoadSaved();

            StartCaptureOpenAppCommand = new RelayCommand(() => { IsCapturingOpenApp = true; IsCapturingCycle = false; IsCapturingMute = false; IsCapturingMixer = false; });
            StartCaptureCycleCommand = new RelayCommand(() => { IsCapturingCycle = true; IsCapturingMute = false; IsCapturingOpenApp = false; IsCapturingMixer = false; });
            StartCaptureMuteCommand = new RelayCommand(() => { IsCapturingMute = true; IsCapturingCycle = false; IsCapturingOpenApp = false; IsCapturingMixer = false; });
            StartCaptureMixerCommand = new RelayCommand(() => { IsCapturingMixer = true; IsCapturingMute = false; IsCapturingCycle = false; IsCapturingOpenApp = false; });

            ResetOpenAppCommand = new RelayCommand(() => ResetGesture(DefaultOpenAppKey, DefaultOpenAppMods, action: "open"));
            ResetCycleCommand = new RelayCommand(() => ResetGesture(DefaultCycleKey, DefaultCycleMods, action: "cycle"));
            ResetMuteCommand = new RelayCommand(() => ResetGesture(DefaultMuteKey, DefaultMuteMods, action: "mute"));
            ResetMixerCommand = new RelayCommand(() => ResetGesture(DefaultMixerKey, DefaultMixerMods, action: "mixer"));
        }

        public ICommand StartCaptureOpenAppCommand { get; }
        public ICommand StartCaptureCycleCommand { get; }
        public ICommand StartCaptureMuteCommand { get; }
        public ICommand StartCaptureMixerCommand { get; }
        public ICommand ResetOpenAppCommand { get; }
        public ICommand ResetCycleCommand { get; }
        public ICommand ResetMuteCommand { get; }
        public ICommand ResetMixerCommand { get; }

        public event Action? HotkeysChanged;

        public bool IsOpenAppEnabled
        {
            get => _isOpenAppEnabled;
            set { if (SetProperty(ref _isOpenAppEnabled, value)) { SaveAll(); RaiseRebind(); } }
        }

        public bool IsCycleEnabled
        {
            get => _isCycleEnabled;
            set { if (SetProperty(ref _isCycleEnabled, value)) { SaveAll(); RaiseRebind(); } }
        }

        public bool IsMuteEnabled
        {
            get => _isMuteEnabled;
            set { if (SetProperty(ref _isMuteEnabled, value)) { SaveAll(); RaiseRebind(); } }
        }

        public bool IsMixerEnabled
        {
            get => _isMixerEnabled;
            set { if (SetProperty(ref _isMixerEnabled, value)) { SaveAll(); RaiseRebind(); } }
        }

        public Key OpenAppKey
        {
            get => _openAppKey;
            set => SetProperty(ref _openAppKey, value);
        }

        public ModifierKeys OpenAppMods
        {
            get => _openAppMods;
            set => SetProperty(ref _openAppMods, value);
        }

        public Key CycleKey
        {
            get => _cycleKey;
            set => SetProperty(ref _cycleKey, value);
        }

        public ModifierKeys CycleMods
        {
            get => _cycleMods;
            set => SetProperty(ref _cycleMods, value);
        }

        public Key MuteKey
        {
            get => _muteKey;
            set => SetProperty(ref _muteKey, value);
        }

        public ModifierKeys MuteMods
        {
            get => _muteMods;
            set => SetProperty(ref _muteMods, value);
        }

        public Key MixerKey
        {
            get => _mixerKey;
            set => SetProperty(ref _mixerKey, value);
        }

        public ModifierKeys MixerMods
        {
            get => _mixerMods;
            set => SetProperty(ref _mixerMods, value);
        }

        public string OpenAppGesture
        {
            get => _openAppGesture;
            private set { if (SetProperty(ref _openAppGesture, value)) OnPropertyChanged(nameof(OpenAppDisplay)); }
        }

        public string CycleGesture
        {
            get => _cycleGesture;
            private set { if (SetProperty(ref _cycleGesture, value)) OnPropertyChanged(nameof(CycleDisplay)); }
        }

        public string MuteGesture
        {
            get => _muteGesture;
            private set { if (SetProperty(ref _muteGesture, value)) OnPropertyChanged(nameof(MuteDisplay)); }
        }

        public string MixerGesture
        {
            get => _mixerGesture;
            private set { if (SetProperty(ref _mixerGesture, value)) OnPropertyChanged(nameof(MixerDisplay)); }
        }

        public bool IsCapturingOpenApp
        {
            get => _isCapturingOpenApp;
            set { SetProperty(ref _isCapturingOpenApp, value); OnPropertyChanged(nameof(OpenAppDisplay)); }
        }

        public bool IsCapturingCycle
        {
            get => _isCapturingCycle;
            set { SetProperty(ref _isCapturingCycle, value); OnPropertyChanged(nameof(CycleDisplay)); }
        }

        public bool IsCapturingMute
        {
            get => _isCapturingMute;
            set { SetProperty(ref _isCapturingMute, value); OnPropertyChanged(nameof(MuteDisplay)); }
        }

        public bool IsCapturingMixer
        {
            get => _isCapturingMixer;
            set { SetProperty(ref _isCapturingMixer, value); OnPropertyChanged(nameof(MixerDisplay)); }
        }

        public string OpenAppDisplay => IsCapturingOpenApp ? "Press keys…" : FormatGesture(OpenAppGesture);
        public string CycleDisplay => IsCapturingCycle ? "Press keys…" : FormatGesture(CycleGesture);
        public string MuteDisplay => IsCapturingMute ? "Press keys…" : FormatGesture(MuteGesture);
        public string MixerDisplay => IsCapturingMixer ? "Press keys…" : FormatGesture(MixerGesture);

        public bool TryCaptureGesture(string gesture)
        {
            if (string.IsNullOrWhiteSpace(gesture))
                return false;

            gesture = NormalizeGesture(gesture);
            var (mods, key) = TryExtractSingleKeyGesture(gesture);

            if (IsCapturingOpenApp)
            {
                OpenAppGesture = gesture;
                OpenAppMods = mods;
                OpenAppKey = key;
                IsCapturingOpenApp = false;
                SaveAll();
                RaiseRebind();
                return true;
            }

            if (IsCapturingCycle)
            {
                CycleGesture = gesture;
                CycleMods = mods;
                CycleKey = key;
                IsCapturingCycle = false;
                SaveAll();
                RaiseRebind();
                return true;
            }

            if (IsCapturingMute)
            {
                MuteGesture = gesture;
                MuteMods = mods;
                MuteKey = key;
                IsCapturingMute = false;
                SaveAll();
                RaiseRebind();
                return true;
            }

            if (IsCapturingMixer)
            {
                MixerGesture = gesture;
                MixerMods = mods;
                MixerKey = key;
                IsCapturingMixer = false;
                SaveAll();
                RaiseRebind();
                return true;
            }

            return false;
        }

        public bool TryCapture(Key key, ModifierKeys mods)
        {
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
                return false;

            return TryCaptureGesture(BuildGesture(mods, key));
        }

        public void CancelCapture()
        {
            IsCapturingOpenApp = false;
            IsCapturingCycle = false;
            IsCapturingMute = false;
            IsCapturingMixer = false;
        }

        private void LoadSaved()
        {
            var s = _storageService.LoadKeyBinds();
            if (s == null)
                return;

            if (Enum.TryParse<Key>(s.OpenAppKey, out var ok)) OpenAppKey = ok;
            if (Enum.TryParse<ModifierKeys>(s.OpenAppMods, out var om)) OpenAppMods = om;
            if (Enum.TryParse<Key>(s.CycleKey, out var ck)) CycleKey = ck;
            if (Enum.TryParse<ModifierKeys>(s.CycleMods, out var cm)) CycleMods = cm;
            if (Enum.TryParse<Key>(s.MuteKey, out var mk)) MuteKey = mk;
            if (Enum.TryParse<ModifierKeys>(s.MuteMods, out var mm)) MuteMods = mm;
            if (Enum.TryParse<Key>(s.MixerKey, out var xik)) MixerKey = xik;
            if (Enum.TryParse<ModifierKeys>(s.MixerMods, out var xim)) MixerMods = xim;

            OpenAppGesture = !string.IsNullOrWhiteSpace(s.OpenAppGesture) ? NormalizeGesture(s.OpenAppGesture) : BuildGesture(OpenAppMods, OpenAppKey);
            CycleGesture = !string.IsNullOrWhiteSpace(s.CycleGesture) ? NormalizeGesture(s.CycleGesture) : BuildGesture(CycleMods, CycleKey);
            MuteGesture = !string.IsNullOrWhiteSpace(s.MuteGesture) ? NormalizeGesture(s.MuteGesture) : BuildGesture(MuteMods, MuteKey);
            MixerGesture = !string.IsNullOrWhiteSpace(s.MixerGesture) ? NormalizeGesture(s.MixerGesture) : BuildGesture(MixerMods, MixerKey);

            _isOpenAppEnabled = s.IsOpenAppEnabled;
            _isCycleEnabled = s.IsCycleEnabled;
            _isMuteEnabled = s.IsMuteEnabled;
            _isMixerEnabled = s.IsMixerEnabled;
        }

        public void SaveAll()
        {
            _storageService.SaveKeyBinds(new Models.KeyBindsSettings
            {
                OpenAppKey = OpenAppKey.ToString(),
                OpenAppMods = OpenAppMods.ToString(),
                CycleKey = CycleKey.ToString(),
                CycleMods = CycleMods.ToString(),
                MuteKey = MuteKey.ToString(),
                MuteMods = MuteMods.ToString(),
                MixerKey = MixerKey.ToString(),
                MixerMods = MixerMods.ToString(),
                OpenAppGesture = OpenAppGesture,
                CycleGesture = CycleGesture,
                MuteGesture = MuteGesture,
                MixerGesture = MixerGesture,
                IsOpenAppEnabled = _isOpenAppEnabled,
                IsCycleEnabled = _isCycleEnabled,
                IsMuteEnabled = _isMuteEnabled,
                IsMixerEnabled = _isMixerEnabled,
            });
        }

        private void RaiseRebind() => HotkeysChanged?.Invoke();

        public static string BuildGesture(ModifierKeys mods, params Key[] keys)
        {
            var parts = new List<string>();
            if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Control");
            if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Windows");

            parts.AddRange(keys
                .Where(k => k != Key.None)
                .Distinct()
                .OrderBy(k => KeyToLabel(k), StringComparer.OrdinalIgnoreCase)
                .Select(k => k.ToString()));

            return string.Join("+", parts);
        }

        public static string NormalizeGesture(string gesture)
        {
            if (string.IsNullOrWhiteSpace(gesture))
                return string.Empty;

            var modifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var keys = new HashSet<Key>();

            foreach (var rawToken in gesture.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var token = rawToken.Trim();
                if (token.Length == 0)
                    continue;

                if (TryMapModifierToken(token, out var modifier))
                {
                    modifiers.Add(modifier);
                    continue;
                }

                if (Enum.TryParse<Key>(token, true, out var key) && key != Key.None)
                    keys.Add(key);
            }

            var parts = new List<string>();
            foreach (var modifier in new[] { "Control", "Alt", "Shift", "Windows" })
            {
                if (modifiers.Contains(modifier))
                    parts.Add(modifier);
            }

            parts.AddRange(keys.OrderBy(k => KeyToLabel(k), StringComparer.OrdinalIgnoreCase).Select(k => k.ToString()));
            return string.Join("+", parts);
        }

        public static string FormatGesture(string gesture)
        {
            var normalized = NormalizeGesture(gesture);
            if (string.IsNullOrWhiteSpace(normalized))
                return "None";

            var parts = new List<string>();
            foreach (var token in normalized.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token == "Control") parts.Add("Ctrl");
                else if (token == "Windows") parts.Add("Win");
                else if (Enum.TryParse<Key>(token, true, out var key)) parts.Add(KeyToLabel(key));
                else parts.Add(token);
            }

            return string.Join(" + ", parts);
        }

        public static string FormatHotkey(ModifierKeys mods, Key key)
        {
            return FormatGesture(BuildGesture(mods, key));
        }

        private static (ModifierKeys mods, Key key) TryExtractSingleKeyGesture(string gesture)
        {
            var mods = ModifierKeys.None;
            Key key = Key.None;
            int keyCount = 0;

            foreach (var token in NormalizeGesture(gesture).Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token == "Control") mods |= ModifierKeys.Control;
                else if (token == "Alt") mods |= ModifierKeys.Alt;
                else if (token == "Shift") mods |= ModifierKeys.Shift;
                else if (token == "Windows") mods |= ModifierKeys.Windows;
                else if (Enum.TryParse<Key>(token, true, out var parsedKey))
                {
                    key = parsedKey;
                    keyCount++;
                }
            }

            return keyCount == 1 ? (mods, key) : (ModifierKeys.None, Key.None);
        }

        private void ResetGesture(Key key, ModifierKeys mods, string action)
        {
            var gesture = BuildGesture(mods, key);
            switch (action)
            {
                case "open":
                    OpenAppKey = key;
                    OpenAppMods = mods;
                    OpenAppGesture = gesture;
                    break;
                case "cycle":
                    CycleKey = key;
                    CycleMods = mods;
                    CycleGesture = gesture;
                    break;
                case "mute":
                    MuteKey = key;
                    MuteMods = mods;
                    MuteGesture = gesture;
                    break;
                case "mixer":
                    MixerKey = key;
                    MixerMods = mods;
                    MixerGesture = gesture;
                    break;
            }

            SaveAll();
            RaiseRebind();
        }

        private static bool TryMapModifierToken(string token, out string modifier)
        {
            modifier = token switch
            {
                "Ctrl" => "Control",
                "Control" => "Control",
                "Alt" => "Alt",
                "Shift" => "Shift",
                "Win" => "Windows",
                "Windows" => "Windows",
                _ => string.Empty
            };

            return modifier.Length > 0;
        }

        private static string KeyToLabel(Key key) => key switch
        {
            Key.D0 => "0", Key.D1 => "1", Key.D2 => "2", Key.D3 => "3",
            Key.D4 => "4", Key.D5 => "5", Key.D6 => "6", Key.D7 => "7",
            Key.D8 => "8", Key.D9 => "9",
            Key.OemComma => ",", Key.OemPeriod => ".", Key.OemMinus => "-",
            Key.OemPlus => "=", Key.Add => "+", Key.Subtract => "-",
            Key.Multiply => "*", Key.Divide => "/", Key.NumPad0 => "Num 0",
            Key.NumPad1 => "Num 1", Key.NumPad2 => "Num 2", Key.NumPad3 => "Num 3",
            Key.NumPad4 => "Num 4", Key.NumPad5 => "Num 5", Key.NumPad6 => "Num 6",
            Key.NumPad7 => "Num 7", Key.NumPad8 => "Num 8", Key.NumPad9 => "Num 9",
            Key.Space => "Space",
            _ => key.ToString()
        };
    }
}
