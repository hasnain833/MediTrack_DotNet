using Microsoft.UI.Xaml.Controls;
using DChemist.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DChemist.Views
{
    public sealed partial class DashboardPage : Page
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPage()
        {
            System.Diagnostics.Debug.WriteLine("[DashboardPage] Constructor: Resolving ViewModel...");
            ViewModel = App.Current.Services.GetRequiredService<DashboardViewModel>();

            System.Diagnostics.Debug.WriteLine("[DashboardPage] Constructor: Initializing XAML Components...");
            try 
            {
                this.InitializeComponent();
                // Cached: switching back is instant; stats refresh in OnNavigatedTo.
                NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
                System.Diagnostics.Debug.WriteLine("[DashboardPage] Constructor: XAML Initialized.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardPage] Constructor: FATAL XAML ERROR: {ex}");
                throw;
            }
        }

        protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.LoadRealStatsAsync();
        }

        private static void Go(string page) =>
            App.Current.Services.GetRequiredService<DChemist.Services.NavigationService>().RequestPage(page);

        private void OnNewSaleClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => Go("DChemist.Views.BillingPage");
        private void OnNewPurchaseClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => Go("DChemist.Views.StockInPage");
        private void OnAllBillsClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => Go("DChemist.Views.FinancialPage");

        private void OnAttentionFilterClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is not Microsoft.UI.Xaml.Controls.Primitives.ToggleButton chip) return;
            foreach (var c in new[] { AttnAll, AttnExp, AttnLow })
                c.IsChecked = c == chip;
            ViewModel.AttentionFilter = (string)chip.Tag;
        }
    }
}
