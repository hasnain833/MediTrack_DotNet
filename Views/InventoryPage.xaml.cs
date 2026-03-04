using Microsoft.UI.Xaml.Controls;
using MediTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MediTrack.Views
{
    public sealed partial class InventoryPage : Page
    {
        public InventoryViewModel ViewModel { get; }

        public InventoryPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<InventoryViewModel>();
        }
    }
}
