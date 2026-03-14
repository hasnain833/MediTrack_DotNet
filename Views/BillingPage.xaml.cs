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
            LockFocus();
        }

        private void ContinueusScanToggle_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ContinuousScanToggle.Content = "Enable Continuous Scanning";
        }

        private void PageRoot_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // If user clicks anywhere while mode is active, reclaim focus
            if (ContinuousScanToggle.IsChecked == true)
            {
                LockFocus();
            }
        }

        private void LockFocus()
        {
            BarcodeReceiver.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }

        private void HiddenBarcodeReceiver_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                // Since HandleBarcodeScanAsync triggers on PropertyChanged, setting the text handles it. 
                // But just in case, we can manually trigger by clearing and resetting or simply checking the buffer.
                if (!string.IsNullOrWhiteSpace(ViewModel.BarcodeText))
                {
                    // Trigger add to cart if medicine is set
                    _ = ViewModel.ExecuteAddToCartAsync(ViewModel.SelectedMedicine);
                    ViewModel.BarcodeText = string.Empty;
                }
            }
        }

        }
    }
}
