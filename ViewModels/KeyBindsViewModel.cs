using System;
using System.Windows.Input;
using EchoX.Services;

namespace EchoX.ViewModels
{
    public class KeyBindsViewModel : ViewModelBase
    {
        private readonly StorageService _storageService;

        // ── Defaults ──
        private static readonly Key DefaultOpenAppKey   = Key.X;
        private static readonly ModifierKeys DefaultOpenAppMods = ModifierKeys.Shift | ModifierKeys.Alt;
        private static readonly Key DefaultCycleKey     = Key.S;
        private static readonly ModifierKeys DefaultCycleMods   = ModifierKeys.Control | ModifierKeys.Alt;
        private static readonly Key DefaultMuteKey      = Key.K;
        private static readonly ModifierKeys DefaultMuteMods    = ModifierKeys.Control | ModifierKeys.Alt;

        // ── Backing fields ──
        private Key _openAppKey = DefaultOpenAppKey;
        private ModifierKeys _openAppMods = DefaultOpenAppMods;
        private Key _cycleKey = DefaultCycleKey;
        private ModifierKeys _cycleMods = DefaultCycleMods;
        private Key _muteKey = DefaultMuteKey;
        private ModifierKeys _muteMods = DefaultMuteMods;

        private bool _isOpenAppEnabled = true;
        private bool _isCycleEnabled   = true;
        private bool _isMuteEnabled    = true;

        private bool _isCapturingOpenApp;
        private bool _isCapturingCycle;
        private bool _isCapturingMute;

        public KeyBindsViewModel(StorageService storageService)
        {
            _storageService = storageService;
            LoadSaved();

            StartCaptureOpenAppCommand = new RelayCommand(() => { IsCapturingOpenApp = true; IsCapturingCycle = false; IsCapturingMute = false; });
            StartCaptureCycleCommand   = new RelayCommand(() => { IsCapturingCycle = true; IsCapturingMute = false; IsCapturingOpenApp = false; });
            StartCaptureMuteCommand    = new RelayCommand(() => { IsCapturingMute = true; IsCapturingCycle = false; IsCapturingOpenApp = false; });

            ResetOpenAppCommand = new RelayCommand(() => { OpenAppKey = DefaultOpenAppKey; OpenAppMods = DefaultOpenAppMods; SaveAll(); RaiseRebind(); });
            ResetCycleCommand   = new RelayCommand(() => { CycleKey = DefaultCycleKey; CycleMods = DefaultCycleMods; SaveAll(); RaiseRebind(); });
            ResetMuteCommand    = new RelayCommand(() => { MuteKey = DefaultMuteKey; MuteMods = DefaultMuteMods; SaveAll(); RaiseRebind(); });
        }

        // ── Commands ──
        public ICommand StartCaptureOpenAppCommand { get; }
        public ICommand StartCaptureCycleCommand   { get; }
        public ICommand StartCaptureMuteCommand    { get; }
        public ICommand ResetOpenAppCommand        { get; }
        public ICommand ResetCycleCommand          { get; }
        public ICommand ResetMuteCommand           { get; }

        public event Action? HotkeysChanged;

        // ── Enabled toggles ──
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

        // ── Key/Mod properties ──
        public Key OpenAppKey
        {
            get => _openAppKey;
            set { if (SetProperty(ref _openAppKey, value)) OnPropertyChanged(nameof(OpenAppDisplay)); }
        }
        public ModifierKeys OpenAppMods
        {
            get => _openAppMods;
            set { if (SetProperty(ref _openAppMods, value)) OnPropertyChanged(nameof(OpenAppDisplay)); }
        }
        public Key CycleKey
        {
            get => _cycleKey;
            set { if (SetProperty(ref _cycleKey, value)) OnPropertyChanged(nameof(CycleDisplay)); }
        }
        public ModifierKeys CycleMods
        {
            get => _cycleMods;
            set { if (SetProperty(ref _cycleMods, value)) OnPropertyChanged(nameof(CycleDisplay)); }
        }
        public Key MuteKey
        {
            get => _muteKey;
            set { if (SetProperty(ref _muteKey, value)) OnPropertyChanged(nameof(MuteDisplay)); }
        }
        public ModifierKeys MuteMods
        {
            get => _muteMods;
            set { if (SetProperty(ref _muteMods, value)) OnPropertyChanged(nameof(MuteDisplay)); }
        }

