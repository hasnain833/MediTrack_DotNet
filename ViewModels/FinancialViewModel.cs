using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using DChemist.Models;
using DChemist.Repositories;
using DChemist.Services;
using DChemist.Utils;

namespace DChemist.ViewModels
{
    public class FinancialViewModel : ViewModelBase
    {
        private readonly SaleRepository _saleRepo;
        private readonly IReportingService _reportingService;

        public FinancialViewModel(SaleRepository saleRepo, IReportingService reportingService)
        {
            _saleRepo = saleRepo;
            _reportingService = reportingService;
            
            SalesHistory = new ObservableCollection<SaleSummary>();
            RevenueStats = new ObservableCollection<RevenueStat>();
            
            ExportCommand = new RelayCommand(async _ => await _reportingService.ExportSalesToCsvAsync(SalesHistory));
            
            _ = LoadDataAsync();
        }

        public ObservableCollection<SaleSummary> SalesHistory { get; }
        public ObservableCollection<RevenueStat> RevenueStats { get; }
        public System.Windows.Input.ICommand ExportCommand { get; }

        private async Task LoadDataAsync()
        {
            try
            {
                // 1. Load Sales History
                var history = await _saleRepo.GetAllSummariesAsync(50);
                SalesHistory.Clear();
                foreach (var item in history) SalesHistory.Add(item);

                // 2. Load Revenue Stats
                RevenueStats.Clear();
                
                var todayStart = DateTime.Today;
                var todayEnd = DateTime.Today.AddDays(1).AddSeconds(-1);
                var dailyRev = await _saleRepo.GetRevenueTotalAsync(todayStart, todayEnd);
                RevenueStats.Add(new RevenueStat { Label = "Daily", Value = $"PKR {dailyRev:N2}", Change = "Real-time" });

                var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                var weeklyRev = await _saleRepo.GetRevenueTotalAsync(weekStart, todayEnd);
                RevenueStats.Add(new RevenueStat { Label = "Weekly", Value = $"PKR {weeklyRev:N2}", Change = "This Week" });

                var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var monthlyRev = await _saleRepo.GetRevenueTotalAsync(monthStart, todayEnd);
                RevenueStats.Add(new RevenueStat { Label = "Monthly", Value = $"PKR {monthlyRev:N2}", Change = "This Month" });
            }
            catch (Exception ex)
            {
                AppLogger.LogError("FinancialViewModel.LoadDataAsync failed", ex);
            }
        }
    }

    public class RevenueStat
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Change { get; set; } = string.Empty;
    }
}
