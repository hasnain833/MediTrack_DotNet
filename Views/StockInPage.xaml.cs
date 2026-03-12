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

            // Auto-focus barcode input after page loads
            this.Loaded += (_, _) => BarcodeInput.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }

        /// <summary>
        /// Barcode scanners send Enter after the scan — treat that as Lookup.
        /// </summary>
        private void OnBarcodeKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                ViewModel.LookupBarcodeCommand.Execute(null);
            }
        }
    }
}