        // ── Capture state ──
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

        public string OpenAppDisplay => IsCapturingOpenApp ? "Press keys\u2026" : FormatHotkey(OpenAppMods, OpenAppKey);
        public string CycleDisplay   => IsCapturingCycle   ? "Press keys\u2026" : FormatHotkey(CycleMods,   CycleKey);
        public string MuteDisplay    => IsCapturingMute    ? "Press keys\u2026" : FormatHotkey(MuteMods,    MuteKey);

        // ── Capture ──
        public bool TryCapture(Key key, ModifierKeys mods)
        {
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt  || key == Key.RightAlt  ||
                key == Key.LeftShift|| key == Key.RightShift||
                key == Key.LWin     || key == Key.RWin)
                return false;

            if (IsCapturingOpenApp) { OpenAppKey = key; OpenAppMods = mods; IsCapturingOpenApp = false; SaveAll(); RaiseRebind(); return true; }
            if (IsCapturingCycle)   { CycleKey   = key; CycleMods   = mods; IsCapturingCycle   = false; SaveAll(); RaiseRebind(); return true; }
            if (IsCapturingMute)    { MuteKey    = key; MuteMods    = mods; IsCapturingMute    = false; SaveAll(); RaiseRebind(); return true; }
            return false;
        }

        public void CancelCapture()
        {
            IsCapturingOpenApp = false;
            IsCapturingCycle   = false;
            IsCapturingMute    = false;
        }

        // ── Persistence ──
        private void LoadSaved()
        {
            var s = _storageService.LoadKeyBinds();
            if (s == null) return;
            if (Enum.TryParse<Key>(s.OpenAppKey, out var ok))           OpenAppKey  = ok;
            if (Enum.TryParse<ModifierKeys>(s.OpenAppMods, out var om)) OpenAppMods = om;
            if (Enum.TryParse<Key>(s.CycleKey, out var ck))             CycleKey    = ck;
            if (Enum.TryParse<ModifierKeys>(s.CycleMods, out var cm))   CycleMods   = cm;
            if (Enum.TryParse<Key>(s.MuteKey, out var mk))              MuteKey     = mk;
            if (Enum.TryParse<ModifierKeys>(s.MuteMods, out var mm))    MuteMods    = mm;
            _isOpenAppEnabled = s.IsOpenAppEnabled;
            _isCycleEnabled   = s.IsCycleEnabled;
            _isMuteEnabled    = s.IsMuteEnabled;
        }

        public void SaveAll()
        {
            _storageService.SaveKeyBinds(new Models.KeyBindsSettings
            {
                OpenAppKey       = OpenAppKey.ToString(),
                OpenAppMods      = OpenAppMods.ToString(),
                CycleKey         = CycleKey.ToString(),
                CycleMods        = CycleMods.ToString(),
                MuteKey          = MuteKey.ToString(),
                MuteMods         = MuteMods.ToString(),
                IsOpenAppEnabled = _isOpenAppEnabled,
                IsCycleEnabled   = _isCycleEnabled,
                IsMuteEnabled    = _isMuteEnabled,
            });
        }

        private void RaiseRebind() => HotkeysChanged?.Invoke();

        public static string FormatHotkey(ModifierKeys mods, Key key)
        {
            if (key == Key.None) return "None";
            var parts = new System.Collections.Generic.List<string>();
            if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (mods.HasFlag(ModifierKeys.Alt))     parts.Add("Alt");
            if (mods.HasFlag(ModifierKeys.Shift))   parts.Add("Shift");
            if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            parts.Add(KeyToLabel(key));
            return string.Join(" + ", parts);
        }

        private static string KeyToLabel(Key key) => key switch
        {
            Key.D0 => "0", Key.D1 => "1", Key.D2 => "2", Key.D3 => "3",
            Key.D4 => "4", Key.D5 => "5", Key.D6 => "6", Key.D7 => "7",
            Key.D8 => "8", Key.D9 => "9",
            Key.OemComma => ",", Key.OemPeriod => ".", Key.OemMinus => "-",
            Key.OemPlus => "=", Key.Space => "Space",
            _ => key.ToString()
        };
    }
}
