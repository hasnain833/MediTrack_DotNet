using Microsoft.UI.Xaml;

namespace MediTrack
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            
            // Set window custom title bar or icon if needed in future
            this.Title = "MediTrack - Premium Medical Management";
        }
    }
}
