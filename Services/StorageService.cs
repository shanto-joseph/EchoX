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

        public StorageService()
        {
            _filePath  = Path.Combine(_folderPath, "profiles.json");
            _cachePath = Path.Combine(_folderPath, "cache.json");

            if (!Directory.Exists(_folderPath))
            {
                Directory.CreateDirectory(_folderPath);
            }
        }

        public void SaveProfiles(List<AudioProfile> profiles)
        {
            string json = JsonConvert.SerializeObject(profiles, Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }

        public List<AudioProfile> LoadProfiles()
        {
            if (!File.Exists(_filePath)) return new List<AudioProfile>(); 
            try {
                string json = File.ReadAllText(_filePath);
                return JsonConvert.DeserializeObject<List<AudioProfile>>(json) ?? new List<AudioProfile>();
            } catch { return new List<AudioProfile>(); }
        }

        public void SaveDeviceCache(DeviceCache cache)
        {
            try {
                string json = JsonConvert.SerializeObject(cache, Formatting.Indented);
                File.WriteAllText(_cachePath, json);
            } catch { }
        }

        public DeviceCache LoadDeviceCache()
        {
            if (!File.Exists(_cachePath)) return new DeviceCache();
            try {
                string json = File.ReadAllText(_cachePath);
                return JsonConvert.DeserializeObject<DeviceCache>(json) ?? new DeviceCache();
            } catch { return new DeviceCache(); }
        }
    }
}