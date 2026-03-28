using System;
using System.Windows.Input;
using EchoX.Services;

namespace EchoX.ViewModels
{
    public class KeyBindsViewModel : ViewModelBase
    {
        private readonly StorageService _storageService;

        // ── Global hotkey backing fields ──
        private Key _cycleKey = Key.S;
        private ModifierKeys _cycleMods = ModifierKeys.Control | ModifierKeys.Alt;
        private Key _muteKey = Key.K;
        private ModifierKeys _muteMods = ModifierKeys.Control | ModifierKeys.Alt;

        private bool _isCapturingCycle;
        private bool _isCapturingMute;

        public KeyBindsViewModel(StorageService storageService)
        {
            _storageService = storageService;
            LoadSaved();

            StartCaptureCycleCommand = new RelayCommand(() => { IsCapturingCycle = true; IsCapturingMute = false; });
            StartCaptureMuteCommand  = new RelayCommand(() => { IsCapturingMute = true; IsCapturingCycle = false; });
            ClearCycleCommand        = new RelayCommand(() => { CycleKey = Key.None; CycleMods = ModifierKeys.None; SaveAll(); RaiseRebind(); });
            ClearMuteCommand         = new RelayCommand(() => { MuteKey = Key.None; MuteMods = ModifierKeys.None; SaveAll(); RaiseRebind(); });
        }

        // ── Commands ──
        public ICommand StartCaptureCycleCommand { get; }
        public ICommand StartCaptureMuteCommand  { get; }
        public ICommand ClearCycleCommand        { get; }
        public ICommand ClearMuteCommand         { get; }

        // ── Fired when the user finishes capturing a new binding ──
        public event Action? HotkeysChanged;

        // ── Properties ──
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

        public string CycleDisplay => IsCapturingCycle ? "Press keys…" : FormatHotkey(CycleMods, CycleKey);
        public string MuteDisplay  => IsCapturingMute  ? "Press keys…" : FormatHotkey(MuteMods,  MuteKey);

        // ── Called from MainWindow on KeyDown when capturing ──
        public bool TryCapture(Key key, ModifierKeys mods)
        {
            // Ignore lone modifier presses
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt  || key == Key.RightAlt  ||
                key == Key.LeftShift|| key == Key.RightShift||
                key == Key.LWin     || key == Key.RWin)
                return false;

            if (IsCapturingCycle)
            {
                CycleKey = key; CycleMods = mods;
                IsCapturingCycle = false;
                SaveAll(); RaiseRebind();
                return true;
            }
            if (IsCapturingMute)
            {
                MuteKey = key; MuteMods = mods;
                IsCapturingMute = false;
                SaveAll(); RaiseRebind();
                return true;
            }
            return false;
        }

        public void CancelCapture()
        {
            IsCapturingCycle = false;
            IsCapturingMute  = false;
        }

        // ── Persistence ──
        private void LoadSaved()
        {
            var s = _storageService.LoadKeyBinds();
            if (s == null) return;
            if (Enum.TryParse<Key>(s.CycleKey, out var ck))           CycleKey  = ck;
            if (Enum.TryParse<ModifierKeys>(s.CycleMods, out var cm)) CycleMods = cm;
            if (Enum.TryParse<Key>(s.MuteKey, out var mk))            MuteKey   = mk;
            if (Enum.TryParse<ModifierKeys>(s.MuteMods, out var mm))  MuteMods  = mm;
        }

        public void SaveAll()
        {
            _storageService.SaveKeyBinds(new Models.KeyBindsSettings
            {
                CycleKey  = CycleKey.ToString(),
                CycleMods = CycleMods.ToString(),
                MuteKey   = MuteKey.ToString(),
                MuteMods  = MuteMods.ToString()
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
            if (mods.HasFlag(ModifierKeys.Windows))  parts.Add("Win");
            parts.Add(KeyToLabel(key));
            return string.Join(" + ", parts);
        }

        private static string KeyToLabel(Key key)
        {
            return key switch
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
}
