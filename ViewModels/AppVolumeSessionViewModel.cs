using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EchoX.Models;

namespace EchoX.ViewModels
{
    public class AppVolumeSessionViewModel : ViewModelBase
    {
        private readonly Action<string, string, double> _setVolume;
        private readonly Action<string, string, bool> _setMute;
        private bool _isApplyingSnapshot;
        private double _volume;
        private bool _isMuted;
        private string _appName = string.Empty;
        private string _subtitle = string.Empty;
        private ImageSource? _iconSource;
        private bool _hasIcon;
        private string _lastIconPath = string.Empty;
        private string _lastExecutablePath = string.Empty;
        private int _lastProcessId = -1;
        private int _iconLoadVersion;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconEx(
            string lpszFile,
            int nIconIndex,
            IntPtr[]? phiconLarge,
            IntPtr[]? phiconSmall,
            uint nIcons);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            out SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtr", CharSet = CharSet.Auto)]
        private static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetClassLong", CharSet = CharSet.Auto)]
        private static extern uint GetClassLongPtr32(IntPtr hWnd, int nIndex);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const int WM_GETICON = 0x007F;
        private const int ICON_SMALL2 = 2;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const int GCLP_HICON = -14;
        private const int GCLP_HICONSM = -34;

        public AppVolumeSessionViewModel(
            AppAudioSessionInfo session,
            Action<string, string, double> setVolume,
            Action<string, string, bool> setMute)
        {
            DeviceId = session.DeviceId;
            SessionId = session.SessionId;
            _setVolume = setVolume;
            _setMute = setMute;
            ApplySnapshot(session);
        }

        public string DeviceId { get; }
        public string SessionId { get; }

        public string AppName
        {
            get => _appName;
            private set => SetProperty(ref _appName, value);
        }

        public string Subtitle
        {
            get => _subtitle;
            private set => SetProperty(ref _subtitle, value);
        }

        public ImageSource? IconSource
        {
            get => _iconSource;
            private set => SetProperty(ref _iconSource, value);
        }

        public bool HasIcon
        {
            get => _hasIcon;
            private set => SetProperty(ref _hasIcon, value);
        }

        public string FallbackGlyph => IsMuted ? "\uE198" : "\uE189";

        public double Volume
        {
            get => _volume;
            set
            {
                if (!SetProperty(ref _volume, value))
                    return;

                OnPropertyChanged(nameof(VolumeText));
                if (!_isApplyingSnapshot)
                    _setVolume(DeviceId, SessionId, value);
            }
        }

        public string VolumeText => $"{Volume:0}%";

        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                if (!SetProperty(ref _isMuted, value))
                    return;

