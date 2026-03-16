using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using DChemist.Database;
using DChemist.Services;
using DChemist.Repositories;
using DChemist.Utils;
using Npgsql;

namespace DChemist.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly IDashboardRepository _dashboardRepo;
        private readonly AuthorizationService _auth;
        private bool _isBusy;

        public DashboardViewModel(IDashboardRepository dashboardRepo, AuthorizationService auth)
        {
            _dashboardRepo = dashboardRepo;
            _auth = auth;
            
            Metrics = new ObservableCollection<MetricItem>
            {
                new() { Title = "Low Stock Items",  Icon = "\uE7BA", Value = "—", Trend = "Loading…",          Positive = false },
                new() { Title = "Expiring Soon",    Icon = "\uE916", Value = "—", Trend = "Within 90 days",    Positive = false },
                new() { Title = "Today's Revenue",  Icon = "\uE94C", Value = "—", Trend = "Loading…",          Positive = true  },
            };

            _ = LoadRealStatsAsync();
        }

        public ObservableCollection<MetricItem> Metrics { get; }
        public ObservableCollection<RecentSaleItem> RecentSales { get; } = new();
        public ObservableCollection<CriticalAlertViewModel> CriticalAlerts { get; } = new();
        public bool IsAdmin => _auth.IsAdmin;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        private async Task LoadRealStatsAsync()
        {
            IsBusy = true;
            try
            {
                // 1. Low stock items
                var lowStock = await _dashboardRepo.GetLowStockCountAsync();
                Metrics[0].Value = lowStock.ToString("N0");
                Metrics[0].Trend = lowStock == 0 ? "All items well stocked" : $"{lowStock} items need restocking";
                Metrics[0].Positive = lowStock == 0;

                // 2. Expiring soon
                var expiring = await _dashboardRepo.GetExpiringSoonCountAsync();
                Metrics[1].Value = expiring.ToString("N0");
                Metrics[1].Trend = expiring == 0 ? "No imminent expiries" : $"{expiring} batches expiring soon";
                Metrics[1].Positive = expiring == 0;

                // 3. Today's revenue
                var revenue = await _dashboardRepo.GetTodaysRevenueAsync();
                Metrics[2].Value = $"PKR {revenue:N0}";
                Metrics[2].Trend = revenue > 0 ? "Sales active today" : "No sales yet today";
                Metrics[2].Positive = revenue > 0;

                // 4. Recent Sales Activity
                var recentSales = await _dashboardRepo.GetRecentSalesAsync();
                
                // 5. Critical Alerts
                var criticalAlertsData = await _dashboardRepo.GetCriticalAlertsAsync();

                App.MainRoot?.DispatcherQueue.TryEnqueue(() => 
                {
                    RecentSales.Clear();
                    foreach (var sale in recentSales)
                    {
                        RecentSales.Add(new RecentSaleItem
                        {
                            Invoice = sale.Invoice,
                            Date = sale.Date,
                            Total = sale.Total,
                            Method = sale.Method
                        });
                    }

                    CriticalAlerts.Clear();
                    foreach (var alert in criticalAlertsData)
                    {
                        CriticalAlerts.Add(new CriticalAlertViewModel
                        {
                            Message = alert.Message,
                            Type = alert.Type
                        });
                    }
                });

                // Force UI refresh for all metrics
                foreach (var m in Metrics) m.NotifyChanged();

                AppLogger.LogInfo("DashboardViewModel stats loaded successfully.");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("DashboardViewModel.LoadRealStatsAsync failed", ex);
                foreach (var m in Metrics) { m.Trend = "Could not load data"; m.NotifyChanged(); }
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    public class MetricItem : ViewModelBase
    {
        private string _title = string.Empty;
        private string _value = string.Empty;
        private string _icon = string.Empty;
        private string _trend = string.Empty;
        private bool _positive;

        public string Title   { get => _title;    set => SetProperty(ref _title, value); }
        public string Value   { get => _value;    set => SetProperty(ref _value, value); }
        public string Icon    { get => _icon;     set => SetProperty(ref _icon, value); }
        public string Trend   { get => _trend;    set => SetProperty(ref _trend, value); }
        public bool   Positive{ get => _positive; set => SetProperty(ref _positive, value); }

        /// <summary>Force the UI to re-read all properties of this item.</summary>
        public void NotifyChanged() => OnPropertyChanged(string.Empty);
    }

    public class RecentSaleItem
    {
        public string Invoice { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal Total { get; set; }
        public string Method { get; set; } = string.Empty;
    }

    public class CriticalAlertViewModel
    {
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Color => Type == "Low Stock" ? "#D93025" : "#F9AB00"; // Red for stock, Amber for expiry
    }
}
