using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using EchoX.Models;
using EchoX.Services;
using EchoX.ViewModels;

namespace EchoX.ViewModels
{
    public class ProfilesViewModel : ViewModelBase
    {
        private readonly AudioEngine _audioEngine;
        private readonly StorageService _storageService;
        private readonly MainWindowViewModel _mainWindowViewModel;
        private int _currentProfileIndex = -1;
        private IDisposable? _deviceWatcher;

        public ProfilesViewModel(MainWindowViewModel mainWindowViewModel, AudioEngine audioEngine, StorageService storageService)
        {
            _mainWindowViewModel = mainWindowViewModel;
            _audioEngine = audioEngine;
            _storageService = storageService;

            SaveCommand = new RelayCommand(SaveProfile);
            CancelEditCommand = new RelayCommand(CancelEdit);
            ActivateCommand = new RelayCommand<AudioProfile>(ActivateProfile);
            EditCommand = new RelayCommand<AudioProfile>(EditProfile);
            DeleteCommand = new RelayCommand<AudioProfile>(DeleteProfile);
            NewCommand = new RelayCommand(() => {
                CancelEdit();
                IsEditorVisible = true;
            });

            // Step 1: Load cached data immediately
            LoadFromCache();

            // Step 2: Refresh asynchronously
            System.Threading.Tasks.Task.Run(() => InitialLoad());

            // Step 3: Watch for new devices (USB plug/unplug) in realtime
            _deviceWatcher = _audioEngine.WatchDevices(() => {
                System.Threading.Tasks.Task.Run(() => InitialLoad());
            });

            Profiles.CollectionChanged += (s, e) => {
                OnPropertyChanged(nameof(FilteredProfiles));
                OnPropertyChanged(nameof(IsEmpty));
            };
        }

        private void LoadFromCache()
        {
            var profiles = _storageService.LoadProfiles();
            Profiles.Clear();
            foreach (var p in profiles) Profiles.Add(p);
            OnPropertyChanged(nameof(FilteredProfiles));
            OnPropertyChanged(nameof(IsEmpty));
        }

