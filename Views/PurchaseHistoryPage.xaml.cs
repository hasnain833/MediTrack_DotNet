using DChemist.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

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
    }
}
