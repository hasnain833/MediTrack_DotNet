using Microsoft.UI.Xaml.Controls;
using DChemist.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DChemist.Views
{
    public sealed partial class FinancialPage : Page
    {
        public FinancialViewModel ViewModel { get; }

        public FinancialPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<FinancialViewModel>();
            this.DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.InitializeAsync();
        }

        private void ReturnItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is InvoiceItemViewModel item)
            {
                if (ViewModel.ExecuteReturnCommand.CanExecute(item))
                {
                    ViewModel.ExecuteReturnCommand.Execute(item);
                }
            }
        }
    }
}
