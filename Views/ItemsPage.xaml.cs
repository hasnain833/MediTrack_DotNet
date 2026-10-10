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
            // Cached: switching back is instant; data refreshes via events / OnNavigatedTo.
            NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            ViewModel = App.Current.Services.GetRequiredService<ItemsViewModel>();
            ViewModel.RequestFocus += OnViewModelRequestFocus;
            this.Loaded += (s, e) => (ViewModel.IsFormExpanded ? (Control)BarcodeBox : ListSearchBox).Focus(FocusState.Programmatic);
            this.KeyDown += OnPageKeyDown;
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

        // F2 = search · Ctrl+N = new medicine · F8 = save · Esc = close the form
        private void OnPageKeyDown(object sender, KeyRoutedEventArgs e)
        {
            bool ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                        .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            switch (e.Key)
            {
                case Windows.System.VirtualKey.F2:
                    ListSearchBox.Focus(FocusState.Programmatic);
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.N when ctrl:
                    OnNewClick(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.F8 when ViewModel.IsFormExpanded:
                    OnSaveClick(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.Escape when ViewModel.IsFormExpanded:
                    OnCloseFormClick(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
            }
        }

        // Enter in the list search opens the top match in the form, ready to edit by keyboard.
        private async void OnListSearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var top = await ViewModel.SearchNowAsync();
            if (top == null) return;
            OpenForEdit(top);
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is DChemist.Models.Medicine medicine) OpenForEdit(medicine);
        }

        private void OpenForEdit(DChemist.Models.Medicine medicine)
        {
            ViewModel.EditInFormCommand.Execute(medicine);
            ViewModel.IsFormExpanded = true;
            DispatcherQueue.TryEnqueue(() => { MedicineNameBox.Focus(FocusState.Programmatic); MedicineNameBox.SelectAll(); });
        }

        private void OnNewClick(object sender, RoutedEventArgs e)
        {
            ViewModel.StartNew();
            DispatcherQueue.TryEnqueue(() => BarcodeBox.Focus(FocusState.Programmatic));
        }

        private void OnCloseFormClick(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearEntryCommand.Execute(null);
            ViewModel.IsFormExpanded = false;
            ListSearchBox.Focus(FocusState.Programmatic);
        }

        private void OnFilterClick(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton chip) return;
            foreach (var c in new[] { FilterAll, FilterLow, FilterExp, FilterNet })
                c.IsChecked = c == chip;
            ViewModel.Filter = (string)chip.Tag;
            ExportExpiryButton.Visibility = chip == FilterExp ? Visibility.Visible : Visibility.Collapsed;
        }

        // Expiring → Export: pick one supplier (only those with expiring stock), then the print dialog (Microsoft Print to PDF for a PDF).
        private async void OnExportExpiryClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var rows = await App.Current.Services.GetRequiredService<DChemist.Repositories.BatchRepository>().GetExpiringStockAsync();
                var missingSupplier = rows.Count(r => !r.SupplierId.HasValue);
                var suppliers = rows.Where(r => r.SupplierId.HasValue).GroupBy(r => r.SupplierId).Select(g => g.ToList()).ToList();
                if (suppliers.Count == 0)
                {
                    await new ContentDialog { Title = "Nothing to export", Content = missingSupplier > 0
                        ? $"{missingSupplier} batch(es) are expired or expiring within 6 months, but have no supplier assigned. Assign a supplier to export their return list."
                        : "No remaining stock is expired or expiring within 6 months.", CloseButtonText = "OK", XamlRoot = XamlRoot }.ShowAsync();
                    return;
                }

                var picker = new ComboBox
                {
                    ItemsSource = suppliers.Select(g => $"{g[0].SupplierName}  ({g.Count} batch{(g.Count == 1 ? "" : "es")}, PKR {g.Sum(r => r.Value):N0})").ToList(),
                    SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 360
                };
                var dialog = new ContentDialog
                {
                    Title = "Expiry return list",
                    Content = new StackPanel { Spacing = 8, Children = { new TextBlock { Text = missingSupplier > 0 ? $"Supplier: {missingSupplier} batch(es) without a supplier cannot be exported." : "Supplier", TextWrapping = TextWrapping.Wrap, Opacity = 0.7 }, picker } },
                    PrimaryButtonText = "Print / Save PDF",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = XamlRoot
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

                var chosen = suppliers[picker.SelectedIndex];
                var settings = App.Current.Services.GetRequiredService<DChemist.Services.SettingsService>();
                string phone = await settings.GetPharmacyPhoneAsync();
                string info = string.Join("  ·  ", new[] { await settings.GetPharmacyAddressAsync(), string.IsNullOrWhiteSpace(phone) ? "" : $"Ph: {phone}" }.Where(s => !string.IsNullOrWhiteSpace(s)));
                var pages = ExpiryReturnSheet.BuildPages(chosen, await settings.GetPharmacyNameAsync(), info);
                await App.Current.Services.GetRequiredService<DChemist.Services.IPrintService>()
                    .PrintPagesAsync(pages, $"Expiry return - {chosen[0].SupplierName}");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Expiry export failed", ex);
                await new ContentDialog { Title = "Export failed", Content = ex.Message, CloseButtonText = "OK", XamlRoot = XamlRoot }.ShowAsync();
            }
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
                Move(current, +1);
            }
            else if (e.Key == Windows.System.VirtualKey.Down) { e.Handled = true; Move(current, +1); }
            else if (e.Key == Windows.System.VirtualKey.Up) { e.Handled = true; Move(current, -1); }
        }

        // Form order top to bottom; Box/Tablet count as one stop.
        private Control[] FieldOrder => new Control[]
        {
            BarcodeBox, MedicineNameBox, CategoryBox, NetCheckBox,
            BoxModeBtn, TabletModeBtn, PacketsPerBoxBox, UnitsPerPacketBox,
            BatchNumberBox, ExpiryDateBox,
            SellingPriceBox, PurchaseCostBox, EditStockBox
        };

        /// <summary>
        /// Focus the next/previous field that can take focus. Focus() returns false for fields inside
        /// a collapsed group (e.g. packs/box in Tablet mode), so those are skipped instead of trapping Enter.
        /// </summary>
        private void Move(Control current, int step)
        {
            var seq = FieldOrder;
            int i = Array.IndexOf(seq, current);
            if (i < 0) return;
            if (step > 0 && (current == BoxModeBtn || current == TabletModeBtn)) i = Array.IndexOf(seq, TabletModeBtn);

            for (i += step; i >= 0 && i < seq.Length; i += step)
            {
                if (seq[i].Visibility == Visibility.Visible && seq[i].Focus(FocusState.Programmatic))
                {
                    if (seq[i] is TextBox tb) tb.SelectAll();
                    return;
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
                if (sender is Control focused) Move(focused, +1);
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

        private async void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DChemist.Models.Medicine medicine)
            {
                var dialog = new ContentDialog
                {
                    Title = "Delete Medicine",
                    Content = $"Are you sure you want to permanently delete '{medicine.Name}' and ALL its stock batches?\n\nThis cannot be undone.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await (ViewModel.DeleteMedicineCommand as AsyncRelayCommand)!.ExecuteAsync(medicine);
                }
            }
        }
        private void OnPurchasePriceTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is DChemist.Models.Medicine med)
            {
                med.IsPurchasePriceVisible = !med.IsPurchasePriceVisible;
            }
        }
    }
}
