using System.Collections.ObjectModel;
using MediTrack.Models;
using MediTrack.Utils;

namespace MediTrack.ViewModels
{
    public class FinancialViewModel : ViewModelBase
    {
        public FinancialViewModel()
        {
            SalesHistory = new ObservableCollection<SaleSummary>
            {
                new SaleSummary { BillNo = "BILL-92834", Customer = "Walking Customer", Amount = 120.50m, Date = "27 Feb 2026", Status = "Paid" },
                new SaleSummary { BillNo = "BILL-92835", Customer = "Aslam Khan", Amount = 450.00m, Date = "27 Feb 2026", Status = "Paid" },
                new SaleSummary { BillNo = "BILL-92836", Customer = "Sarah Jane", Amount = 25.00m, Date = "27 Feb 2026", Status = "Paid" }
            };

            RevenueStats = new ObservableCollection<RevenueStat>
            {
                new RevenueStat { Label = "Daily", Value = "$595.50", Change = "+5%" },
                new RevenueStat { Label = "Weekly", Value = "$4,200.00", Change = "+12%" },
                new RevenueStat { Label = "Monthly", Value = "$18,500.00", Change = "+8%" }
            };
        }

        public ObservableCollection<SaleSummary> SalesHistory { get; }
        public ObservableCollection<RevenueStat> RevenueStats { get; }
    }

    public class SaleSummary
    {
        public string BillNo { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Date { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class RevenueStat
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Change { get; set; } = string.Empty;
    }
}
