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
    }
}
