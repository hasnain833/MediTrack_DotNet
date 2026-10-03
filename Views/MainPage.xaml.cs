using Microsoft.UI.Xaml.Controls;
using DChemist.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Linq;

namespace DChemist.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; }

        public MainPage()
        {
            Debug.WriteLine("[MainPage] Constructor started."); // Added log
            ViewModel = App.Current.Services.GetRequiredService<MainViewModel>();
            this.InitializeComponent();
            var navService = App.Current.Services.GetRequiredService<DChemist.Services.NavigationService>();
            navService.Initialize(ContentFrame);
            if (ViewModel.NavigationItems.Count > 0)
            {
                ViewModel.SelectedItem = ViewModel.NavigationItems[0];
            }

            // Page-switch requests (dashboard buttons, F-keys). Unsubscribe on logout: the service outlives this page.
            navService.PageRequested += GoTo;
            this.Unloaded += (_, _) => navService.PageRequested -= GoTo;
            this.KeyDown += OnGlobalKeyDown;
            Debug.WriteLine("[MainPage] Constructor finished."); // Added log
        }

        private void GoTo(string pageType)
        {
            var item = ViewModel.NavigationItems.FirstOrDefault(i => i.PageType == pageType);
            if (item != null) ViewModel.SelectedItem = item;
        }

        // App-wide keys. Pages handle their own keys first (e.g. Billing uses F3 to add to cart),
        // so these only fire when the page didn't use the key.
        private void OnGlobalKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            string? target = e.Key switch
            {
                Windows.System.VirtualKey.F1 => "DChemist.Views.DashboardPage",
                Windows.System.VirtualKey.F3 => "DChemist.Views.BillingPage",
                Windows.System.VirtualKey.F4 => "DChemist.Views.StockInPage",
                _ => null
            };
            if (target == null) return;
            GoTo(target);
            e.Handled = true;
        }

    }
}