        private void InitialLoad()
        {
            try
            {
                var profiles = _storageService.LoadProfiles();
                var activeId = _storageService.LoadActiveProfileId();

                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var currentIds = profiles.Select(p => p.Id).ToList();
                    var existingProfiles = Profiles.ToList();

                    // Remove gone
                    foreach (var existing in existingProfiles)
                        if (!currentIds.Contains(existing.Id)) Profiles.Remove(existing);

                    // Add new OR Update existing
                    foreach (var profile in profiles)
                    {
                        var match = Profiles.FirstOrDefault(p => p.Id == profile.Id);
                        if (match == null)
                        {
                            Profiles.Add(profile);
                        }
                        else
                        {
                            // Sync properties on existing object to keep UI bindings alive
                            match.Name = profile.Name;
                            match.InputDeviceId = profile.InputDeviceId;
                            match.OutputDeviceId = profile.OutputDeviceId;
                            match.CallInputDeviceId = profile.CallInputDeviceId;
                            match.CallOutputDeviceId = profile.CallOutputDeviceId;
                            match.ShortcutKey = profile.ShortcutKey;
                            match.ShortcutModifiers = profile.ShortcutModifiers;
                            match.ShortcutMouseButton = profile.ShortcutMouseButton;
                        }
                    }

                    if (!string.IsNullOrEmpty(activeId))
                    {
                        ActiveProfile = Profiles.FirstOrDefault(p => p.Id == activeId);
                    }
                    
                    OnPropertyChanged(nameof(FilteredProfiles));
                    OnPropertyChanged(nameof(IsEmpty));
                    RegisterAllProfileHotkeys();
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ProfilesViewModel InitialLoad: {ex.Message}");
            }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    OnPropertyChanged(nameof(FilteredProfiles));
                    OnPropertyChanged(nameof(IsEmpty));
                }
            }
        }

        public System.Collections.Generic.IEnumerable<AudioProfile> FilteredProfiles
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SearchText)) return Profiles;
                return Profiles.Where(p => p.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);
            }
        }

        public bool IsEmpty => !FilteredProfiles.Any();

        private string _profileName = string.Empty;
        public string ProfileName
        {
            get => _profileName;
            set => SetProperty(ref _profileName, value);
        }

        private AudioDevice? _selectedInputDevice;
        public AudioDevice? SelectedInputDevice
        {
            get => _selectedInputDevice;
            set => SetProperty(ref _selectedInputDevice, value);
        }

        private AudioDevice? _selectedOutputDevice;
        public AudioDevice? SelectedOutputDevice
        {
            get => _selectedOutputDevice;
            set => SetProperty(ref _selectedOutputDevice, value);
        }

        private AudioDevice? _selectedCallInputDevice;
        public AudioDevice? SelectedCallInputDevice
        {
            get => _selectedCallInputDevice;
            set => SetProperty(ref _selectedCallInputDevice, value);
        }

        private AudioDevice? _selectedCallOutputDevice;
        public AudioDevice? SelectedCallOutputDevice
        {
            get => _selectedCallOutputDevice;
            set => SetProperty(ref _selectedCallOutputDevice, value);
        }

        public ObservableCollection<AudioDevice> InputDevices  => _mainWindowViewModel.DevicesViewModel.InputDevices;
        public ObservableCollection<AudioDevice> OutputDevices => _mainWindowViewModel.DevicesViewModel.OutputDevices;
        public ObservableCollection<AudioProfile> Profiles     { get; } = new ObservableCollection<AudioProfile>();

        public ICommand SaveCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand ActivateCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }

        private bool _isActivating = false;
        public bool IsActivating => _isActivating;

        private AudioProfile? _activeProfile;
        public AudioProfile? ActiveProfile
        {
            get => _activeProfile;
            set
            {
                if (_activeProfile != null) _activeProfile.IsActive = false;
                if (SetProperty(ref _activeProfile, value) && value != null && !_isActivating)
                {
                    foreach (var p in Profiles)
                        p.IsActive = (p.Id == _activeProfile?.Id);
                    ActivateProfile(value);
                    OnPropertyChanged(nameof(ActiveShortcutDisplay));
                }
                else if (value == null)
                {
                    foreach (var p in Profiles) p.IsActive = false;
                    OnPropertyChanged(nameof(ActiveShortcutDisplay));
                }
            }
        }

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (SetProperty(ref _isEditing, value))
                    OnPropertyChanged(nameof(SaveButtonText));
            }
        }

        public string SaveButtonText => IsEditing ? "Update" : "Save";

        private bool _isEditorVisible;
        public bool IsEditorVisible
        {
            get => _isEditorVisible;
            set => SetProperty(ref _isEditorVisible, value);
        }

        public ICommand NewCommand { get; }

        // ── Selected profile for the detail/editor panel ─────────────────────
        private AudioProfile? _selectedProfile;
        public AudioProfile? SelectedProfile
        {
            get => _selectedProfile;
            set => SetProperty(ref _selectedProfile, value);
        }

        private string _shortcutText = string.Empty;
        public string ShortcutText
        {
            get => _shortcutText;
            set => SetProperty(ref _shortcutText, value);
        }

        public string ActiveShortcutDisplay
        {
            get
            {
                var p = ActiveProfile;
                if (p == null) return "None";
                if (!string.IsNullOrEmpty(p.ShortcutMouseButton))
                    return p.ShortcutMouseButton == "XButton1" ? "Mouse Button 4" : "Mouse Button 5";
                if (string.IsNullOrEmpty(p.ShortcutKey)) return "None";

                var parts = new System.Collections.Generic.List<string>();
                var shortcutModifiers = p.ShortcutModifiers;
                if (shortcutModifiers != null && shortcutModifiers.Length > 0)
                    foreach (var part in shortcutModifiers.Split(','))
                    {
                        var t = part.Trim();
                        if (t == "Control") parts.Add("Ctrl");
                        else if (t == "Windows") parts.Add("Win");
                        else if (!string.IsNullOrEmpty(t)) parts.Add(t);
                    }
                if (System.Enum.TryParse<System.Windows.Input.Key>(p.ShortcutKey, true, out var key))
                    parts.Add(KeyToDisplayString(key));
                else
                    parts.Add(p.ShortcutKey ?? string.Empty);
                return string.Join(" + ", parts);
            }
        }

        private string? _shortcutWarning;
        public string? ShortcutWarning
        {
            get => _shortcutWarning;
            set => SetProperty(ref _shortcutWarning, value);
        }

        /// <summary>Returns the name of the profile already using this shortcut, or null if free.</summary>
        public string? GetShortcutConflict(System.Windows.Input.Key key, System.Windows.Input.ModifierKeys mods, string? excludeProfileId)
        {
            string keyStr  = key.ToString();
            string modStr  = mods.ToString();
            foreach (var p in Profiles)
            {
                if (p.Id == excludeProfileId) continue;
                if (p.ShortcutKey == keyStr && p.ShortcutModifiers == modStr)
                    return p.Name;
            }
            return null;
        }

        // Called for mouse side buttons
        public void SetMouseShortcut(string displayLabel, string keyName)
        {
            ShortcutText = displayLabel;
            _pendingKey = null; // no keyboard key
            _pendingModifiers = System.Windows.Input.ModifierKeys.None;
            _pendingMouseButton = keyName;
        }

        private string? _pendingMouseButton;

        // Called from the UI's KeyDown handler
        public void SetShortcutFromKey(System.Windows.Input.Key key, System.Windows.Input.ModifierKeys modifiers)
        {
            // Ignore if only a modifier key was pressed
            if (key == System.Windows.Input.Key.LeftCtrl  || key == System.Windows.Input.Key.RightCtrl  ||
                key == System.Windows.Input.Key.LeftAlt   || key == System.Windows.Input.Key.RightAlt   ||
                key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
                key == System.Windows.Input.Key.LWin      || key == System.Windows.Input.Key.RWin       ||
                key == System.Windows.Input.Key.System)
                return;

            // Clear on Escape
            if (key == System.Windows.Input.Key.Escape)
            {
                ShortcutText = string.Empty;
                _pendingKey = null;
                _pendingModifiers = System.Windows.Input.ModifierKeys.None;
                _pendingMouseButton = null;
                return;
            }

            _pendingKey = key;
            _pendingModifiers = modifiers;

            // Build display string
            var parts = new System.Collections.Generic.List<string>();
            if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt))     parts.Add("Alt");
            if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))   parts.Add("Shift");
            if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Windows)) parts.Add("Win");
            parts.Add(KeyToDisplayString(key));
            ShortcutText = string.Join(" + ", parts);
        }

        private System.Windows.Input.Key? _pendingKey;
        private System.Windows.Input.ModifierKeys _pendingModifiers = System.Windows.Input.ModifierKeys.None;

        private static string KeyToDisplayString(System.Windows.Input.Key key)
        {
            // Clean up display names
            var name = key.ToString();
            if (name.StartsWith("D") && name.Length == 2 && char.IsDigit(name[1])) return name[1].ToString();
            if (name.StartsWith("NumPad")) return "Num" + name.Substring(6);
            return name;
        }

        // Helper: resolve a device ID to its display name
        public string GetDeviceName(string? deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return "—";
            var d = InputDevices.FirstOrDefault(x => x.Id == deviceId)
                 ?? OutputDevices.FirstOrDefault(x => x.Id == deviceId);
            return d?.Name ?? "Unknown device";
        }

        private void RegisterAllProfileHotkeys()
        {
            _mouseButtonProfiles.Clear();
            foreach (var p in Profiles)
                RegisterProfileHotkey(p);
        }

        private void RegisterProfileHotkey(AudioProfile profile)
        {
            var shortcutMouseButton = profile.ShortcutMouseButton;
            if (shortcutMouseButton != null && shortcutMouseButton.Length > 0)
            {
                // Mouse button shortcuts are handled by the global mouse hook in MainWindow
                // Store the mapping so the hook can look it up
                _mouseButtonProfiles[shortcutMouseButton] = profile;
                return;
            }

            if (string.IsNullOrWhiteSpace(profile.ShortcutKey)) return;
            try
            {
                if (System.Enum.TryParse<System.Windows.Input.Key>(profile.ShortcutKey, true, out var key))
                {
                    var modifiers = System.Windows.Input.ModifierKeys.None;
                    var shortcutModifiers = profile.ShortcutModifiers;
                    if (shortcutModifiers != null && shortcutModifiers.Length > 0)
                        foreach (var part in shortcutModifiers.Split(','))
                            if (System.Enum.TryParse<System.Windows.Input.ModifierKeys>(part.Trim(), true, out var mod))
                                modifiers |= mod;

                    NHotkey.Wpf.HotkeyManager.Current.AddOrReplace(
                        $"Profile_{profile.Id}", key, modifiers,
                        (s, e) => { ActivateProfile(profile); e.Handled = true; });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Profile hotkey failed: {ex.Message}");
            }
        }

        // Mouse button → profile mapping (used by MainWindow's global mouse hook)
        public readonly System.Collections.Generic.Dictionary<string, AudioProfile> _mouseButtonProfiles
            = new System.Collections.Generic.Dictionary<string, AudioProfile>();

        public void HandleMouseButton(string buttonName)
        {
            if (_mouseButtonProfiles.TryGetValue(buttonName, out var profile))
                ActivateProfile(profile);
        }

        private void UnregisterProfileHotkey(AudioProfile profile)
        {
            // Unregister keyboard hotkey
            try { NHotkey.Wpf.HotkeyManager.Current.Remove($"Profile_{profile.Id}"); }
            catch { }

            // Unregister mouse button mapping
            var shortcutMouseButton = profile.ShortcutMouseButton;
            if (shortcutMouseButton != null && shortcutMouseButton.Length > 0)
                _mouseButtonProfiles.Remove(shortcutMouseButton);
        }

        private void SaveProfile()
        {
            if (string.IsNullOrWhiteSpace(ProfileName) || SelectedInputDevice == null || SelectedOutputDevice == null)
                return;

            var profiles = _storageService.LoadProfiles();

            // Validation: Duplicate name check (excluding the one being edited)
            if (profiles.Any(p => p.Name.Equals(ProfileName, StringComparison.OrdinalIgnoreCase) && 
                (!IsEditing || p.Id != Profiles.ElementAtOrDefault(_currentProfileIndex)?.Id)))
            {
                // In a real app, show a message box. For now, we'll just suffix it.
                ProfileName += " (Copy)";
            }

            var profile = new AudioProfile
            {
                Name               = ProfileName,
                InputDeviceId      = SelectedInputDevice.Id,
                OutputDeviceId     = SelectedOutputDevice.Id,
                CallInputDeviceId  = SelectedCallInputDevice?.Id,
                CallOutputDeviceId = SelectedCallOutputDevice?.Id,
                ShortcutKey        = _pendingKey.HasValue ? _pendingKey.Value.ToString() : null,
                ShortcutModifiers  = _pendingKey.HasValue ? _pendingModifiers.ToString() : null,
                ShortcutMouseButton = _pendingMouseButton
            };
            
            AudioProfile? savedProfile = null;
            if (IsEditing && _currentProfileIndex >= 0 && _currentProfileIndex < Profiles.Count)
            {
                var existing = Profiles[_currentProfileIndex];
                UnregisterProfileHotkey(existing);

                // Update in-memory ObservableCollection object (drives the UI)
                existing.Name               = ProfileName;
                existing.InputDeviceId      = SelectedInputDevice.Id;
                existing.OutputDeviceId     = SelectedOutputDevice.Id;
                existing.CallInputDeviceId  = SelectedCallInputDevice?.Id;
                existing.CallOutputDeviceId = SelectedCallOutputDevice?.Id;
                existing.ShortcutKey        = _pendingKey.HasValue ? _pendingKey.Value.ToString() : null;
                existing.ShortcutModifiers  = _pendingKey.HasValue ? _pendingModifiers.ToString() : null;
                existing.ShortcutMouseButton = _pendingMouseButton;

                // Update the storage list entry by ID
                int storageIndex = profiles.FindIndex(p => p.Id == existing.Id);
                if (storageIndex >= 0)
                {
                    profiles[storageIndex].Name               = existing.Name;
                    profiles[storageIndex].InputDeviceId      = existing.InputDeviceId;
                    profiles[storageIndex].OutputDeviceId     = existing.OutputDeviceId;
                    profiles[storageIndex].CallInputDeviceId  = existing.CallInputDeviceId;
                    profiles[storageIndex].CallOutputDeviceId = existing.CallOutputDeviceId;
                    profiles[storageIndex].ShortcutKey        = existing.ShortcutKey;
                    profiles[storageIndex].ShortcutModifiers  = existing.ShortcutModifiers;
                    profiles[storageIndex].ShortcutMouseButton = existing.ShortcutMouseButton;
                }
                savedProfile = existing;
            }
            else
            {
                profiles.Add(profile);
                savedProfile = profile;
            }

            _storageService.SaveProfiles(profiles);
            
            // Register hotkey for the saved profile
            if (savedProfile != null) RegisterProfileHotkey(savedProfile);

            // Refresh shortcut display
            OnPropertyChanged(nameof(ActiveShortcutDisplay));

            // Auto-sync hardware if this was the active profile
            if (ActiveProfile != null && savedProfile != null && ActiveProfile.Id == savedProfile.Id)
            {
                ActivateProfile(savedProfile);
            }

            CancelEdit();
            IsEditorVisible = false;
            
            // Re-sync but don't clear full list
            InitialLoad();
        }

        private void CancelEdit()
        {
            ProfileName = string.Empty;
            SelectedInputDevice = null;
            SelectedOutputDevice = null;
            SelectedCallInputDevice = null;
            SelectedCallOutputDevice = null;
            ShortcutText = string.Empty;
            _pendingKey = null;
            _pendingModifiers = System.Windows.Input.ModifierKeys.None;
            _pendingMouseButton = null;
            ShortcutWarning = null;
            IsEditing = false;
            IsEditorVisible = false;
            SelectedProfile = null;
            _currentProfileIndex = -1;
        }

        private void EditProfile(AudioProfile profile)
        {
            SelectedProfile = profile;
            IsEditing = true;
            IsEditorVisible = true;
            ProfileName = profile.Name;
            ShortcutText = profile.ShortcutKey ?? string.Empty;
            // Restore pending key/modifiers from saved profile
            if (!string.IsNullOrEmpty(profile.ShortcutMouseButton))
            {
                _pendingMouseButton = profile.ShortcutMouseButton;
                _pendingKey = null;
                ShortcutText = profile.ShortcutMouseButton == "XButton1" ? "Mouse Button 4" : "Mouse Button 5";
            }
            else if (!string.IsNullOrEmpty(profile.ShortcutKey) &&
                System.Enum.TryParse<System.Windows.Input.Key>(profile.ShortcutKey, true, out var savedKey))
            {
                _pendingKey = savedKey;
                _pendingModifiers = System.Windows.Input.ModifierKeys.None;
                var shortcutModifiers = profile.ShortcutModifiers;
                if (shortcutModifiers != null && shortcutModifiers.Length > 0)
                    foreach (var part in shortcutModifiers.Split(','))
                        if (System.Enum.TryParse<System.Windows.Input.ModifierKeys>(part.Trim(), true, out var mod))
                            _pendingModifiers |= mod;

                // Build display string directly (avoid SetShortcutFromKey guard checks)
                var parts = new System.Collections.Generic.List<string>();
                if (_pendingModifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)) parts.Add("Ctrl");
                if (_pendingModifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt))     parts.Add("Alt");
                if (_pendingModifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))   parts.Add("Shift");
                if (_pendingModifiers.HasFlag(System.Windows.Input.ModifierKeys.Windows)) parts.Add("Win");
                parts.Add(KeyToDisplayString(savedKey));
                ShortcutText = string.Join(" + ", parts);
            }
            else
            {
                ShortcutText = string.Empty;
                _pendingKey = null;
                _pendingModifiers = System.Windows.Input.ModifierKeys.None;
                _pendingMouseButton = null;
            }
            _currentProfileIndex = Profiles.IndexOf(profile);
            
            SelectedInputDevice = InputDevices.FirstOrDefault(d => d.Id == profile.InputDeviceId);
            SelectedOutputDevice = OutputDevices.FirstOrDefault(d => d.Id == profile.OutputDeviceId);
            SelectedCallInputDevice = InputDevices.FirstOrDefault(d => d.Id == profile.CallInputDeviceId);
            SelectedCallOutputDevice = OutputDevices.FirstOrDefault(d => d.Id == profile.CallOutputDeviceId);
        }

        private void ActivateProfile(AudioProfile profile)
        {
            if (profile == null) return;
            try
            {
                _isActivating = true;
                ActiveProfile = profile;
                _isActivating = false;

                foreach (var p in Profiles)
                    p.IsActive = (p.Id == profile.Id);

                // Always notify shortcut display after activation
                OnPropertyChanged(nameof(ActiveShortcutDisplay));

                _storageService.SaveActiveProfileId(profile.Id);
                var devVm = _mainWindowViewModel.DevicesViewModel;
                int pendingPrimarySwitches = 0;

                if (!string.IsNullOrEmpty(profile.InputDeviceId))
                {
                    var match = devVm.InputDevices.FirstOrDefault(d => d.Id == profile.InputDeviceId);
                    if (match != null)
                    {
                        pendingPrimarySwitches++;
                        devVm._pendingSwitches = pendingPrimarySwitches;
                        devVm.CurrentInputDevice = match;
                    }
                }

                if (!string.IsNullOrEmpty(profile.OutputDeviceId))
                {
                    var match = devVm.OutputDevices.FirstOrDefault(d => d.Id == profile.OutputDeviceId);
                    if (match != null)
                    {
                        pendingPrimarySwitches++;
                        devVm._pendingSwitches = pendingPrimarySwitches;
                        devVm.CurrentOutputDevice = match;
                    }
                }

                if (!string.IsNullOrEmpty(profile.CallInputDeviceId))
                {
                    var match = devVm.InputDevices.FirstOrDefault(d => d.Id == profile.CallInputDeviceId);
                    if (match != null) devVm.SetCallInputCommand.Execute(match);
                }

                if (!string.IsNullOrEmpty(profile.CallOutputDeviceId))
                {
                    var match = devVm.OutputDevices.FirstOrDefault(d => d.Id == profile.CallOutputDeviceId);
                    if (match != null) devVm.SetCallOutputCommand.Execute(match);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ActivateProfile failed: {ex.Message}");
            }
        }


        private void DeleteProfile(AudioProfile profile)
        {
            var dialog = new ConfirmDeleteDialog(profile.Name)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true)
                return;

            // If the deleted profile was being edited, clear the form
            if (IsEditing && _currentProfileIndex >= 0 && _currentProfileIndex < Profiles.Count)
            {
                if (Profiles[_currentProfileIndex].Id == profile.Id)
                    CancelEdit();
            }

            var profiles = _storageService.LoadProfiles();
            profiles.RemoveAll(p => p.Id == profile.Id);
            _storageService.SaveProfiles(profiles);
            UnregisterProfileHotkey(profile);

            // If deleted the active one, clear active
            if (ActiveProfile?.Id == profile.Id)
            {
                ActiveProfile = null;
                _storageService.SaveActiveProfileId("");
            }

            InitialLoad();
        }

        public void CycleProfiles()
        {
            if (Profiles.Count == 0) return;

            _currentProfileIndex++;
            if (_currentProfileIndex >= Profiles.Count)
                _currentProfileIndex = 0;

            var nextProfile = Profiles[_currentProfileIndex];
            ActivateProfile(nextProfile);

            _mainWindowViewModel.NotifyTray("Profile Switched", $"Activated: {nextProfile.Name}");
        }

        public void ToggleMicMute()
        {
            bool isNowMuted = _audioEngine.ToggleMuteDefaultMic();
            
            // Invoke the event for MainWindow tray icon/GUI
            _mainWindowViewModel.OnMicrophoneMuteChanged(isNowMuted);
            
            // Notify via tray balloon
            _mainWindowViewModel.NotifyTray("Microphone", isNowMuted ? "Muted" : "Unmuted");

            // Update UI list for devices
            _mainWindowViewModel.DevicesViewModel.UpdateDeviceMuteStates();
        }
    }
}
