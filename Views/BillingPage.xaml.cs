using Microsoft.UI.Xaml.Controls;
using MediTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MediTrack.Views
{
    public sealed partial class BillingPage : Page
    {
        public BillingViewModel ViewModel { get; }

        public BillingPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<BillingViewModel>();
            
            // Handle selection in search box
            MedicineSearchBox.SuggestionChosen += (s, e) => {
                ViewModel.SelectedMedicine = e.SelectedItem as MediTrack.Models.Medicine;
            };
        }
    }
}
