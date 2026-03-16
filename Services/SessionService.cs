using System.Collections.ObjectModel;
using DChemist.Models;
using DChemist.ViewModels;

namespace DChemist.Services
{
    public class SessionService
    {
        // Persists the active billing cart between page navigations
        public ObservableCollection<SaleItemViewModel> ActiveCart { get; } = new();
        
        // Persists the customer info for the active sale
        public string? ActiveCustomerName { get; set; }
        public string? ActiveCustomerPhone { get; set; }

        public void ClearActiveSale()
        {
            ActiveCart.Clear();
            ActiveCustomerName = null;
            ActiveCustomerPhone = null;
        }
    }
}
