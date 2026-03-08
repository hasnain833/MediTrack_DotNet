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
        }
    }
}
