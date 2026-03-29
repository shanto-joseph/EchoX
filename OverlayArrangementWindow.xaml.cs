using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using EchoX.Models;
using EchoX.Services;
using EchoX.ViewModels;

namespace EchoX
{
    public partial class OverlayArrangementWindow : Window
    {
        private readonly SettingsViewModel _settingsViewModel;
        private readonly AppSettings _workingSettings;
        private readonly Screen _screen;
        private FrameworkElement? _draggingElement;
        private System.Windows.Point _dragOffset;

        public OverlayArrangementWindow(SettingsViewModel settingsViewModel)
        {
            InitializeComponent();
            _settingsViewModel = settingsViewModel;
            _workingSettings = CloneSettings(settingsViewModel.GetAppSettingsSnapshot());
            _screen = OverlayLayoutService.GetPreferredScreen();

            Left = _screen.WorkingArea.Left;
            Top = _screen.WorkingArea.Top;
            Width = _screen.WorkingArea.Width;
            Height = _screen.WorkingArea.Height;

            Loaded += OnLoaded;
            SizeChanged += (s, e) => UpdateStaticPanels();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            MonitorText.Text = $"Editing {FormatMonitorName(_screen.DeviceName)}";
            PositionPreview(NotificationPreview, OverlayIds.NotificationPopup);
            PositionPreview(MutePreview, OverlayIds.MuteIndicator);
            UpdateStaticPanels();
        }

        private static AppSettings CloneSettings(AppSettings source)
        {
            return new AppSettings
            {
                NotificationType = source.NotificationType,
                ShowMuteIndicator = source.ShowMuteIndicator,
                LastUpdateChecked = source.LastUpdateChecked,
                OverlayPlacements = source.OverlayPlacements
                    .Select(p => new OverlayPlacement
                    {
                        OverlayId = p.OverlayId,
                        MonitorDeviceName = p.MonitorDeviceName,
                        XRatio = p.XRatio,
                        YRatio = p.YRatio
                    })
                    .ToList()
            };
        }

        private void PositionPreview(FrameworkElement element, string overlayId)
        {
            element.UpdateLayout();
            var position = OverlayLayoutService.GetPosition(
                _workingSettings,
                overlayId,
                element.ActualWidth > 0 ? element.ActualWidth : element.Width,
                element.ActualHeight > 0 ? element.ActualHeight : element.Height,
                _screen);

            Canvas.SetLeft(element, position.X - _screen.WorkingArea.Left);
            Canvas.SetTop(element, position.Y - _screen.WorkingArea.Top);
        }

        private void UpdateStaticPanels()
        {
            Canvas.SetLeft(ActionBar, Math.Max(24, (DesignCanvas.ActualWidth - ActionBar.ActualWidth) / 2));
            Canvas.SetTop(ActionBar, Math.Max(24, DesignCanvas.ActualHeight - ActionBar.ActualHeight - 28));
            Canvas.SetTop(FooterHint, Math.Max(24, DesignCanvas.ActualHeight - FooterHint.ActualHeight - 24));
            Canvas.SetLeft(FooterHint, 24);
        }

        private void Preview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element)
                return;

            _draggingElement = element;
            _dragOffset = e.GetPosition(element);
            element.CaptureMouse();
            e.Handled = true;
        }

        private void Preview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ReleaseDrag();
            e.Handled = true;
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ReleaseDrag();
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_draggingElement == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            var pointer = e.GetPosition(DesignCanvas);
            var maxLeft = Math.Max(0, DesignCanvas.ActualWidth - _draggingElement.ActualWidth);
            var maxTop = Math.Max(0, DesignCanvas.ActualHeight - _draggingElement.ActualHeight);
            var nextLeft = Math.Max(0, Math.Min(maxLeft, pointer.X - _dragOffset.X));
            var nextTop = Math.Max(0, Math.Min(maxTop, pointer.Y - _dragOffset.Y));

            Canvas.SetLeft(_draggingElement, nextLeft);
            Canvas.SetTop(_draggingElement, nextTop);
        }

        private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            SavePreview(NotificationPreview, OverlayIds.NotificationPopup);
            SavePreview(MutePreview, OverlayIds.MuteIndicator);
            _settingsViewModel.ApplyOverlaySettings(_workingSettings);
            DialogResult = true;
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            OverlayLayoutService.ResetPlacement(_workingSettings, OverlayIds.NotificationPopup, _screen);
            OverlayLayoutService.ResetPlacement(_workingSettings, OverlayIds.MuteIndicator, _screen);
            PositionPreview(NotificationPreview, OverlayIds.NotificationPopup);
            PositionPreview(MutePreview, OverlayIds.MuteIndicator);
        }

        private void SavePreview(FrameworkElement element, string overlayId)
        {
            var left = _screen.WorkingArea.Left + Canvas.GetLeft(element);
            var top = _screen.WorkingArea.Top + Canvas.GetTop(element);
            OverlayLayoutService.SavePlacement(
                _workingSettings,
                overlayId,
                _screen,
                left,
                top,
                element.ActualWidth > 0 ? element.ActualWidth : element.Width,
                element.ActualHeight > 0 ? element.ActualHeight : element.Height);
        }

        private void ReleaseDrag()
        {
            if (_draggingElement == null)
                return;

            _draggingElement.ReleaseMouseCapture();
            _draggingElement = null;
        }

        private static string FormatMonitorName(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
                return "Current Display";

            var normalized = deviceName.Trim().Replace(@"\\.\", string.Empty);
            if (normalized.StartsWith("DISPLAY", StringComparison.OrdinalIgnoreCase))
            {
                var suffix = normalized.Substring("DISPLAY".Length);
                if (!string.IsNullOrWhiteSpace(suffix))
                    return $"Display {suffix}";
            }

            return normalized;
        }
    }
}
