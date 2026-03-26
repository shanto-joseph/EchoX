using System;
using System.Reflection;
using Microsoft.Win32;

namespace EchoX.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private bool _launchWithWindows;
        private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "EchoX";

        public SettingsViewModel()
        {
            _launchWithWindows = GetStartupStatus();
        }

        public bool LaunchWithWindows
        {
            get => _launchWithWindows;
            set
            {
                if (SetProperty(ref _launchWithWindows, value))
                {
                    SetStartup(value);
                }
            }
        }

        private bool GetStartupStatus()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    return key?.GetValue(AppName) != null;
                }
            }
            catch { return false; }
        }

        private void SetStartup(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (enable)
                    {
                        string appPath = Assembly.GetExecutingAssembly().Location;
                        // For .NET Core/5+ publish apps, this might need adjustment to the .exe path
                        // but since we are net48, this is simple.
                        key.SetValue(AppName, $"\"{appPath}\"");
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set startup: {ex.Message}");
            }
        }
    }
}