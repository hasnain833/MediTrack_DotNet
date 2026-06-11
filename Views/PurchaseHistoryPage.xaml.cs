using DChemist.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
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
            ViewModel = App.Current.Services.GetRequiredService<PurchaseHistoryViewModel>();
        }

        private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                ViewModel.SearchCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
