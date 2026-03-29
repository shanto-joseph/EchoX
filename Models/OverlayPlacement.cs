using System.Collections.Generic;

namespace EchoX.Models
{
    public static class OverlayIds
    {
        public const string NotificationPopup = "notification-popup";
        public const string MuteIndicator = "mute-indicator";
    }

    public class OverlayPlacement
    {
        public string OverlayId { get; set; } = string.Empty;
        public string MonitorDeviceName { get; set; } = string.Empty;
        public double XRatio { get; set; }
        public double YRatio { get; set; }
    }

    public class OverlaySettingsSnapshot
    {
        public List<OverlayPlacement> OverlayPlacements { get; set; } = new List<OverlayPlacement>();
    }
}
