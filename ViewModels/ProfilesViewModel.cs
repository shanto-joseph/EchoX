using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
                            match.ShortcutGesture = profile.ShortcutGesture;
                            match.ShortcutKey = profile.ShortcutKey;
                            match.ShortcutModifiers = profile.ShortcutModifiers;
                            match.ShortcutMouseButton = profile.ShortcutMouseButton;
                        }
                    }

                    if (!string.IsNullOrEmpty(activeId))
                    {
                        SetActiveProfileSilently(Profiles.FirstOrDefault(p => p.Id == activeId));
                    }
                    else
                    {
                        SetActiveProfileSilently(null);
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
        public ICommand NewCommand { get; }

        /// <summary>Raised when the user wants to edit a profile from an external tab — subscriber should navigate to Profiles tab.</summary>
        public event Action<AudioProfile>? EditAndNavigateRequested;

        private bool _isActivating = false;
        public bool IsActivating => _isActivating;

        private AudioProfile? _activeProfile;
        public AudioProfile? ActiveProfile
        {
            get => _activeProfile;
            set
            {
                if (_activeProfile != null)
                {
                    _activeProfile.IsActive = false;
                    _activeProfile.PropertyChanged -= ActiveProfile_PropertyChanged;
                }
                if (SetProperty(ref _activeProfile, value) && value != null && !_isActivating)
                {
                    value.PropertyChanged += ActiveProfile_PropertyChanged;
                    foreach (var p in Profiles)
                        p.IsActive = (p.Id == _activeProfile?.Id);
                    ActivateProfile(value);
                    OnPropertyChanged(nameof(ActiveShortcutDisplay));
                }
                else if (value != null)
                {
                    value.PropertyChanged -= ActiveProfile_PropertyChanged;
                    value.PropertyChanged += ActiveProfile_PropertyChanged;
                }
                else if (value == null)
                {
                    foreach (var p in Profiles) p.IsActive = false;
                    OnPropertyChanged(nameof(ActiveShortcutDisplay));
                }
            }
        }

        public void SetActiveProfileSilently(AudioProfile? profile)
        {
            _isActivating = true;
            ActiveProfile = profile;
            _isActivating = false;
        }

        private void ActiveProfile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AudioProfile.ShortcutKey) ||
                e.PropertyName == nameof(AudioProfile.ShortcutGesture) ||
                e.PropertyName == nameof(AudioProfile.ShortcutModifiers) ||
                e.PropertyName == nameof(AudioProfile.ShortcutMouseButton) ||
                e.PropertyName == nameof(AudioProfile.ShortcutDisplay))
            {
                OnPropertyChanged(nameof(ActiveShortcutDisplay));
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
                return string.Equals(p.ShortcutDisplay, "No shortcut", StringComparison.OrdinalIgnoreCase)
                    ? "None"
                    : p.ShortcutDisplay;
            }
        }

        private string? _shortcutWarning;
        public string? ShortcutWarning
        {
            get => _shortcutWarning;
            set => SetProperty(ref _shortcutWarning, value);
        }

        /// <summary>Returns the name of the profile already using this shortcut, or null if free.</summary>
        public string? GetShortcutConflict(string? gesture, string? mouseButton, string? excludeProfileId)
        {
            var normalizedGesture = string.IsNullOrWhiteSpace(gesture)
                ? null
                : KeyBindsViewModel.NormalizeGesture(gesture!);

            foreach (var p in Profiles)
            {
                if (p.Id == excludeProfileId) continue;

                if (!string.IsNullOrWhiteSpace(mouseButton) &&
                    string.Equals(p.ShortcutMouseButton, mouseButton, StringComparison.OrdinalIgnoreCase))
                    return p.Name;

                var profileGesture = GetEffectiveProfileGesture(p);
                if (!string.IsNullOrWhiteSpace(normalizedGesture) &&
                    string.Equals(profileGesture, normalizedGesture, StringComparison.OrdinalIgnoreCase))
                    return p.Name;
            }
            return null;
        }

        // Called for mouse side buttons
        public void SetMouseShortcut(string displayLabel, string keyName)
        {
            ShortcutText = displayLabel;
            _pendingGesture = null;
            _pendingMouseButton = keyName;
        }

        private string? _pendingMouseButton;
        private string? _pendingGesture;

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
                _pendingGesture = null;
                _pendingMouseButton = null;
                return;
            }

            SetShortcutFromGesture(KeyBindsViewModel.BuildGesture(modifiers, key));
        }

        public void SetShortcutFromGesture(string? gesture)
        {
            if (string.IsNullOrWhiteSpace(gesture))
            {
                ShortcutText = string.Empty;
                _pendingGesture = null;
                _pendingMouseButton = null;
                return;
            }

            _pendingGesture = KeyBindsViewModel.NormalizeGesture(gesture!);
            _pendingMouseButton = null;
            ShortcutText = KeyBindsViewModel.FormatGesture(_pendingGesture);
        }

        private static string KeyToDisplayString(System.Windows.Input.Key key)
        {
            // Clean up display names
            var name = key.ToString();
            if (key == System.Windows.Input.Key.OemMinus) return "-";
            if (key == System.Windows.Input.Key.OemPlus) return "=";
            if (key == System.Windows.Input.Key.OemComma) return ",";
            if (key == System.Windows.Input.Key.OemPeriod) return ".";
            if (key == System.Windows.Input.Key.Add) return "+";
            if (key == System.Windows.Input.Key.Subtract) return "-";
            if (key == System.Windows.Input.Key.Multiply) return "*";
            if (key == System.Windows.Input.Key.Divide) return "/";
            if (name.StartsWith("D") && name.Length == 2 && char.IsDigit(name[1])) return name[1].ToString();
            if (name.StartsWith("NumPad")) return "Num" + name.Substring(6);
            return name;
        }

        private static string? GetEffectiveProfileGesture(AudioProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.ShortcutGesture))
                return KeyBindsViewModel.NormalizeGesture(profile.ShortcutGesture!);

            if (string.IsNullOrWhiteSpace(profile.ShortcutKey))
                return null;

            if (!System.Enum.TryParse<System.Windows.Input.Key>(profile.ShortcutKey, true, out var key))
                return null;

            return KeyBindsViewModel.BuildGesture(ParseModifiers(profile.ShortcutModifiers), key);
        }

        private static System.Windows.Input.ModifierKeys ParseModifiers(string? modifierText)
        {
            var modifiers = System.Windows.Input.ModifierKeys.None;
            if (string.IsNullOrWhiteSpace(modifierText))
                return modifiers;

            foreach (var part in modifierText!.Split(','))
            {
                if (System.Enum.TryParse<System.Windows.Input.ModifierKeys>(part.Trim(), true, out var mod))
                    modifiers |= mod;
            }

            return modifiers;
        }

        private static void ApplyLegacyShortcutFields(AudioProfile profile)
        {
            profile.ShortcutKey = null;
            profile.ShortcutModifiers = null;

            var gesture = GetEffectiveProfileGesture(profile);
            if (string.IsNullOrWhiteSpace(gesture))
                return;

            var tokens = gesture!.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.Trim())
                .Where(token => token.Length > 0)
                .ToArray();

            var modifierTokens = new System.Collections.Generic.List<string>();
            System.Windows.Input.Key? lastKey = null;

            foreach (var token in tokens)
            {
                if (System.Enum.TryParse<System.Windows.Input.ModifierKeys>(token, true, out var _))
                {
                    modifierTokens.Add(token);
                    continue;
                }

                if (!System.Enum.TryParse<System.Windows.Input.Key>(token, true, out var parsedKey))
                    return;

                if (lastKey.HasValue)
                    return;

                lastKey = parsedKey;
            }

            if (!lastKey.HasValue)
                return;

            profile.ShortcutKey = lastKey.Value.ToString();
            profile.ShortcutModifiers = modifierTokens.Count > 0 ? string.Join(", ", modifierTokens) : null;
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
            _gestureProfiles.Clear();
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
                _mouseButtonProfiles[shortcutMouseButton!] = profile;
                return;
            }

            var gesture = GetEffectiveProfileGesture(profile);
            if (!string.IsNullOrWhiteSpace(gesture))
                _gestureProfiles[gesture!] = profile;
        }

        // Mouse button → profile mapping (used by MainWindow's global mouse hook)
        public readonly System.Collections.Generic.Dictionary<string, AudioProfile> _mouseButtonProfiles
            = new System.Collections.Generic.Dictionary<string, AudioProfile>();
        private readonly System.Collections.Generic.Dictionary<string, AudioProfile> _gestureProfiles
            = new System.Collections.Generic.Dictionary<string, AudioProfile>(StringComparer.OrdinalIgnoreCase);

        public void HandleMouseButton(string buttonName)
        {
            if (_mouseButtonProfiles.TryGetValue(buttonName, out var profile))
                ActivateProfile(profile);
        }

        public bool HandleGesture(string gesture)
        {
            if (string.IsNullOrWhiteSpace(gesture))
                return false;

            var normalized = KeyBindsViewModel.NormalizeGesture(gesture);
            var profile = Profiles.FirstOrDefault(candidate =>
                string.Equals(GetEffectiveProfileGesture(candidate), normalized, StringComparison.OrdinalIgnoreCase));

            if (profile != null)
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() => ActivateProfile(profile)));
                return true;
            }

            return false;
        }

        private void UnregisterProfileHotkey(AudioProfile profile)
        {
            var shortcutMouseButton = profile.ShortcutMouseButton;
            if (shortcutMouseButton != null && shortcutMouseButton.Length > 0)
                _mouseButtonProfiles.Remove(shortcutMouseButton!);

            var gesture = GetEffectiveProfileGesture(profile);
            if (!string.IsNullOrWhiteSpace(gesture))
                _gestureProfiles.Remove(gesture!);
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
                ShortcutGesture    = _pendingGesture,
                ShortcutMouseButton = _pendingMouseButton
            };
            ApplyLegacyShortcutFields(profile);
            
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
                existing.ShortcutGesture    = _pendingGesture;
                existing.ShortcutMouseButton = _pendingMouseButton;
                ApplyLegacyShortcutFields(existing);

                // Update the storage list entry by ID
                int storageIndex = profiles.FindIndex(p => p.Id == existing.Id);
                if (storageIndex >= 0)
                {
                    profiles[storageIndex].Name               = existing.Name;
                    profiles[storageIndex].InputDeviceId      = existing.InputDeviceId;
                    profiles[storageIndex].OutputDeviceId     = existing.OutputDeviceId;
                    profiles[storageIndex].CallInputDeviceId  = existing.CallInputDeviceId;
                    profiles[storageIndex].CallOutputDeviceId = existing.CallOutputDeviceId;
                    profiles[storageIndex].ShortcutGesture    = existing.ShortcutGesture;
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
            _pendingGesture = null;
            _pendingMouseButton = null;
            ShortcutWarning = null;
            IsEditing = false;
            IsEditorVisible = false;
            SelectedProfile = null;
            _currentProfileIndex = -1;
        }

        public void CancelEditSession()
        {
            CancelEdit();
        }

        private void EditProfile(AudioProfile profile)
        {
            SelectedProfile = profile;
            IsEditing = true;
            IsEditorVisible = true;
            ProfileName = profile.Name;
            _pendingGesture = null;
            _pendingMouseButton = null;
            ShortcutText = string.Empty;

            if (!string.IsNullOrEmpty(profile.ShortcutMouseButton))
            {
                _pendingMouseButton = profile.ShortcutMouseButton;
                ShortcutText = profile.ShortcutMouseButton == "XButton1" ? "Mouse Button 4" : "Mouse Button 5";
            }
            else if (!string.IsNullOrWhiteSpace(profile.ShortcutGesture))
            {
                _pendingGesture = KeyBindsViewModel.NormalizeGesture(profile.ShortcutGesture!);
                ShortcutText = KeyBindsViewModel.FormatGesture(_pendingGesture);
            }
            else if (!string.IsNullOrEmpty(profile.ShortcutKey) &&
                System.Enum.TryParse<System.Windows.Input.Key>(profile.ShortcutKey, true, out var savedKey))
            {
                _pendingGesture = KeyBindsViewModel.BuildGesture(ParseModifiers(profile.ShortcutModifiers), savedKey);
                ShortcutText = KeyBindsViewModel.FormatGesture(_pendingGesture);
            }
            else
            {
                ShortcutText = string.Empty;
                _pendingMouseButton = null;
            }
            _currentProfileIndex = Profiles.IndexOf(profile);
            
            SelectedInputDevice = InputDevices.FirstOrDefault(d => d.Id == profile.InputDeviceId);
            SelectedOutputDevice = OutputDevices.FirstOrDefault(d => d.Id == profile.OutputDeviceId);
            SelectedCallInputDevice = InputDevices.FirstOrDefault(d => d.Id == profile.CallInputDeviceId);
            SelectedCallOutputDevice = OutputDevices.FirstOrDefault(d => d.Id == profile.CallOutputDeviceId);
        }

        /// <summary>Called from Key Binds tab — opens the profile editor and asks MainWindow to navigate to Profiles tab.</summary>
        public void EditAndNavigate(AudioProfile profile)
        {
            EditProfile(profile);
            EditAndNavigateRequested?.Invoke(profile);
        }

        /// <summary>Saves only the shortcut fields for a profile (called from Key Binds inline capture).</summary>
        public void SaveProfileShortcut(AudioProfile profile,
            string? gesture,
            string? mouseButton)
        {
            profile.ShortcutGesture     = string.IsNullOrWhiteSpace(gesture) ? null : KeyBindsViewModel.NormalizeGesture(gesture!);
            profile.ShortcutMouseButton = mouseButton;
            ApplyLegacyShortcutFields(profile);
            profile.IsCapturingShortcut = false;

            var profiles = _storageService.LoadProfiles();
            var idx = profiles.FindIndex(p => p.Id == profile.Id);
            if (idx >= 0)
            {
                profiles[idx].ShortcutGesture     = profile.ShortcutGesture;
                profiles[idx].ShortcutKey         = profile.ShortcutKey;
                profiles[idx].ShortcutModifiers   = profile.ShortcutModifiers;
                profiles[idx].ShortcutMouseButton = profile.ShortcutMouseButton;
                _storageService.SaveProfiles(profiles);
            }

            UnregisterProfileHotkey(profile);
            RegisterProfileHotkey(profile);
        }

        /// <summary>Clears the shortcut for a profile (called from Key Binds clear button).</summary>
        public void ClearProfileShortcut(AudioProfile profile)
        {
            UnregisterProfileHotkey(profile);
            profile.ShortcutKey         = null;
            profile.ShortcutModifiers   = null;
            profile.ShortcutGesture     = null;
            profile.ShortcutMouseButton = null;
            profile.IsCapturingShortcut = false;

            var profiles = _storageService.LoadProfiles();
            var idx = profiles.FindIndex(p => p.Id == profile.Id);
            if (idx >= 0)
            {
                profiles[idx].ShortcutGesture     = null;
                profiles[idx].ShortcutKey         = null;
                profiles[idx].ShortcutModifiers   = null;
                profiles[idx].ShortcutMouseButton = null;
                _storageService.SaveProfiles(profiles);
            }
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
