using Microsoft.UI.Xaml.Controls;
using DChemist.ViewModels;
using DChemist.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DChemist.Views
{
    public sealed partial class InventoryPage : Page
    {
        public InventoryViewModel ViewModel { get; }

        public InventoryPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<InventoryViewModel>();
        }

        private void OnTogglePriceClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Medicine med)
            {
                ViewModel.TogglePurchasePriceCommand.Execute(med);
            }
        }

        private void OnEditMedicineClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Medicine med)
            {
                ViewModel.EditMedicineCommand.Execute(med);
            }
        }

        private void OnDeleteMedicineClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Medicine med)
            {
                ViewModel.DeleteMedicineCommand.Execute(med);
            }
        }
    }
}
