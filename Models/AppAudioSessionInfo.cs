namespace EchoX.Models
{
    public class AppAudioSessionInfo
    {
        public string SessionId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public double Volume { get; set; }
        public bool IsMuted { get; set; }
        public bool IsSystemSession { get; set; }
    }
}
