using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using DChemist.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace DChemist.Views
{
    public sealed partial class StockInPage : Page
    {
        public StockInViewModel ViewModel { get; }

        public StockInPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<StockInViewModel>();
            this.DataContext = ViewModel;
            
            this.Loaded += (s, e) => MedicineSearchBox.Focus(FocusState.Programmatic);
        }

        private void OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs qualification)
        {
            if (qualification.SelectedItem is DChemist.Models.Medicine med)
            {
                ViewModel.SelectMedicine(med);
                // The item is added at index 0. We wait for it to load to focus it.
            }
        }

        private void OnMedicineSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion is DChemist.Models.Medicine med)
            {
                ViewModel.SelectMedicine(med);
            }
            else if (ViewModel.SearchSuggestions.Count > 0)
            {
                ViewModel.SelectMedicine(ViewModel.SearchSuggestions[0]);
            }
        }

        private void OnRowControlLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                // If this is the quantity box of the first item, focus it
                var item = tb.DataContext as DChemist.Models.ReceivingItem;
                if (item != null && ViewModel.ReceivingItems.Count > 0 && item == ViewModel.ReceivingItems[0])
                {
                    tb.Focus(FocusState.Programmatic);
                    tb.SelectAll();
                }
            }
        }

        private void OnRowInputKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Enter) return;

            var currentBox = sender as TextBox;
            if (currentBox == null) return;

            // Walk up to find the row Grid (the DataTemplate root)
            var rowGrid = currentBox.Parent as Grid        // direct child
                       ?? (currentBox.Parent as FrameworkElement)?.Parent as Grid; // inside StackPanel

            if (rowGrid == null) return;

            // Identify which textbox we are in by name
            if (currentBox.Name == "QtyBox")
            {
                // Move to the Total Price box (direct child of row Grid, col 2)
                var priceBox = rowGrid.Children
                    .OfType<TextBox>()
                    .FirstOrDefault(tb => tb.Name == "PriceBox");
                if (priceBox != null)
                {
                    priceBox.Focus(FocusState.Programmatic);
                    priceBox.SelectAll();
                    e.Handled = true;
                }
            }
            else if (currentBox.Name == "PriceBox")
            {
                // Done with this row — go back to medicine search
                MedicineSearchBox.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }

        private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var element = sender as FrameworkElement;
                if (element == null) return;

                switch (element.Name)
                {
                    case "SupplierComboBox":
                        InvoiceNoBox.Focus(FocusState.Programmatic);
                        break;
                    case "InvoiceNoBox":
                        InvoiceDateBox.Focus(FocusState.Programmatic);
                        break;
                    case "InvoiceDateBox":
                        MedicineSearchBox.Focus(FocusState.Programmatic);
                        break;
                }
            }
        }

        private void OnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DChemist.Models.ReceivingItem item)
            {
                ViewModel.ReceivingItems.Remove(item);
            }
        }

        private void OnSupplierSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is DChemist.Models.Supplier supplier)
            {
                ViewModel.SelectedSupplier = supplier;
                ViewModel.SessionSupplierName = supplier.Name;
            }
        }
    }
}
