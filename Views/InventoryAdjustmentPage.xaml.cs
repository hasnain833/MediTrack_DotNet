using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using DChemist.ViewModels;

namespace DChemist.Views
{
    public sealed partial class InventoryAdjustmentPage : Page
    {
        public InventoryAdjustmentViewModel ViewModel { get; }

        public InventoryAdjustmentPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<InventoryAdjustmentViewModel>();
        }
    }
}
