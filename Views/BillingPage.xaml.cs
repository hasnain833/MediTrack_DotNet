using Microsoft.UI.Xaml.Controls;
using DChemist.ViewModels;
using DChemist.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DChemist.Views
{
    public sealed partial class BillingPage : Page
    {
        public BillingViewModel ViewModel { get; }

        public BillingPage()
        {
            System.Diagnostics.Debug.WriteLine("[BillingPage] Constructor: Initializing XAML Components...");
            this.InitializeComponent();
            System.Diagnostics.Debug.WriteLine("[BillingPage] Constructor: Resolving ViewModel...");
            ViewModel = App.Current.Services.GetRequiredService<BillingViewModel>();
            System.Diagnostics.Debug.WriteLine("[BillingPage] Constructor: Ready.");
        }
        protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[BillingPage] OnNavigatedTo: Start.");
            base.OnNavigatedTo(e);
            await ViewModel.InitializeAsync();
            System.Diagnostics.Debug.WriteLine("[BillingPage] OnNavigatedTo: ViewModel Initialized.");
        }
        private void MedicineSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion is DChemist.Models.Medicine medicine)
            {
                _ = ViewModel.ExecuteAddToCartAsync(medicine);
                sender.Text = string.Empty;
            }
        }

        private void ContinueusScanToggle_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ContinuousScanToggle.Content = "Stop Scanning";
        }

        private void ContinueusScanToggle_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ContinuousScanToggle.Content = "Enable Continuous Scanning";
        }

        private void PageRoot_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Focus locking no longer strictly required with global key listener
        }

        private System.Text.StringBuilder _scannerBuffer = new System.Text.StringBuilder();
        private DateTime _lastScannerCharTime = DateTime.MinValue;

        private void PageRoot_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // Ignore if we are in a text box that might need the Enter key (though mostly they don't in this UI)
            // But specifically, if focus is in a quantity field or search box, we might want to be careful.
            // However, most scanners are VERY fast.
            
            var now = DateTime.Now;
            var elapsed = (now - _lastScannerCharTime).TotalMilliseconds;
            _lastScannerCharTime = now;

            // If it's a numeric key or a standard barcode character
            if ((e.Key >= Windows.System.VirtualKey.Number0 && e.Key <= Windows.System.VirtualKey.Number9) ||
                (e.Key >= Windows.System.VirtualKey.NumberPad0 && e.Key <= Windows.System.VirtualKey.NumberPad9))
            {
                // If it's fast (< 100ms) or we are already accumulating, it's likely a scanner
                char c = GetCharFromKey(e.Key);
                _scannerBuffer.Append(c);
            }
            else if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (_scannerBuffer.Length >= 3) // Minimum barcode length
                {
                    string barcode = _scannerBuffer.ToString();
                    _scannerBuffer.Clear();
                    ViewModel.BarcodeText = barcode;
                    _ = ViewModel.ExecuteAddToCartAsync();
                    e.Handled = true;
                }
                else
                {
                    _scannerBuffer.Clear();
                }
            }
            else if (e.Key == Windows.System.VirtualKey.F2)
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
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                if (ViewModel.ClearCartCommand.CanExecute(null))
                    ViewModel.ClearCartCommand.Execute(null);
                e.Handled = true;
            }
            else
            {
                // Reset on non-numeric (except Enter/Fx) if it's slow
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

        private void HiddenBarcodeReceiver_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // Legacy support for the hidden textbox if focus lands there
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true; 
                if (!string.IsNullOrWhiteSpace(ViewModel.BarcodeText))
                {
                    _ = ViewModel.ExecuteAddToCartAsync();
                    ViewModel.BarcodeText = string.Empty;
                }
            }
        }
    }
}
