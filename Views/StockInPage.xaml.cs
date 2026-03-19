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
                // Force binding update if needed, but PropertyChanged trigger should handle it
                ViewModel.LookupBarcodeCommand.Execute(null);
            }
        }

        private System.Text.StringBuilder _scannerBuffer = new System.Text.StringBuilder();
        private DateTime _lastScannerCharTime = DateTime.MinValue;

        private void PageRoot_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            var now = DateTime.Now;
            var elapsed = (now - _lastScannerCharTime).TotalMilliseconds;
            _lastScannerCharTime = now;

            if ((e.Key >= Windows.System.VirtualKey.Number0 && e.Key <= Windows.System.VirtualKey.Number9) ||
                (e.Key >= Windows.System.VirtualKey.NumberPad0 && e.Key <= Windows.System.VirtualKey.NumberPad9))
            {
                char c = GetCharFromKey(e.Key);
                _scannerBuffer.Append(c);
            }
            else if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (_scannerBuffer.Length >= 3)
                {
                    string barcode = _scannerBuffer.ToString();
                    _scannerBuffer.Clear();
                    
                    // Don't duplicate if focus was already inside BarcodeReceiver and handled by OnBarcodeKeyDown
                    if (!ReferenceEquals(FocusManager.GetFocusedElement(this.XamlRoot), BarcodeReceiver))
                    {
                        ViewModel.BarcodeText = barcode;
                        ViewModel.LookupBarcodeCommand.Execute(null);
                        e.Handled = true;
                    }
                }
                else
                {
                    _scannerBuffer.Clear();
                }
            }
            else
            {
                if (elapsed > 100) _scannerBuffer.Clear();
            }
        }

        private char GetCharFromKey(Windows.System.VirtualKey key)
        {
            if (key >= Windows.System.VirtualKey.Number0 && key <= Windows.System.VirtualKey.Number9)
                return (char)('0' + (key - Windows.System.VirtualKey.Number0));
            if (key >= Windows.System.VirtualKey.NumberPad0 && key <= Windows.System.VirtualKey.NumberPad9)
                return (char)('0' + (key - Windows.System.VirtualKey.NumberPad0));
            return '\0';
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
