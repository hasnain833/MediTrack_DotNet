using System.Collections.ObjectModel;
using MediTrack.Models;
using MediTrack.Services;
using MediTrack.Utils;

namespace MediTrack.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        public DashboardViewModel()
        {
            Metrics = new ObservableCollection<MetricItem>
            {
                new MetricItem { Title = "Total Sales", Value = "$12,450", Icon = "\uE825", Trend = "+12% vs last month", Positive = true },
                new MetricItem { Title = "Medicines in Stock", Value = "1,240", Icon = "\uE811", Trend = "5 items low on stock", Positive = false },
                new MetricItem { Title = "Active Users", Value = "8", Icon = "\uE716", Trend = "All systems operational", Positive = true },
                new MetricItem { Title = "Today's Revenue", Value = "$450", Icon = "\uE94C", Trend = "+5% from yesterday", Positive = true }
            };

            RecentActivities = new ObservableCollection<string>
            {
                "Sale #1245 completed by admin",
                "Panadol stock updated (+500)",
                "New user 'Naveed' added to system",
                "Weekly financial report generated"
            };
        }

        public ObservableCollection<MetricItem> Metrics { get; }
        public ObservableCollection<string> RecentActivities { get; }
    }

    public class MetricItem
    {
        public string Title { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Trend { get; set; } = string.Empty;
        public bool Positive { get; set; }
    }
}
