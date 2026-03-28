using System.Windows;

namespace EchoX
{
    public partial class ConfirmDeleteDialog : Window
    {
        public ConfirmDeleteDialog(string profileName)
        {
            InitializeComponent();
            MessageText.Text = $"Are you sure you want to delete \"{profileName}\"? This action cannot be undone.";

            // Allow dragging the dialog
            MouseLeftButtonDown += (s, e) => DragMove();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
