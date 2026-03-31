using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace EchoX;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\EchoX.SingleInstance";
    private static readonly uint ActivateExistingInstanceMessage = RegisterWindowMessage("EchoX.ActivateExistingInstance");
    private static readonly IntPtr HwndBroadcast = new IntPtr(0xffff);
    private Mutex? _singleInstanceMutex;

    internal static uint ActivateMessageId => ActivateExistingInstanceMessage;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            PostMessage(HwndBroadcast, ActivateExistingInstanceMessage, IntPtr.Zero, IntPtr.Zero);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += (s, ex) =>
        {
            System.IO.File.WriteAllText("crash.log", ex.Exception.ToString());
            System.Windows.MessageBox.Show(ex.Exception.Message, "EchoX Error");
            ex.Handled = true;
        };

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
        base.OnExit(e);
    }
}
