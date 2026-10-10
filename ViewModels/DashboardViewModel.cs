using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DChemist.Repositories;
using DChemist.Services;
using DChemist.Utils;

namespace DChemist.ViewModels
{
    /// <summary>Home screen: today's numbers, what needs attention, this week's sales, recent bills.</summary>
    public class DashboardViewModel : ViewModelBase
    {
        private readonly IDashboardRepository _dashboardRepo;
        private readonly DashboardStatsRepository _statsRepo;
        private readonly AuthorizationService _auth;
        private readonly AuthService _authService;
        private List<AttentionItem> _allAttention = new();

        public DashboardViewModel(IDashboardRepository dashboardRepo, DashboardStatsRepository statsRepo,
                                  AuthorizationService auth, AuthService authService)
        {
            _dashboardRepo = dashboardRepo;
            _statsRepo = statsRepo;
            _auth = auth;
            _authService = authService;
            // Loaded by DashboardPage.OnNavigatedTo (page is cached, so it refreshes on each visit).
        }

        public bool IsAdmin => _auth.IsAdmin;

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        public string Greeting
        {
            get
            {
                int h = DateTime.Now.Hour;
                string part = h < 12 ? "Good morning" : h < 17 ? "Good afternoon" : "Good evening";
                var u = _authService.CurrentUser;
                string? name = string.IsNullOrWhiteSpace(u?.FullName) ? u?.Username : u.FullName;
                return string.IsNullOrWhiteSpace(name) ? part : $"{part}, {name}";
            }
        }
        private string _profitLabel = "PROFIT TODAY";
        public string ProfitLabel { get => _profitLabel; private set => SetProperty(ref _profitLabel, value); }
        public string TodayText => DateTime.Now.ToString("dddd, d MMMM");

        // ---- KPI row ----
        private decimal _salesToday;
        public decimal SalesToday { get => _salesToday; private set => SetProperty(ref _salesToday, value); }
        private int _billsToday;
        public int BillsToday { get => _billsToday; private set => SetProperty(ref _billsToday, value); }
        private decimal _profitToday;
        public decimal ProfitToday { get => _profitToday; private set => SetProperty(ref _profitToday, value); }
        private string _vsLastWeekText = string.Empty;
        public string VsLastWeekText { get => _vsLastWeekText; private set => SetProperty(ref _vsLastWeekText, value); }
        private bool _isUpVsLastWeek = true;
        public bool IsUpVsLastWeek { get => _isUpVsLastWeek; private set => SetProperty(ref _isUpVsLastWeek, value); }
        private string _lastBillText = string.Empty;
        public string LastBillText { get => _lastBillText; private set => SetProperty(ref _lastBillText, value); }
        public string MarginText => SalesToday > 0 ? $"{ProfitToday / SalesToday * 100:0}% margin" : "—";

        // ---- Needs attention ----
        public ObservableCollection<AttentionItem> Attention { get; } = new();
        private int _attentionCount;
        public int AttentionCount { get => _attentionCount; private set => SetProperty(ref _attentionCount, value); }
        private string _attentionSummary = string.Empty;
        public string AttentionSummary { get => _attentionSummary; private set => SetProperty(ref _attentionSummary, value); }
        public string ExpiryChipText => $"Expiry {_allAttention.Count(a => a.IsExpiry)}";
        public string LowChipText => $"Low stock {_allAttention.Count(a => !a.IsExpiry)}";
        public string AllChipText => $"All {_allAttention.Count}";

        private string _attentionFilter = "all";
        public string AttentionFilter
        {
            get => _attentionFilter;
            set { if (SetProperty(ref _attentionFilter, value)) ApplyAttentionFilter(); }
        }

        private void ApplyAttentionFilter()
        {
            Attention.Clear();
            foreach (var a in _allAttention.Where(a => AttentionFilter == "all" || (AttentionFilter == "exp") == a.IsExpiry))
                Attention.Add(a);
        }

        // ---- Week chart + recent bills ----
        public ObservableCollection<DayBar> WeekBars { get; } = new();
        private decimal _weekTotal;
        public decimal WeekTotal { get => _weekTotal; private set => SetProperty(ref _weekTotal, value); }
        public ObservableCollection<RecentSaleItem> RecentSales { get; } = new();

        public async Task LoadRealStatsAsync()
        {
            IsBusy = true;
            try
            {
                var today = await _statsRepo.GetTodayAsync();
                var days = await _statsRepo.GetDailySalesAsync();
                _allAttention = await _statsRepo.GetAttentionItemsAsync();
                var recent = await _dashboardRepo.GetRecentSalesAsync(6);

                SalesToday = today.Sales;
                BillsToday = today.Bills;
                ProfitToday = today.Profit;
                ProfitLabel = today.EstimatedCostItems > 0 || today.MissingCostItems > 0 ? "PROFIT TODAY (ESTIMATE)" : "PROFIT TODAY";
                LastBillText = today.LastBill is DateTime t ? $"last at {t.ToLocalTime():HH:mm}" : "no bills yet";
                OnPropertyChanged(nameof(MarginText));
                OnPropertyChanged(nameof(Greeting));
                OnPropertyChanged(nameof(TodayText));

                // Same weekday last week (days[13] = today, days[6] = 7 days ago)
                decimal lastWeek = days.Count == 14 ? days[6].Total : 0;
                IsUpVsLastWeek = SalesToday >= lastWeek;
                VsLastWeekText = lastWeek > 0
                    ? $"{(IsUpVsLastWeek ? "▲" : "▼")} {Math.Abs((SalesToday - lastWeek) / lastWeek * 100):0}% vs last {DateTime.Today:dddd}"
                    : $"no sales last {DateTime.Today:dddd}";

                var week = days.Skip(Math.Max(0, days.Count - 7)).ToList();
                decimal max = Math.Max(1, week.Max(d => d.Total));
                WeekBars.Clear();
                foreach (var d in week)
                    WeekBars.Add(new DayBar(d.Day.ToString("ddd"), d.Total, (double)(d.Total / max) * 110, d.Day.Date == DateTime.Today));
                WeekTotal = week.Sum(d => d.Total);

                int expired = _allAttention.Count(a => a.IsExpiry && a.DaysLeft < 0);
                int expiring = _allAttention.Count(a => a.IsExpiry && a.DaysLeft >= 0);
                int low = _allAttention.Count(a => !a.IsExpiry);
                AttentionCount = _allAttention.Count;
                AttentionSummary = AttentionCount == 0 ? "all good" : $"{expired} expired · {expiring} expiring · {low} low";
                OnPropertyChanged(nameof(AllChipText));
                OnPropertyChanged(nameof(ExpiryChipText));
                OnPropertyChanged(nameof(LowChipText));
                ApplyAttentionFilter();

                RecentSales.Clear();
                foreach (var s in recent)
                    RecentSales.Add(new RecentSaleItem { Invoice = s.Invoice, Date = s.Date, Total = s.Total, Method = s.Method });
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

    public record DayBar(string Day, decimal Total, double BarHeight, bool IsToday)
    {
        public string Tooltip => $"{Day}: PKR {Total:N0}";
    }

    public class RecentSaleItem
    {
        public string Invoice { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal Total { get; set; }
        public string Method { get; set; } = string.Empty;
        public string TimeText => Date.ToLocalTime().ToString("HH:mm");
    }
}
