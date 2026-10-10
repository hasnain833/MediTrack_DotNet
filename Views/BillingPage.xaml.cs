using Microsoft.UI.Xaml.Controls;
using DChemist.ViewModels;
using DChemist.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace DChemist.Views
{
    public sealed partial class BillingPage : Page
    {
        public BillingViewModel ViewModel { get; }

        public BillingPage()
        {
            this.InitializeComponent();
            // Keep the cart alive when switching pages; cache dies with MainPage's frame on logout.
            NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            ViewModel = App.Current.Services.GetRequiredService<BillingViewModel>();
        }

        protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.InitializeAsync();
            // Don't force scan mode: it moves focus to an invisible box and typed names vanish.
            // The search box handles barcodes too (QuerySubmitted tries the barcode first).
            ContinuousScanToggle.Content = ViewModel.IsContinuousScanMode ? "Stop Scanning" : "Enable Continuous Scanning";
            DispatcherQueue.TryEnqueue(() => SetScannerFocus(ViewModel.IsContinuousScanMode));
        }

        private async void MedicineSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion is DChemist.Models.Medicine medicine)
            {
                await ViewModel.ExecuteAddToCartAsync(medicine);
                sender.Text = string.Empty;
                FocusTouchedItemQuantityInput();
                return;
            }

            var query = args.QueryText?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                // Enter on an empty search starts checkout at the percentage discount.
                if (ViewModel.CartItems.Count > 0) FocusBox(DiscountPercentBox);
                return;
            }

            // 1. Barcode first (scanner input) — direct DB lookup, never uses stale suggestions
            if (await ViewModel.ProcessBarcodeAsync(query, silentFail: true))
            {
                sender.Text = string.Empty;
                FocusTouchedItemQuantityInput();
                return;
            }

            // 2. Search for exactly what was typed (Enter often comes before the debounced search finishes)
            var results = await ViewModel.SearchNowAsync(query);
            var match = results.FirstOrDefault(m => m.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
                        ?? (results.Count == 1 ? results[0] : null);

            if (match != null)
            {
                await ViewModel.ExecuteAddToCartAsync(match);
                sender.Text = string.Empty;
                FocusTouchedItemQuantityInput();
                return;
            }

            ViewModel.IsStatusSuccess = false;
            ViewModel.StatusMessage = results.Count > 1
                ? "Multiple medicines found. Use ↓ to pick one from the list, then Enter."
                : $"⚠ No medicine found matching '{query}'.";
            sender.IsSuggestionListOpen = results.Count > 1;
        }

        private void ContinueusScanToggle_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ContinuousScanToggle.Content = "Stop Scanning";
            SetScannerFocus(true);
        }

        private void ContinueusScanToggle_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ContinuousScanToggle.Content = "Enable Continuous Scanning";
            SetScannerFocus(false);
        }

        private System.Text.StringBuilder _scannerBuffer = new System.Text.StringBuilder();
        private DateTime _lastScannerCharTime = DateTime.MinValue;

        private async void PageRoot_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            var now = DateTime.Now;
            var elapsed = (now - _lastScannerCharTime).TotalMilliseconds;
            _lastScannerCharTime = now;

            if (e.Key == Windows.System.VirtualKey.Enter || e.Key == Windows.System.VirtualKey.Tab)
            {
                if (_scannerBuffer.Length >= 6) 
                {
                    string barcode = _scannerBuffer.ToString();
                    _scannerBuffer.Clear();
                    MedicineSearchBox.Text = string.Empty; 
                    _ = await ViewModel.ProcessBarcodeAsync(barcode);
                    e.Handled = true;
                }
                else
                {
                    _scannerBuffer.Clear();
                }
                return; 
            }
            else if (e.Key == Windows.System.VirtualKey.F2 || e.Key == Windows.System.VirtualKey.F3 || 
                     e.Key == Windows.System.VirtualKey.F5 || e.Key == Windows.System.VirtualKey.F8 || 
                     e.Key == Windows.System.VirtualKey.Escape)
            {
            }
            else if (elapsed < 80)
            {
                if (e.Key >= Windows.System.VirtualKey.Number0 && e.Key <= Windows.System.VirtualKey.Z ||
                    e.Key >= Windows.System.VirtualKey.NumberPad0 && e.Key <= Windows.System.VirtualKey.NumberPad9)
                {
                    char c = GetCharFromKey(e.Key);
                    if (c != '\0') _scannerBuffer.Append(c);
                }
            }
            else
            {
                _scannerBuffer.Clear();
                char c = GetCharFromKey(e.Key);
                if (c != '\0') _scannerBuffer.Append(c);
            }

            if (e.Key == Windows.System.VirtualKey.F2)
            {
                MedicineSearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.F3)
            {
                if (ViewModel.AddToCartCommand.CanExecute(null))
                    _ = ViewModel.ExecuteAddToCartAsync();
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.F5)
            {
                if (ViewModel.CompleteSaleReportedCommand.CanExecute(null))
                    ViewModel.CompleteSaleReportedCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.F8)
            {
                if (ViewModel.CompleteSaleInternalCommand.CanExecute(null))
                    ViewModel.CompleteSaleInternalCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape && IsCtrlDown())
            {
                // Ctrl+Esc, not plain Esc: Esc is also used to close the suggestion list,
                // and one stray press used to wipe the whole bill.
                if (ViewModel.ClearCartCommand.CanExecute(null))
                    ViewModel.ClearCartCommand.Execute(null);
                MedicineSearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                e.Handled = true;
            }
            else
            {
                if (elapsed > 100) _scannerBuffer.Clear();
            }
        }

        private void SetScannerFocus(bool continuousMode)
        {
            if (continuousMode)
            {
                BarcodeReceiver.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            }
            else
            {
                MedicineSearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            }
        }

        private char GetCharFromKey(Windows.System.VirtualKey key)
        {
            if (key >= Windows.System.VirtualKey.Number0 && key <= Windows.System.VirtualKey.Number9)
                return (char)('0' + (key - Windows.System.VirtualKey.Number0));
            if (key >= Windows.System.VirtualKey.NumberPad0 && key <= Windows.System.VirtualKey.NumberPad9)
                return (char)('0' + (key - Windows.System.VirtualKey.NumberPad0));
            if (key >= Windows.System.VirtualKey.A && key <= Windows.System.VirtualKey.Z)
                return (char)('A' + (key - Windows.System.VirtualKey.A));
            return '\0';
        }

        private async void HiddenBarcodeReceiver_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter || e.Key == Windows.System.VirtualKey.Tab)
            {
                e.Handled = true; 
                string barcode = BarcodeReceiver.Text.Trim();
                if (!string.IsNullOrWhiteSpace(barcode))
                {
                    ViewModel.BarcodeText = barcode;
                    _ = await ViewModel.ProcessBarcodeAsync(barcode);
                    BarcodeReceiver.Text = string.Empty;
                    if (ViewModel.IsContinuousScanMode)
                    {
                        BarcodeReceiver.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                    }
                }
            }
        }

        // Enter walks the checkout inputs in order; the whole sale works with Enter only.
        private void OnEnterNext(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Enter) return;
            if (ReferenceEquals(sender, DiscountPercentBox)) FocusBox(DiscountBox);
            else if (ReferenceEquals(sender, DiscountBox)) FocusBox(CashBox);
            else if (ReferenceEquals(sender, CashBox)) { CustomerExpander.IsExpanded = true; CustomerNameBox.UpdateLayout(); FocusBox(CustomerNameBox); }
            else if (ReferenceEquals(sender, CustomerNameBox)) FocusBox(CustomerPhoneBox);
            e.Handled = true;
        }

        private static void FocusBox(TextBox box)
        {
            box.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            box.SelectAll();
        }

        private void OnSaveAcceleratorInvoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender,
            Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            _scannerBuffer.Clear();
            args.Handled = true;
            if (ViewModel.CompleteSaleInternalCommand.CanExecute(null))
                ViewModel.CompleteSaleInternalCommand.Execute(null);
        }

        private void OnPrintAcceleratorInvoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender,
            Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            _scannerBuffer.Clear();
            args.Handled = true;
            if (ViewModel.CompleteSaleReportedCommand.CanExecute(null))
                ViewModel.CompleteSaleReportedCommand.Execute(null);
        }

        // Enter in Phone (last input) prints the bill.
        private void OnPrintKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Enter) return;
            if (ViewModel.CompleteSaleReportedCommand.CanExecute(null))
                ViewModel.CompleteSaleReportedCommand.Execute(null);
            MedicineSearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            e.Handled = true;
        }

        private static bool IsCtrlDown() =>
            Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        private void OnRowInputKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // Ctrl+Del removes this cart line
            if (e.Key == Windows.System.VirtualKey.Delete && IsCtrlDown() && (sender as Microsoft.UI.Xaml.FrameworkElement)?.DataContext is SaleItemViewModel line)
            {
                ViewModel.RemoveFromCartCommand.Execute(line);
                MedicineSearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                e.Handled = true;
                return;
            }

            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var textBox = sender as TextBox;
                var container = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(textBox) as Grid;
                
                if (container != null && textBox?.PlaceholderText == "Box")
                {
                    // Move from Box to Tablet
                    var tabBox = FindVisualChild<TextBox>(container, "TabletInput");
                    tabBox?.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                    e.Handled = true;
                }
                else
                {
                    // Back to search
                    MedicineSearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Focus the Box qty of the row just added/bumped. UpdateLayout realizes the row immediately,
        /// so there's no fixed delay for fast typists to type into the wrong box.
        /// </summary>
        private void FocusTouchedItemQuantityInput()
        {
            var item = ViewModel.LastTouchedItem;
            if (item == null) return;

            CartListView.ScrollIntoView(item);
            CartListView.UpdateLayout();
            if (CartListView.ContainerFromItem(item) is ListViewItem container)
            {
                var box = FindVisualChild<TextBox>(container, "BoxInput");
                box?.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                box?.SelectAll();
            }
        }

        private T? FindVisualChild<T>(Microsoft.UI.Xaml.DependencyObject obj, string name) where T : Microsoft.UI.Xaml.DependencyObject
        {
            for (int i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(obj, i);
                if (child is T t && (string.IsNullOrEmpty(name) || (child is Microsoft.UI.Xaml.FrameworkElement fe && fe.Name == name)))
                    return t;
                var childOfChild = FindVisualChild<T>(child, name);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }
    }
}
