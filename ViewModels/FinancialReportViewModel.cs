using System;
using System.Threading.Tasks;
using System.Windows.Input;
using DChemist.Models;
using DChemist.Repositories;
using DChemist.Services;
using DChemist.Utils;

namespace DChemist.ViewModels
{
    public class FinancialReportViewModel : ViewModelBase
    {
        private readonly SaleRepository _saleRepo;
        private readonly IReportingService _reportingService;
        private DateTimeOffset _reportDate = DateTimeOffset.Now;
        private FinancialReport? _report;
        private bool _isBusy;
        private string _statusMessage = string.Empty;

        public FinancialReportViewModel(SaleRepository saleRepo, IReportingService reportingService)
        {
            _saleRepo = saleRepo;
            _reportingService = reportingService;

            LoadReportCommand = new AsyncRelayCommand(LoadReportAsync);
            ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync, _ => Report != null);
            
            _ = LoadReportAsync();
        }

        public DateTimeOffset ReportDate
        {
            get => _reportDate;
            set { if (SetProperty(ref _reportDate, value)) _ = LoadReportAsync(); }
        }

        public FinancialReport? Report
        {
            get => _report;
            set 
            { 
                if (SetProperty(ref _report, value)) 
                {
                    ((AsyncRelayCommand)ExportCsvCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        public ICommand LoadReportCommand { get; }
        public ICommand ExportCsvCommand { get; }

        private async Task LoadReportAsync()
        {
            IsBusy = true;
            try
            {
                Report = await _saleRepo.GetFinancialReportAsync(ReportDate.DateTime);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to load financial report", ex);
                StatusMessage = "⚠ Failed to load report data.";
            }
            finally { IsBusy = false; }
        }

        private async Task ExportCsvAsync()
        {
            if (Report == null) return;
            IsBusy = true;
            try
            {
                var success = await _reportingService.ExportFinancialReportToCsvAsync(Report);
                StatusMessage = success ? "✅ Report exported successfully." : "⚠ Export cancelled or failed.";
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Export failed", ex);
                StatusMessage = "⚠ Export failed.";
            }
            finally { IsBusy = false; }
        }
    }
}