                OnPropertyChanged(nameof(MuteGlyph));
                if (!_isApplyingSnapshot)
                    _setMute(DeviceId, SessionId, value);
            }
        }

        public string MuteGlyph => IsMuted ? "\uE198" : "\uE15D";

        public void ApplySnapshot(AppAudioSessionInfo session)
        {
            _isApplyingSnapshot = true;
            try
            {
                AppName = session.DisplayName;
                Subtitle = string.Empty;
                if (!string.Equals(_lastIconPath, session.IconPath, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(_lastExecutablePath, session.ExecutablePath, StringComparison.OrdinalIgnoreCase) ||
                    _lastProcessId != session.ProcessId)
                {
                    _lastIconPath = session.IconPath ?? string.Empty;
                    _lastExecutablePath = session.ExecutablePath ?? string.Empty;
                    _lastProcessId = session.ProcessId;
                    IconSource = null;
                    HasIcon = false;
                    _ = LoadIconAsync(_lastIconPath, _lastExecutablePath, _lastProcessId);
                }
                OnPropertyChanged(nameof(FallbackGlyph));
                Volume = session.Volume;
                IsMuted = session.IsMuted;
            }
            finally
            {
                _isApplyingSnapshot = false;
            }
        }

        private async Task LoadIconAsync(string iconPath, string executablePath, int processId)
        {
            var version = ++_iconLoadVersion;
            var icon = await Task.Run(() => LoadIcon(iconPath, executablePath, processId));
            await System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (version != _iconLoadVersion)
                    return;

                IconSource = icon;
                HasIcon = icon != null;
            }));
        }

        private static ImageSource? LoadIcon(string iconPath, string executablePath, int processId)
        {
            var iconSource = LoadFromIconReference(iconPath);
            if (iconSource != null)
                return iconSource;

            iconSource = LoadFromFile(executablePath);
            if (iconSource != null)
                return iconSource;

            var processPath = ResolveProcessPath(processId);
            if (!string.IsNullOrWhiteSpace(processPath) &&
                !string.Equals(processPath, executablePath, StringComparison.OrdinalIgnoreCase))
            {
                iconSource = LoadFromFile(processPath);
                if (iconSource != null)
                    return iconSource;
            }

            return LoadFromProcessWindow(processId);
        }

        private static ImageSource? LoadFromIconReference(string iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath))
                return null;

            var cleaned = iconPath.Trim().Trim('"');
            if (cleaned.StartsWith("@"))
                cleaned = cleaned.Substring(1);

            var filePath = cleaned;
            var iconIndex = 0;
            var commaIndex = cleaned.LastIndexOf(',');
            if (commaIndex > 1)
            {
                filePath = cleaned.Substring(0, commaIndex).Trim().Trim('"');
                int.TryParse(cleaned.Substring(commaIndex + 1).Trim(), out iconIndex);
            }

            if (!File.Exists(filePath))
                return null;

            try
            {
                var smallIcons = new[] { IntPtr.Zero };
                var extracted = ExtractIconEx(filePath, iconIndex, null, smallIcons, 1);
                if (extracted == 0 || smallIcons[0] == IntPtr.Zero)
                    return null;

                try
                {
                    var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                        smallIcons[0],
                        System.Windows.Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(20, 20));
                    bitmapSource.Freeze();
                    return bitmapSource;
                }
                finally
                {
                    DestroyIcon(smallIcons[0]);
                }
            }
            catch
            {
                return null;
            }
        }

        private static ImageSource? LoadFromFile(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                return null;

            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(executablePath);
                if (icon == null)
                    return null;

                var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(20, 20));
                bitmapSource.Freeze();
                return bitmapSource;
            }
            catch
            {
            }

            try
            {
                var shellInfo = new SHFILEINFO();
                var result = SHGetFileInfo(
                    executablePath,
                    0,
                    out shellInfo,
                    (uint)Marshal.SizeOf<SHFILEINFO>(),
                    SHGFI_ICON | SHGFI_SMALLICON);

                if (result == IntPtr.Zero || shellInfo.hIcon == IntPtr.Zero)
                    return null;

                try
                {
                    var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                        shellInfo.hIcon,
                        System.Windows.Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(20, 20));
                    bitmapSource.Freeze();
                    return bitmapSource;
                }
                finally
                {
                    DestroyIcon(shellInfo.hIcon);
                }
            }
            catch
            {
                return null;
            }
        }

        private static string ResolveProcessPath(int processId)
        {
            if (processId <= 0)
                return string.Empty;

            try
            {
                using var process = Process.GetProcessById(processId);
                return process.MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static ImageSource? LoadFromProcessWindow(int processId)
        {
            if (processId <= 0)
                return null;

            try
            {
                using var process = Process.GetProcessById(processId);
                var handle = process.MainWindowHandle;
                if (handle == IntPtr.Zero)
                    return null;

                var iconHandle = SendMessage(handle, WM_GETICON, new IntPtr(ICON_SMALL2), IntPtr.Zero);
                if (iconHandle == IntPtr.Zero)
                    iconHandle = SendMessage(handle, WM_GETICON, new IntPtr(ICON_SMALL), IntPtr.Zero);
                if (iconHandle == IntPtr.Zero)
                    iconHandle = SendMessage(handle, WM_GETICON, new IntPtr(ICON_BIG), IntPtr.Zero);
                if (iconHandle == IntPtr.Zero)
                    iconHandle = GetClassLongPtr(handle, GCLP_HICONSM);
                if (iconHandle == IntPtr.Zero)
                    iconHandle = GetClassLongPtr(handle, GCLP_HICON);
                if (iconHandle == IntPtr.Zero)
                    return null;

                var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                    iconHandle,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(20, 20));
                bitmapSource.Freeze();
                return bitmapSource;
            }
            catch
            {
                return null;
            }
        }

        private static IntPtr GetClassLongPtr(IntPtr handle, int index)
        {
            return IntPtr.Size == 8
                ? GetClassLongPtr64(handle, index)
                : new IntPtr(unchecked((int)GetClassLongPtr32(handle, index)));
        }
    }
}
