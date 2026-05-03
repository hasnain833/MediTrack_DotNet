using System;
using System.Linq;
using DChemist.ViewModels;
using DChemist.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace DChemist.Views
{
    public sealed partial class ItemsPage : Page
    {
        public ItemsViewModel ViewModel { get; }

        public ItemsPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<ItemsViewModel>();
            ViewModel.RequestFocus += OnViewModelRequestFocus;
            this.Loaded += (s, e) => BarcodeBox.Focus(FocusState.Programmatic);
        }

        private void OnViewModelRequestFocus(object? sender, string target)
        {
            Control? control = target switch
            {
                "Barcode" => BarcodeBox,
                "MedicineName" => MedicineNameBox,
                "BatchNumber" => BatchNumberBox,
                "ExpiryDate" => ExpiryDateBox,
                "SellingPrice" => SellingPriceBox,
                _ => null
            };
            control?.Focus(FocusState.Programmatic);
        }

        private void ToggleForm_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.IsFormExpanded = !ViewModel.IsFormExpanded;
        }

        private void OnBarcodeKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                ViewModel.LookupBarcodeCommand.Execute(null);
                MedicineNameBox.Focus(FocusState.Programmatic);
            }
        }

        private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var current = sender as Control;
            if (current == null) return;

            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                if (current == ExpiryDateBox) ViewModel.FormatExpiryDate();
                if (current == SellingPriceBox)
                {
                    OnSaveClick(this, new RoutedEventArgs());
                    return;
                }
                MoveToNext(current);
            }
            else if (e.Key == Windows.System.VirtualKey.Down) { e.Handled = true; MoveToNext(current); }
            else if (e.Key == Windows.System.VirtualKey.Up) { e.Handled = true; MoveToPrevious(current); }
        }

        private void MoveToNext(Control current)
        {
            Control[] sequence = { 
                BarcodeBox, MedicineNameBox, BatchNumberBox, ExpiryDateBox, 
                BoxModeBtn, TabletModeBtn, 
                PackQuantityBox, PacketsPerBoxBox, UnitsPerPacketBox, QuantityBox,
                SellingPriceBox 
            };
            int idx = Array.IndexOf(sequence, current);
            if (idx >= 0)
            {
                int searchStart = idx + 1;
                if (current == BoxModeBtn || current == TabletModeBtn)
                {
                    searchStart = Array.IndexOf(sequence, PackQuantityBox);
                }

                for (int i = searchStart; i < sequence.Length; i++)
                {
                    if (sequence[i].Visibility == Visibility.Visible)
                    {
                        sequence[i].Focus(FocusState.Programmatic);
                        if (sequence[i] is TextBox tb) tb.SelectAll();
                        break;
                    }
                }
            }
        }

        private void MoveToPrevious(Control current)
        {
            Control[] sequence = { 
                BarcodeBox, MedicineNameBox, BatchNumberBox, ExpiryDateBox, 
                BoxModeBtn, TabletModeBtn, 
                PackQuantityBox, PacketsPerBoxBox, UnitsPerPacketBox, QuantityBox,
                SellingPriceBox 
            };
            int idx = Array.IndexOf(sequence, current);
            if (idx > 0)
            {
                for (int i = idx - 1; i >= 0; i--)
                {
                    if (sequence[i].Visibility == Visibility.Visible)
                    {
                        sequence[i].Focus(FocusState.Programmatic);
                        if (sequence[i] is TextBox tb) tb.SelectAll();
                        break;
                    }
                }
            }
        }

        private void OnModeChanged(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn)
            {
                if (btn == BoxModeBtn) ViewModel.SelectedQuantityMode = ItemsViewModel.QuantityInputMode.Box;
                else if (btn == TabletModeBtn) ViewModel.SelectedQuantityMode = ItemsViewModel.QuantityInputMode.Tablet;
            }
        }

        private void OnModeKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (sender is Control focused) MoveToNext(focused);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Left)
            {
                if (ViewModel.IsTabletMode) { ViewModel.SelectedQuantityMode = ItemsViewModel.QuantityInputMode.Box; BoxModeBtn.Focus(FocusState.Programmatic); }
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Right)
            {
                if (ViewModel.IsBoxMode) { ViewModel.SelectedQuantityMode = ItemsViewModel.QuantityInputMode.Tablet; TabletModeBtn.Focus(FocusState.Programmatic); }
                e.Handled = true;
            }
        }

        private void ExpiryDateBox_LostFocus(object sender, RoutedEventArgs e) => ViewModel.FormatExpiryDate();

        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            await (ViewModel.SaveCommand as AsyncRelayCommand)!.ExecuteAsync(null);
        }
    }
}
