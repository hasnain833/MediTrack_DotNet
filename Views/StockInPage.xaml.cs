using DChemist.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DChemist.Views
{
    public sealed partial class StockInPage : Page
    {
        public StockInViewModel ViewModel { get; }

        public StockInPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<StockInViewModel>();
            ViewModel.RequestFocus += OnViewModelRequestFocus;
            this.Loaded += (s, e) => LockFocus();
        }

        private void OnViewModelRequestFocus(object? sender, string target)
        {
            if (target == "MedicineName")
            {
                MedicineNameBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            }
        }

        private void PageRoot_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (ViewModel.IsAutoAddEnabled)
            {
                LockFocus();
            }
        }

        private void LockFocus()
        {
            BarcodeReceiver.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }

        private void OnBarcodeKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                ViewModel.LookupBarcodeCommand.Execute(null);
            }
        }

        private void OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is DChemist.Models.Medicine med)
            {
                ViewModel.SelectMedicine(med);
            }
        }

        private void OnSupplierSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is DChemist.Models.Supplier supplier)
            {
                ViewModel.SelectedSupplier = supplier;
            }
        }
    }
}
