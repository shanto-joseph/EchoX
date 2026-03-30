using System;
using System.Diagnostics;
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
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _isUpdateAvailable;
        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            private set => SetProperty(ref _isUpdateAvailable, value);
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

        public ICommand CheckForUpdateCommand { get; }

        private readonly StorageService? _storageService;

        public AboutViewModel(StorageService? storageService = null)
        {
            _storageService = storageService;

            if (_storageService != null)
                _lastChecked = _storageService.LoadAppSettings().LastUpdateChecked;

            UpdateStatusDetail = _lastChecked == "Never"
                ? "Checks GitHub Releases for the latest EchoX build."
                : $"Last checked: {_lastChecked}";

            CheckForUpdateCommand = new RelayCommand(
                () =>
                {
                    if (IsUpdateAvailable && !string.IsNullOrWhiteSpace(ReleaseUrl))
                    {
                        OpenReleasePage();
                        return;
                    }

                    _ = CheckForUpdatesAsync(false);
                },
                () => !IsCheckingForUpdates);
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
                UpdateStatusText = "Checking...";
                UpdateActionText = "Checking...";
                UpdateStatusDetail = "Looking for the latest EchoX release...";

                var payload = await DownloadLatestReleasePayloadAsync().ConfigureAwait(true);
                var json = JObject.Parse(payload);

                var latestTag = (json["tag_name"]?.ToString() ?? string.Empty).Trim();
                var latestVersion = NormalizeVersionLabel(latestTag);
                var htmlUrl = json["html_url"]?.ToString();

                ReleaseUrl = htmlUrl;
                LatestVersion = latestVersion;
                StampLastChecked();

                if (TryParseVersion(latestVersion, out var latest) && TryParseVersion(AppVersion, out var current) && latest > current)
                {
                    IsUpdateAvailable = true;
                    UpdateStatusText = $"Update available";
                    UpdateActionText = "Download";
                    UpdateStatusDetail = $"v{latestVersion} is available. Click to open the release page.";
                    return;
                }

                IsUpdateAvailable = false;
                UpdateStatusText = "Up to date";
                UpdateActionText = "Check now";
                UpdateStatusDetail = $"You're on the latest version{(string.IsNullOrWhiteSpace(latestVersion) ? string.Empty : $" (v{latestVersion})")}.";
            }
            catch
            {
                IsUpdateAvailable = false;
                UpdateStatusText = silent ? "Updates unavailable" : "Check failed";
                UpdateActionText = "Try again";
                UpdateStatusDetail = "EchoX couldn't reach the update server right now.";
            }
            finally
            {
                IsCheckingForUpdates = false;
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
    }
}
