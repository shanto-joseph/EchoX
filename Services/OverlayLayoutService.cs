using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EchoX.Models;

namespace EchoX.Services
{
    public static class OverlayLayoutService
    {
        public static Screen GetPreferredScreen()
        {
            var appWindow = System.Windows.Application.Current?.MainWindow;
            if (appWindow != null)
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(appWindow).Handle;
                if (handle != IntPtr.Zero)
                    return Screen.FromHandle(handle);
            }

            return Screen.FromPoint(Cursor.Position);
        }

        public static System.Windows.Point GetPosition(AppSettings settings, string overlayId, double overlayWidth, double overlayHeight, Screen? preferredScreen = null)
        {
            var targetScreen = preferredScreen ?? GetPreferredScreen();
            var placement = settings.OverlayPlacements.FirstOrDefault(p =>
                string.Equals(p.OverlayId, overlayId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.MonitorDeviceName, targetScreen.DeviceName, StringComparison.OrdinalIgnoreCase));

            return placement == null
                ? GetDefaultPosition(overlayId, targetScreen, overlayWidth, overlayHeight)
                : ToPoint(targetScreen.WorkingArea, placement.XRatio, placement.YRatio, overlayWidth, overlayHeight);
        }

        public static System.Windows.Point GetDefaultPosition(string overlayId, Screen screen, double overlayWidth, double overlayHeight)
        {
            var area = screen.WorkingArea;
            if (string.Equals(overlayId, OverlayIds.MuteIndicator, StringComparison.OrdinalIgnoreCase))
            {
                return new System.Windows.Point(
                    area.Left + 8,
                    area.Top + Math.Max(0, (area.Height - overlayHeight) / 2));
            }

            return new System.Windows.Point(
                area.Right - overlayWidth - 16,
                area.Top + 16);
        }

        public static void SavePlacement(AppSettings settings, string overlayId, Screen screen, double left, double top, double overlayWidth, double overlayHeight)
        {
            var existing = settings.OverlayPlacements.FirstOrDefault(p =>
                string.Equals(p.OverlayId, overlayId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.MonitorDeviceName, screen.DeviceName, StringComparison.OrdinalIgnoreCase));

            var workingArea = screen.WorkingArea;
            var widthRange = Math.Max(1d, workingArea.Width - overlayWidth);
            var heightRange = Math.Max(1d, workingArea.Height - overlayHeight);
            var xRatio = Clamp((left - workingArea.Left) / widthRange);
            var yRatio = Clamp((top - workingArea.Top) / heightRange);

            if (existing == null)
            {
                settings.OverlayPlacements.Add(new OverlayPlacement
                {
                    OverlayId = overlayId,
                    MonitorDeviceName = screen.DeviceName,
                    XRatio = xRatio,
                    YRatio = yRatio
                });
                return;
            }

            existing.XRatio = xRatio;
            existing.YRatio = yRatio;
        }

        public static void ResetPlacement(AppSettings settings, string overlayId, Screen screen)
        {
            settings.OverlayPlacements.RemoveAll(p =>
                string.Equals(p.OverlayId, overlayId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.MonitorDeviceName, screen.DeviceName, StringComparison.OrdinalIgnoreCase));
        }

        public static double Clamp(double value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }

        private static System.Windows.Point ToPoint(Rectangle area, double xRatio, double yRatio, double overlayWidth, double overlayHeight)
        {
            var widthRange = Math.Max(0d, area.Width - overlayWidth);
            var heightRange = Math.Max(0d, area.Height - overlayHeight);

            return new System.Windows.Point(
                area.Left + (Clamp(xRatio) * widthRange),
                area.Top + (Clamp(yRatio) * heightRange));
        }
    }
}
