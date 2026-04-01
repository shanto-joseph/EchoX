using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using EchoX.Services;
using Newtonsoft.Json.Linq;

namespace EchoX.ViewModels
{
    public class AboutViewModel : ViewModelBase
    {
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/shanto-joseph/EchoX/releases/latest";
        private const string InstallerAssetNamePrefix = "EchoX-Setup-";

        public string AppVersion =>
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        private string _lastChecked = "Never";
        public string LastChecked
        {
            get => _lastChecked;
            set => SetProperty(ref _lastChecked, value);
        }

        private string _updateStatusText = "Checking...";
        public string UpdateStatusText
        {
            get => _updateStatusText;
            private set => SetProperty(ref _updateStatusText, value);
        }

        private string _updateActionText = "Check now";
        public string UpdateActionText
        {
            get => _updateActionText;
            private set => SetProperty(ref _updateActionText, value);
        }

        private string _updateStatusDetail = "Checks GitHub Releases for the latest EchoX build.";
        public string UpdateStatusDetail
        {
            get => _updateStatusDetail;
            private set => SetProperty(ref _updateStatusDetail, value);
        }

        private bool _isCheckingForUpdates;
        public bool IsCheckingForUpdates
        {
            get => _isCheckingForUpdates;
            private set
            {
                if (SetProperty(ref _isCheckingForUpdates, value))
                {
                    NotifyTopUpdateUiStateChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private bool _isUpdateAvailable;
        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            private set
            {
                if (SetProperty(ref _isUpdateAvailable, value))
                {
                    NotifyTopUpdateUiStateChanged();
                }
            }
        }

        private string? _latestVersion;
        public string? LatestVersion
        {
            get => _latestVersion;
            private set => SetProperty(ref _latestVersion, value);
        }

        private string? _releaseUrl;
        public string? ReleaseUrl
        {
            get => _releaseUrl;
            private set => SetProperty(ref _releaseUrl, value);
        }

        private string? _downloadUrl;
        private string? _downloadFileName;
        private string? _downloadedInstallerPath;

        private bool _isDownloadingUpdate;
        public bool IsDownloadingUpdate
        {
            get => _isDownloadingUpdate;
            private set
            {
                if (SetProperty(ref _isDownloadingUpdate, value))
                {
                    NotifyTopUpdateUiStateChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private int _downloadProgressPercentage;
        public int DownloadProgressPercentage
        {
            get => _downloadProgressPercentage;
            private set => SetProperty(ref _downloadProgressPercentage, value);
        }

        private bool _isUpdateDownloaded;
        public bool IsUpdateDownloaded
        {
            get => _isUpdateDownloaded;
            private set
            {
                if (SetProperty(ref _isUpdateDownloaded, value))
                {
                    NotifyTopUpdateUiStateChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private bool _isInstallingUpdate;
        public bool IsInstallingUpdate
        {
            get => _isInstallingUpdate;
            private set
            {
                if (SetProperty(ref _isInstallingUpdate, value))
                {
                    NotifyTopUpdateUiStateChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private bool _isTopUpdateActionVisible;
        public bool IsTopUpdateActionVisible
        {
            get => _isTopUpdateActionVisible;
            private set
            {
                if (SetProperty(ref _isTopUpdateActionVisible, value))
                    NotifyTopUpdateUiStateChanged();
            }
        }

        public ICommand CheckForUpdateCommand { get; }
        public ICommand RevealTopUpdateActionCommand { get; }
        public bool IsUpdateActionAvailable => IsUpdateAvailable || IsUpdateDownloaded;
        public bool ShowTopUpdateStatus => IsCheckingForUpdates || IsDownloadingUpdate || IsInstallingUpdate || IsUpdateAvailable || IsUpdateDownloaded;
        public bool ShowTopUpdateStatusPill => ShowTopUpdateStatus && !IsDownloadingUpdate && !IsUpdateDownloaded && !IsTopUpdateActionVisible;
        public bool ShowTopUpdateProgress => IsDownloadingUpdate;
        public bool ShowTopUpdateActionButton => (IsTopUpdateActionVisible && IsUpdateAvailable) || IsUpdateDownloaded;
        public bool IsTopUpdateClickable => IsUpdateAvailable;
        public string TopUpdateButtonText => IsUpdateDownloaded ? "Install" : "Update";

        private readonly StorageService? _storageService;

        public AboutViewModel(StorageService? storageService = null)
        {
            _storageService = storageService;

            if (_storageService != null)
                _lastChecked = _storageService.LoadAppSettings().LastUpdateChecked;

            UpdateStatusDetail = _lastChecked == "Never"
                ? "Checks GitHub Releases."
                : $"Last checked: {_lastChecked}";

            CheckForUpdateCommand = new RelayCommand(
                () =>
                {
                    if (IsUpdateDownloaded && !string.IsNullOrWhiteSpace(_downloadedInstallerPath))
                    {
                        InstallDownloadedUpdate();
                        return;
                    }

                    if (IsUpdateAvailable && !string.IsNullOrWhiteSpace(_downloadUrl))
                    {
                        _ = DownloadUpdateAsync();
                        return;
                    }

                    if (IsUpdateAvailable && !string.IsNullOrWhiteSpace(ReleaseUrl))
                    {
                        OpenReleasePage();
                        return;
                    }

                    _ = CheckForUpdatesAsync(false);
                },
                () => !IsCheckingForUpdates && !IsDownloadingUpdate && !IsInstallingUpdate);

            RevealTopUpdateActionCommand = new RelayCommand(
                () => IsTopUpdateActionVisible = IsUpdateAvailable,
                () => IsUpdateAvailable && !IsDownloadingUpdate && !IsInstallingUpdate && !IsUpdateDownloaded);
        }

        public Task CheckForUpdatesAsync(bool silent)
        {
            return CheckForUpdatesCoreAsync(silent);
        }

        private async Task CheckForUpdatesCoreAsync(bool silent)
        {
            try
            {
                IsCheckingForUpdates = true;
                IsTopUpdateActionVisible = false;
                UpdateStatusText = "Checking...";
                UpdateActionText = "Checking...";
                UpdateStatusDetail = "Checking GitHub release...";
                IsUpdateDownloaded = false;
                DownloadProgressPercentage = 0;
                _downloadedInstallerPath = null;

                var payload = await DownloadLatestReleasePayloadAsync().ConfigureAwait(true);
                var json = JObject.Parse(payload);

                var latestTag = (json["tag_name"]?.ToString() ?? string.Empty).Trim();
                var latestVersion = NormalizeVersionLabel(latestTag);
                var htmlUrl = json["html_url"]?.ToString();
                var asset = FindInstallerAsset(json);

                ReleaseUrl = htmlUrl;
                LatestVersion = latestVersion;
                _downloadUrl = asset?.BrowserDownloadUrl;
                _downloadFileName = asset?.Name;
                StampLastChecked();

                if (TryParseVersion(latestVersion, out var latest) && TryParseVersion(AppVersion, out var current) && latest > current)
                {
                    IsUpdateAvailable = true;
                    UpdateStatusText = "Update available";
                    UpdateActionText = !string.IsNullOrWhiteSpace(_downloadUrl) ? "Download" : "Open release";
                    UpdateStatusDetail = !string.IsNullOrWhiteSpace(_downloadUrl)
                        ? $"Update ready: v{latestVersion}"
                        : $"Update ready: v{latestVersion}. Open release page.";
                    return;
                }

                IsUpdateAvailable = false;
                UpdateStatusText = "Up to date";
                UpdateActionText = "Check now";
                UpdateStatusDetail = string.IsNullOrWhiteSpace(latestVersion)
                    ? "You are up to date."
                    : $"Up to date: v{latestVersion}";
            }
            catch
            {
                IsUpdateAvailable = false;
                UpdateStatusText = silent ? "Updates unavailable" : "Check failed";
                UpdateActionText = "Try again";
                UpdateStatusDetail = "Could not reach GitHub.";
            }
            finally
            {
                IsCheckingForUpdates = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task DownloadUpdateAsync()
        {
            if (string.IsNullOrWhiteSpace(_downloadUrl) || string.IsNullOrWhiteSpace(_downloadFileName))
            {
                OpenReleasePage();
                return;
            }

            try
            {
                IsDownloadingUpdate = true;
                IsTopUpdateActionVisible = false;
                DownloadProgressPercentage = 0;
                UpdateStatusText = "Downloading...";
                UpdateActionText = "Downloading...";
                UpdateStatusDetail = "Starting download...";

                var updatesFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EchoX",
                    "Updates");

                Directory.CreateDirectory(updatesFolder);

                var destinationPath = Path.Combine(updatesFolder, _downloadFileName);
                var tempPath = destinationPath + ".download";

                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                using var client = CreateDownloadClient();
                client.DownloadProgressChanged += (_, args) =>
                {
                    DownloadProgressPercentage = args.ProgressPercentage;
                    UpdateStatusDetail = $"Downloading... {args.ProgressPercentage}%";
                };

                await client.DownloadFileTaskAsync(new Uri(_downloadUrl), tempPath).ConfigureAwait(true);

                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);

                File.Move(tempPath, destinationPath);
                _downloadedInstallerPath = destinationPath;
                IsUpdateDownloaded = true;
                DownloadProgressPercentage = 100;
                UpdateStatusText = "Ready to install";
                UpdateActionText = "Install";
                UpdateStatusDetail = "Download complete. Install ready.";
            }
            catch
            {
                IsUpdateDownloaded = false;
                IsTopUpdateActionVisible = false;
                DownloadProgressPercentage = 0;
                _downloadedInstallerPath = null;
                UpdateStatusText = "Download failed";
                UpdateActionText = "Try again";
                UpdateStatusDetail = "Download failed.";
            }
            finally
            {
                IsDownloadingUpdate = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void InstallDownloadedUpdate()
        {
            if (string.IsNullOrWhiteSpace(_downloadedInstallerPath) || !File.Exists(_downloadedInstallerPath))
            {
                IsUpdateDownloaded = false;
                UpdateStatusText = "Installer missing";
                UpdateActionText = "Download";
                UpdateStatusDetail = "Installer missing. Download again.";
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(_downloadedInstallerPath))
                    throw new InvalidOperationException("Installer path is missing.");
                var installerPath = _downloadedInstallerPath!;

                IsInstallingUpdate = true;
                UpdateStatusText = "Launching installer...";
                UpdateActionText = "Launching...";
                UpdateStatusDetail = "Opening installer...";
                IsTopUpdateActionVisible = false;

                var installerProcess = Process.Start(new ProcessStartInfo(installerPath) { UseShellExecute = true });
                if (installerProcess != null)
                    StartInstallerCleanup(installerProcess.Id, installerPath);

                System.Windows.Application.Current?.Shutdown();
            }
            catch
            {
                UpdateStatusText = "Install failed";
                UpdateActionText = "Install";
                UpdateStatusDetail = "Could not launch installer.";
            }
            finally
            {
                IsInstallingUpdate = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void StampLastChecked()
        {
            LastChecked = DateTime.Now.ToString("MMM d, yyyy  h:mm tt");

            if (_storageService != null)
            {
                var settings = _storageService.LoadAppSettings();
                settings.LastUpdateChecked = LastChecked;
                _storageService.SaveAppSettings(settings);
            }
        }

        private void OpenReleasePage()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(ReleaseUrl))
                    Process.Start(new ProcessStartInfo(ReleaseUrl) { UseShellExecute = true });
            }
            catch
            {
            }
        }

        private static async Task<string> DownloadLatestReleasePayloadAsync()
        {
            using var client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "EchoX-Updater/1.0";
            client.Encoding = System.Text.Encoding.UTF8;
            return await client.DownloadStringTaskAsync(new Uri(LatestReleaseApiUrl)).ConfigureAwait(true);
        }

        private static WebClient CreateDownloadClient()
        {
            var client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "EchoX-Updater/1.0";
            return client;
        }

        private static void StartInstallerCleanup(int installerProcessId, string installerPath)
        {
            var escapedPath = installerPath.Replace("'", "''");
            var cleanupScript =
                $"Wait-Process -Id {installerProcessId}; " +
                "Start-Sleep -Seconds 2; " +
                $"Remove-Item -LiteralPath '{escapedPath}' -Force -ErrorAction SilentlyContinue";

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{cleanupScript}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        private static ReleaseAssetInfo? FindInstallerAsset(JObject releaseJson)
        {
            var assets = releaseJson["assets"] as JArray;
            if (assets == null)
                return null;

            var installerAsset = assets
                .OfType<JObject>()
                .Select(asset => new ReleaseAssetInfo
                {
                    Name = asset["name"]?.ToString() ?? string.Empty,
                    BrowserDownloadUrl = asset["browser_download_url"]?.ToString() ?? string.Empty
                })
                .FirstOrDefault(asset =>
                    asset.Name.StartsWith(InstallerAssetNamePrefix, StringComparison.OrdinalIgnoreCase) &&
                    asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            return installerAsset;
        }

        private static string NormalizeVersionLabel(string rawVersion)
        {
            if (string.IsNullOrWhiteSpace(rawVersion))
                return string.Empty;

            return rawVersion.Trim().TrimStart('v', 'V');
        }

        private static bool TryParseVersion(string? rawVersion, out Version version)
        {
            version = new Version(0, 0);
            if (string.IsNullOrWhiteSpace(rawVersion))
                return false;

            var normalized = NormalizeVersionLabel(rawVersion!);
            return Version.TryParse(normalized, out version);
        }

        private void NotifyTopUpdateUiStateChanged()
        {
            OnPropertyChanged(nameof(IsUpdateActionAvailable));
            OnPropertyChanged(nameof(ShowTopUpdateStatus));
            OnPropertyChanged(nameof(ShowTopUpdateStatusPill));
            OnPropertyChanged(nameof(ShowTopUpdateProgress));
            OnPropertyChanged(nameof(ShowTopUpdateActionButton));
            OnPropertyChanged(nameof(IsTopUpdateClickable));
            OnPropertyChanged(nameof(TopUpdateButtonText));
        }

        private sealed class ReleaseAssetInfo
        {
            public string Name { get; set; } = string.Empty;
            public string BrowserDownloadUrl { get; set; } = string.Empty;
        }
    }
}
