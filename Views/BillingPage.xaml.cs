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
    }
}
