using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using DChemist.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.System;

namespace DChemist.Views
{
    public sealed partial class FinancialPage : Page
    {
        public FinancialViewModel ViewModel { get; }

        public FinancialPage()
        {
            this.InitializeComponent();
            // Cached: switching back is instant; data refreshes in OnNavigatedTo.
            NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            ViewModel = App.Current.Services.GetRequiredService<FinancialViewModel>();
            this.DataContext = ViewModel;
            this.KeyDown += OnPageKeyDown;
        }

        protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.InitializeAsync();
            BillList.Focus(FocusState.Programmatic);
        }

        private void OnRangeClick(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton chip) return;
            foreach (var c in new[] { RangeToday, RangeYesterday, RangeWeek, RangeAll })
                c.IsChecked = c == chip;
            ViewModel.Range = (string)chip.Tag;
        }

        // F2 search · F5 reprint · R return items · Esc cancel return · (↑/↓ handled by the list)
        private void OnPageKeyDown(object sender, KeyRoutedEventArgs e)
        {
            bool typing = e.OriginalSource is TextBox;
            switch (e.Key)
            {
                case VirtualKey.F2:
                    SearchBox.Focus(FocusState.Programmatic);
                    e.Handled = true;
                    break;
                case VirtualKey.F5 when ViewModel.ReprintReceiptCommand.CanExecute(null):
                    ViewModel.ReprintReceiptCommand.Execute(null);
                    e.Handled = true;
                    break;
                case VirtualKey.R when !typing && ViewModel.IsNotReturnMode && ViewModel.StartReturnCommand.CanExecute(null):
                    ViewModel.StartReturnCommand.Execute(null);
                    DispatcherQueue.TryEnqueue(FocusFirstReturnBox);
                    e.Handled = true;
                    break;
                case VirtualKey.Escape when ViewModel.IsReturnMode:
                    ViewModel.CancelReturnCommand.Execute(null);
                    BillList.Focus(FocusState.Programmatic);
                    e.Handled = true;
                    break;
            }
        }

        // In return mode: Enter in a quantity box confirms the whole return.
        private void OnReturnQtyKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter) return;
            ViewModel.ConfirmReturnCommand.Execute(null);
            e.Handled = true;
        }

        private void FocusFirstReturnBox()
        {
            var box = FindFirst<TextBox>(this, "ReturnQtyBox");
            box?.Focus(FocusState.Programmatic);
            box?.SelectAll();
        }

        private static T? FindFirst<T>(DependencyObject root, string name) where T : Control
        {
            int n = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
                if (child is T t && t.Name == name && t.IsEnabled) return t;
                if (FindFirst<T>(child, name) is T found) return found;
            }
            return null;
        }
    }
}
