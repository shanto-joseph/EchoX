using EchoX.ViewModels;

namespace EchoX.ViewModels
{
    public class AboutViewModel : ViewModelBase
    {
        public string AppVersion => "1.0.0";
        public string Description => "EchoX is a modern audio device manager for Windows.";
    }
}