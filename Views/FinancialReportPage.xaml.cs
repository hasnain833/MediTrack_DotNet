using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using DChemist.ViewModels;

namespace DChemist.Views
{
    public sealed partial class FinancialReportPage : Page
    {
        public FinancialReportViewModel ViewModel { get; }

        public FinancialReportPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<FinancialReportViewModel>();
        }
    }
}
