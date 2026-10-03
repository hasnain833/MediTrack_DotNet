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
            // Keep the half-entered invoice alive when switching pages.
            NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            ViewModel = App.Current.Services.GetRequiredService<StockInViewModel>();
            this.DataContext = ViewModel;
            
            this.Loaded += (s, e) => MedicineSearchBox.Focus(FocusState.Programmatic);
            this.KeyDown += OnPageKeyDown;
        }

        private void OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs qualification)
        {
            if (qualification.SelectedItem is DChemist.Models.Medicine med)
            {
                ViewModel.SelectMedicine(med);
                // The item is added at index 0. We wait for it to load to focus it.
            }
        }

        private async void OnMedicineSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion is DChemist.Models.Medicine med)
            {
                ViewModel.SelectMedicine(med);
                return;
            }

            var query = args.QueryText?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                // Enter on an empty search = invoice is done: complete the purchase (save validates every row).
                if (ViewModel.SaveAllCommand.CanExecute(null)) ViewModel.SaveAllCommand.Execute(null);
                return;
            }

            // Search exactly what was typed — Enter often arrives before the debounced search,
            // and picking suggestion[0] from the previous text added the wrong medicine.
            var results = await ViewModel.SearchNowAsync(query);
            var match = results.FirstOrDefault(m => m.Name.Equals(query, StringComparison.OrdinalIgnoreCase)
                                                    || m.Barcode == query)
                        ?? (results.Count == 1 ? results[0] : null);

            if (match != null)
                ViewModel.SelectMedicine(match);
            else
            {
                ViewModel.StatusMessage = results.Count > 1
                    ? "Several medicines match — use ↓ to pick one, then Enter."
                    : $"⚠ No medicine found matching '{query}'. Add it on the Items page first.";
                sender.IsSuggestionListOpen = results.Count > 1;
            }
        }

        // F8 = save purchase (same key as Save on Billing). Works from any field.
        private void OnPageKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.F8 && ViewModel.SaveAllCommand.CanExecute(null))
            {
                ViewModel.SaveAllCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void OnRowControlLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                // When the first item's BatchBox loads, focus it so the user can type the batch number immediately
                if (tb.Name == "BatchBox")
                {
                    var item = tb.DataContext as DChemist.Models.ReceivingItem;
                    if (item != null && ViewModel.ReceivingItems.Count > 0 && item == ViewModel.ReceivingItems[0])
                    {
                        tb.Focus(FocusState.Programmatic);
                        tb.SelectAll();
                    }
                }
            }
        }

        private void OnRowInputKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (sender is not TextBox currentBox) return;

            // Ctrl+Delete removes this row and goes back to search
            bool ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                        .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            if (ctrl && e.Key == Windows.System.VirtualKey.Delete && currentBox.DataContext is DChemist.Models.ReceivingItem row)
            {
                ViewModel.ReceivingItems.Remove(row);
                MedicineSearchBox.Focus(FocusState.Programmatic);
                e.Handled = true;
                return;
            }

            if (e.Key != Windows.System.VirtualKey.Enter) return;

            // Walk up to the row root (the DataTemplate's Grid)
            DependencyObject? node = currentBox;
            while (node != null && (node as FrameworkElement)?.Name != "RowRoot")
                node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node);
            if (node == null) return;

            // Enter moves along the row; hidden boxes (Disc % on Net rows, Paid on normal rows) are skipped.
            var next = RowFieldOrder
                .SkipWhile(n => n != currentBox.Name).Skip(1)
                .Select(n => FindByName(node, n))
                .FirstOrDefault(tb => tb != null && tb.IsEnabled && tb.Visibility == Visibility.Visible);

            if (next != null)
            {
                next.Focus(FocusState.Programmatic);
                next.SelectAll();
            }
            else
            {
                // Done with this row — go back to medicine search
                MedicineSearchBox.Focus(FocusState.Programmatic);
            }
            e.Handled = true;
        }

        private static readonly string[] RowFieldOrder = { "BatchBox", "ExpiryBox", "QtyBox", "BonusBox", "PriceBox", "DiscBox", "ActualPaidBox" };

        private static TextBox? FindByName(DependencyObject root, string name)
        {
            int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
                if (child is TextBox tb && tb.Name == name) return tb;
                if (FindByName(child, name) is TextBox found) return found;
            }
            return null;
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
