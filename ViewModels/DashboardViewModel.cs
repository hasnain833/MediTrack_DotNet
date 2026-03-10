using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using DChemist.Database;
using DChemist.Services;
using DChemist.Utils;
using Npgsql;

namespace DChemist.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private readonly AuthorizationService _auth;

        public DashboardViewModel(DatabaseService db, AuthorizationService auth)
        {
            _db = db;
            _auth = auth;
            
            Metrics = new ObservableCollection<MetricItem>
            {
                new() { Title = "Low Stock Items",  Icon = "\uE7BA", Value = "—", Trend = "Loading…",          Positive = false },
                new() { Title = "Expiring Soon",    Icon = "\uE916", Value = "—", Trend = "Within 90 days",    Positive = false },
                new() { Title = "Today's Revenue",  Icon = "\uE94C", Value = "—", Trend = "Loading…",          Positive = true  },
                new() { Title = "Sales Today",      Icon = "\uE825", Value = "—", Trend = "Loading…",          Positive = true  },
            };

            _ = LoadRealStatsAsync();
        }

        public ObservableCollection<MetricItem> Metrics { get; }
        public ObservableCollection<RecentSaleItem> RecentSales { get; } = new();
        public bool IsAdmin => _auth.IsAdmin;

        private async Task LoadRealStatsAsync()
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                // 1. Low stock items (< 10 units total)
                using (var cmd = new NpgsqlCommand(@"
                    SELECT COUNT(*) FROM (
                        SELECT medicine_id FROM inventory_batches
                        GROUP BY medicine_id
                        HAVING COALESCE(SUM(stock_qty), 0) < 10
                    ) AS low_stock", conn))
                {
                    var lowStock = Convert.ToInt64(await cmd.ExecuteScalarAsync() ?? 0);
                    Metrics[0].Value = lowStock.ToString("N0");
                    Metrics[0].Trend = lowStock == 0 ? "All items well stocked" : $"{lowStock} items need restocking";
                    Metrics[0].Positive = lowStock == 0;
                }

                // 2. Expiring soon (within 90 days)
                using (var cmd = new NpgsqlCommand(@"
                    SELECT COUNT(*) FROM inventory_batches 
                    WHERE expiry_date <= CURRENT_DATE + INTERVAL '90 days' 
                    AND stock_qty > 0", conn))
                {
                    var expiring = Convert.ToInt64(await cmd.ExecuteScalarAsync() ?? 0);
                    Metrics[1].Value = expiring.ToString("N0");
                    Metrics[1].Trend = expiring == 0 ? "No imminent expiries" : $"{expiring} batches expiring soon";
                    Metrics[1].Positive = expiring == 0;
                }

                // 3. Today's revenue
                using (var cmd = new NpgsqlCommand(
                    "SELECT COALESCE(SUM(grand_total), 0) FROM sales WHERE sale_date::date = CURRENT_DATE", conn))
                {
                    var revenue = Convert.ToDecimal(await cmd.ExecuteScalarAsync() ?? 0);
                    Metrics[2].Value = $"PKR {revenue:N0}";
                    Metrics[2].Trend = revenue > 0 ? "Sales active today" : "No sales yet today";
                    Metrics[2].Positive = revenue > 0;
                }

                // 4. Number of sales today
                using (var cmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM sales WHERE sale_date::date = CURRENT_DATE", conn))
                {
                    var salesCount = Convert.ToInt64(await cmd.ExecuteScalarAsync() ?? 0);
                    Metrics[3].Value = salesCount.ToString("N0");
                    Metrics[3].Trend = $"{salesCount} transaction(s) today";
                }

                // 5. Recent Sales Activity
                using (var cmd = new NpgsqlCommand(
                    "SELECT invoice_number, sale_date, grand_total, payment_method FROM sales ORDER BY sale_date DESC LIMIT 5", conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    App.MainRoot?.DispatcherQueue.TryEnqueue(() => RecentSales.Clear());
                    while (await reader.ReadAsync())
                    {
                        var sale = new RecentSaleItem
                        {
                            Invoice = reader["invoice_number"].ToString() ?? "N/A",
                            Date = Convert.ToDateTime(reader["sale_date"]),
                            Total = Convert.ToDecimal(reader["grand_total"]),
                            Method = reader["payment_method"].ToString() ?? "Cash"
                        };
                        App.MainRoot?.DispatcherQueue.TryEnqueue(() => RecentSales.Add(sale));
                    }
                }

                // Force UI refresh for all metrics
                foreach (var m in Metrics) m.NotifyChanged();

                AppLogger.LogInfo("DashboardViewModel stats loaded successfully.");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("DashboardViewModel.LoadRealStatsAsync failed", ex);
                foreach (var m in Metrics) { m.Trend = "Could not load data"; m.NotifyChanged(); }
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
}
