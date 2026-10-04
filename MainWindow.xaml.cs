using Microsoft.UI.Xaml;

namespace DChemist
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            
            this.Title = "D. Chemist - Premium Medical Management System";
            // Title bar + taskbar icon (the .exe icon comes from ApplicationIcon in the csproj).
            AppWindow.SetIcon(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "app.ico"));
        }
    }
}
