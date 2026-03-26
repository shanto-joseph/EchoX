using System.Configuration;
using System.Data;
using System.Windows;
namespace EchoX;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (s, ex) =>
        {
            System.IO.File.WriteAllText("crash.log", ex.Exception.ToString());
            System.Windows.MessageBox.Show(ex.Exception.Message, "EchoX Error");
            ex.Handled = true;
        };
    }
}

