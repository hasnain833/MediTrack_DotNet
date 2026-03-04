using Microsoft.UI.Xaml.Controls;
using MediTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MediTrack.Views
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
