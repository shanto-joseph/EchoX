using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using EchoX.ViewModels;

namespace EchoX
{
    public partial class AboutWindow : Window
    {
        private const string BuyMeCoffeeUrl = "https://buymeacoffee.com/shantojoseph";

        public AboutWindow(AboutViewModel aboutViewModel)
        {
            InitializeComponent();
            DataContext = aboutViewModel;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BuyMeCoffeeBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(BuyMeCoffeeUrl) { UseShellExecute = true });
            }
            catch
            {
            }
        }

        private void CreatorLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch
            {
            }
        }
    }
}
