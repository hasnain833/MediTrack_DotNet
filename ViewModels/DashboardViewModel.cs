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
            
            _ = LoadRealStatsAsync();
        }

        private readonly BulkObservableCollection<DashboardMedicineAlert> _lowStockList = new();
        private readonly BulkObservableCollection<DashboardMedicineAlert> _expiringList = new();
        private readonly BulkObservableCollection<RecentSaleItem> _recentSales = new();

        public ObservableCollection<DashboardMedicineAlert> LowStockList => _lowStockList;
        public ObservableCollection<DashboardMedicineAlert> ExpiringList => _expiringList;
        public ObservableCollection<RecentSaleItem> RecentSales => _recentSales;
        public bool IsAdmin => _auth.IsAdmin;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        private async Task LoadRealStatsAsync()
        {
            IsBusy = true;
            try
            {
                var lowStock = await _dashboardRepo.GetLowStockItemsAsync();
                var expiring = await _dashboardRepo.GetExpiringItemsAsync();
                var recentSales = await _dashboardRepo.GetRecentSalesAsync();

                var recentSaleItems = recentSales.Select(sale => new RecentSaleItem
                {
                    Invoice = sale.Invoice,
                    Date = sale.Date,
                    Total = sale.Total,
                    Method = sale.Method
                }).ToList();

                App.MainRoot?.DispatcherQueue.TryEnqueue(() => 
                {
                    _lowStockList.ReplaceAll(lowStock);
                    _expiringList.ReplaceAll(expiring);
                    _recentSales.ReplaceAll(recentSaleItems);
                });

                AppLogger.LogInfo("DashboardViewModel stats loaded successfully.");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("DashboardViewModel.LoadRealStatsAsync failed", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    public class RecentSaleItem
    {
        public string Invoice { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal Total { get; set; }
        public string Method { get; set; } = string.Empty;
    }
}
