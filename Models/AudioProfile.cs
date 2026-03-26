using System;

namespace EchoX.Models
{
    public class AudioProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString(); 
        public string Name { get; set; } = "New Profile";
        
        // Notice the question marks here! It tells C# this can safely be empty.
        public string? OutputDeviceId { get; set; } 
        public string? InputDeviceId { get; set; }
        public string? CallDeviceId { get; set; }
        
        public int VolumeLevel { get; set; } = 100; 
    }
}