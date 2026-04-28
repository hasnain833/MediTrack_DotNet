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
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<BillingViewModel>();
        }
        protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.InitializeAsync();
            ViewModel.IsContinuousScanMode = true;
            ContinuousScanToggle.Content = ViewModel.IsContinuousScanMode ? "Stop Scanning" : "Enable Continuous Scanning";
            DispatcherQueue.TryEnqueue(() => SetScannerFocus(ViewModel.IsContinuousScanMode));
        }
        private async void MedicineSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion is DChemist.Models.Medicine medicine)
            {
                await ViewModel.ExecuteAddToCartAsync(medicine);
                sender.Text = string.Empty;
            }
            else if (!string.IsNullOrWhiteSpace(args.QueryText))
            {
                var query = args.QueryText.Trim();
                
                // 1. Try to find an exact match in the current search results (Name or Barcode)
                var exactMatch = ViewModel.MedicineResults.FirstOrDefault(m => 
                    m.Name.Equals(query, StringComparison.OrdinalIgnoreCase) || 
                    m.Barcode == query);

                if (exactMatch != null)
                {
                    await ViewModel.ExecuteAddToCartAsync(exactMatch);
                    sender.Text = string.Empty;
                }
                // 2. If no exact match but there's only one search result, assume that's the one
                else if (ViewModel.MedicineResults.Count == 1)
                {
                    await ViewModel.ExecuteAddToCartAsync(ViewModel.MedicineResults[0]);
                    sender.Text = string.Empty;
                }
                else
                {
                    // 3. Try as a barcode directly (silent fail if not found)
                    bool found = await ViewModel.ProcessBarcodeAsync(query, silentFail: true);
                    if (found)
                    {
                        sender.Text = string.Empty;
                    }
                    else
                    {
                        // 4. Show appropriate error message based on search results
                        ViewModel.IsStatusSuccess = false;
                        if (ViewModel.MedicineResults.Count > 1)
                        {
                            ViewModel.StatusMessage = "Multiple medicines found. Please select one from the suggestions list.";
                        }
                        else
                        {
                            ViewModel.StatusMessage = $"⚠ No medicine found matching '{query}'.";
                        }
                    }
                }
            }
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

        private void PageRoot_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
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
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                if (ViewModel.ClearCartCommand.CanExecute(null))
                    ViewModel.ClearCartCommand.Execute(null);
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

    }
}
