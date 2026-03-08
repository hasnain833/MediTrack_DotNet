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
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<BillingViewModel>();
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
