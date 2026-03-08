using Microsoft.UI.Xaml;

namespace DChemist
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            
            // Set window custom title bar or icon if needed in future
            this.Title = "D. Chemist - Premium Medical Management";
        }
    }
}
