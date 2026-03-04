using Microsoft.UI.Xaml.Controls;
using MediTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MediTrack.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; }

        public MainPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<MainViewModel>();
            
            // Re-initialize navigation service with inner frame
            App.Current.Services.GetRequiredService<MediTrack.Services.NavigationService>().Initialize(ContentFrame);
        }
    }
}
