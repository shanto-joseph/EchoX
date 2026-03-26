using System.Collections.Generic;

namespace EchoX.Models
{
    public class DeviceCache
    {
        public List<AudioDevice> InputDevices { get; set; } = new List<AudioDevice>();
        public List<AudioDevice> OutputDevices { get; set; } = new List<AudioDevice>();
        public string? LastInputId { get; set; }
        public string? LastOutputId { get; set; }
    }
}
