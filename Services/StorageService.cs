using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using EchoX.Models;

namespace EchoX.Services
{
    public class StorageService
    {
        private readonly string _folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EchoX");
        private readonly string _filePath;
        private readonly string _cachePath;
        private readonly string _keyBindsPath;
        private List<AudioProfile>? _profilesCache;
        private DeviceCache? _deviceCache;

        public StorageService()
        {
            _filePath     = Path.Combine(_folderPath, "profiles.json");
            _cachePath    = Path.Combine(_folderPath, "cache.json");
            _keyBindsPath = Path.Combine(_folderPath, "keybinds.json");

            if (!Directory.Exists(_folderPath))
            {
                Directory.CreateDirectory(_folderPath);
            }
        }

        public void SaveProfiles(List<AudioProfile> profiles)
        {
            _profilesCache = null; // invalidate cache so next load reads fresh
            string json = JsonConvert.SerializeObject(profiles, Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }

        public List<AudioProfile> LoadProfiles()
        {
            if (_profilesCache != null) return _profilesCache;
            if (!File.Exists(_filePath)) return new List<AudioProfile>(); 
            try {
                string json = File.ReadAllText(_filePath);
                _profilesCache = JsonConvert.DeserializeObject<List<AudioProfile>>(json) ?? new List<AudioProfile>();
                return _profilesCache;
            } catch { return new List<AudioProfile>(); }
        }

        public void SaveDeviceCache(DeviceCache cache)
        {
            _deviceCache = cache;
            try {
                string json = JsonConvert.SerializeObject(cache, Formatting.Indented);
                File.WriteAllText(_cachePath, json);
            } catch { }
        }

        public DeviceCache LoadDeviceCache()
        {
            if (_deviceCache != null) return _deviceCache;
            if (!File.Exists(_cachePath)) return new DeviceCache();
            try {
                string json = File.ReadAllText(_cachePath);
                _deviceCache = JsonConvert.DeserializeObject<DeviceCache>(json) ?? new DeviceCache();
                return _deviceCache;
            } catch { return new DeviceCache(); }
        }

        public void SaveActiveProfileId(string id)
        {
            string path = Path.Combine(_folderPath, "active.dat");
            try { File.WriteAllText(path, id); } catch { }
        }

        public string LoadActiveProfileId()
        {
            string path = Path.Combine(_folderPath, "active.dat");
            if (!File.Exists(path)) return string.Empty;
            try { return File.ReadAllText(path).Trim(); } catch { return string.Empty; }
        }

        public void SaveKeyBinds(EchoX.Models.KeyBindsSettings settings)
        {
            try { File.WriteAllText(_keyBindsPath, JsonConvert.SerializeObject(settings, Formatting.Indented)); } catch { }
        }

        public EchoX.Models.KeyBindsSettings? LoadKeyBinds()
        {
            if (!File.Exists(_keyBindsPath)) return null;
            try { return JsonConvert.DeserializeObject<EchoX.Models.KeyBindsSettings>(File.ReadAllText(_keyBindsPath)); } catch { return null; }
        }
    }
}