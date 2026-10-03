using DChemist.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace DChemist.Views
{
    public sealed partial class PurchaseHistoryPage : Page
    {
        public PurchaseHistoryViewModel ViewModel { get; }

        public PurchaseHistoryPage()
        {
            this.InitializeComponent();
            // Cached: switching back is instant; data refreshes via inventory events.
            NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            ViewModel = App.Current.Services.GetRequiredService<PurchaseHistoryViewModel>();
            this.KeyDown += OnPageKeyDown;
            this.Loaded += (_, _) => InvoiceList.Focus(FocusState.Programmatic);
        }

        private void OnRangeClick(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton chip) return;
            foreach (var c in new[] { RangeToday, RangeWeek, RangeMonth, RangeAll })
                c.IsChecked = c == chip;
            ViewModel.Range = (string)chip.Tag;
        }

        // F2 search · E edit · F8 save · Esc cancel edit (↑/↓ handled by the list)
        private void OnPageKeyDown(object sender, KeyRoutedEventArgs e)
        {
            bool typing = e.OriginalSource is TextBox;
            switch (e.Key)
            {
                case VirtualKey.F2:
                    SearchBox.Focus(FocusState.Programmatic);
                    e.Handled = true;
                    break;
                case VirtualKey.E when !typing && ViewModel.IsNotEditMode && ViewModel.HasSelectedInvoice:
                    ViewModel.EditInvoiceCommand.Execute(null);
                    e.Handled = true;
                    break;
                case VirtualKey.F8 when ViewModel.IsEditMode:
                    ViewModel.SaveEditCommand.Execute(null);
                    e.Handled = true;
                    break;
                case VirtualKey.Escape when ViewModel.IsEditMode:
                    ViewModel.CancelEditCommand.Execute(null);
                    InvoiceList.Focus(FocusState.Programmatic);
                    e.Handled = true;
                    break;
            }
        }

        // Enter moves to the next box in edit mode, like Tab; on the last row's cost it saves.
        private void OnEditRowKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter) return;
            e.Handled = true;
            if (sender is TextBox { Tag: "cost" } box && ViewModel.EditableItems.Count > 0 && box.DataContext == ViewModel.EditableItems[^1])
            {
                ViewModel.SaveEditCommand.Execute(null);
                return;
            }
            FocusManager.TryMoveFocus(FocusNavigationDirection.Next, new FindNextElementOptions { SearchRoot = XamlRoot.Content });
        }
    }
}
