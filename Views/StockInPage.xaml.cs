using DChemist.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
                // Move to next field after barcode scan
                MedicineNameBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            }
        }

        private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var current = sender as Control;
            if (current == null) return;

            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;

                if (current == ExpiryDateBox)
                {
                    ViewModel.FormatExpiryDate();
                }

                if (current == AddToListButton)
                {
                    ViewModel.AddToListCommand.Execute(null);
                    return;
                }

                MoveToNext(current);
            }
            else if (e.Key == Windows.System.VirtualKey.Down)
            {
                e.Handled = true;
                MoveToNext(current);
            }
            else if (e.Key == Windows.System.VirtualKey.Up)
            {
                e.Handled = true;
                MoveToPrevious(current);
            }
        }

        private void MedicineNameBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                // This will be handled by OnMedicineQuerySubmitted
            }
        }

        private void OnMedicineQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            // If user choose a suggestion, it's already handled in OnSuggestionChosen.
            // But if they type name and press Enter, we handle it here.
            MoveToNext(MedicineNameBox);
        }

        private void MoveToNext(Control current)
        {
            Control[] sequence = { 
                BarcodeReceiver, MedicineNameBox, GstBox, BatchNumberBox, ExpiryDateBox, 
                BoxModeBtn, PacketModeBtn, TabletModeBtn, 
                UnitsPerPackBox, PackQuantityBox, QuantityBox,
                PurchaseTotalBox, SellingTotalBox, AddToListButton 
            };
            
            int idx = Array.IndexOf(sequence, current);
            if (idx >= 0)
            {
                // If we are on ANY of the mode buttons, jump past all of them to the actual inputs
                int searchStart = idx + 1;
                if (current == BoxModeBtn || current == PacketModeBtn || current == TabletModeBtn)
                {
                    searchStart = Array.IndexOf(sequence, UnitsPerPackBox);
                }

                // Find next visible control
                for (int i = searchStart; i < sequence.Length; i++)
                {
                    if (sequence[i].Visibility == Microsoft.UI.Xaml.Visibility.Visible)
                    {
                        sequence[i].Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                        if (sequence[i] is TextBox tb) tb.SelectAll();
                        break;
                    }
                }
            }
        }

        private void MoveToPrevious(Control current)
        {
            Control[] sequence = { 
                BarcodeReceiver, MedicineNameBox, GstBox, BatchNumberBox, ExpiryDateBox, 
                BoxModeBtn, PacketModeBtn, TabletModeBtn, 
                UnitsPerPackBox, PackQuantityBox, QuantityBox,
                PurchaseTotalBox, SellingTotalBox, AddToListButton 
            };
            int idx = Array.IndexOf(sequence, current);
            if (idx > 0)
            {
                // Find previous visible control
                for (int i = idx - 1; i >= 0; i--)
                {
                    if (sequence[i].Visibility == Microsoft.UI.Xaml.Visibility.Visible)
                    {
                        sequence[i].Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                        if (sequence[i] is TextBox tb) tb.SelectAll();
                        break;
                    }
                }
            }
        }

        private void OnModeChanged(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is ToggleButton btn)
            {
                if (btn == BoxModeBtn) ViewModel.SelectedQuantityMode = StockInViewModel.QuantityInputMode.Box;
                else if (btn == PacketModeBtn) ViewModel.SelectedQuantityMode = StockInViewModel.QuantityInputMode.Packet;
                else if (btn == TabletModeBtn) ViewModel.SelectedQuantityMode = StockInViewModel.QuantityInputMode.Tablet;
            }
        }

        private void OnModeKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                // Move to next field relative to the focused button
                if (sender is Control focused) MoveToNext(focused);
                else MoveToNext(BoxModeBtn);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Left)
            {
                if (ViewModel.SelectedQuantityMode == StockInViewModel.QuantityInputMode.Packet)
                {
                    ViewModel.SelectedQuantityMode = StockInViewModel.QuantityInputMode.Box;
                    BoxModeBtn.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                }
                else if (ViewModel.SelectedQuantityMode == StockInViewModel.QuantityInputMode.Tablet)
                {
                    ViewModel.SelectedQuantityMode = StockInViewModel.QuantityInputMode.Packet;
                    PacketModeBtn.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                }
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Right)
            {
                if (ViewModel.SelectedQuantityMode == StockInViewModel.QuantityInputMode.Box)
                {
                    ViewModel.SelectedQuantityMode = StockInViewModel.QuantityInputMode.Packet;
                    PacketModeBtn.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                }
                else if (ViewModel.SelectedQuantityMode == StockInViewModel.QuantityInputMode.Packet)
                {
                    ViewModel.SelectedQuantityMode = StockInViewModel.QuantityInputMode.Tablet;
                    TabletModeBtn.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                }
                e.Handled = true;
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
                // Move focus to next field after selecting a medicine
                GstBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                GstBox.SelectAll();
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
