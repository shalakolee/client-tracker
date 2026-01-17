using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using ClientTracker.Models;
using ClientTracker.Services;
using Microsoft.Maui.ApplicationModel;

namespace ClientTracker.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly DatabaseService _database;
    private readonly LocalizationResourceManager _localization;
    private int _clientCount;
    private int _saleCount;
    private decimal _monthCommission;
    private decimal _totalSalesAmount;
    private decimal _totalCommissionAmount;
    private decimal _outstandingCommissionAmount;
    private string _totalSalesAmountText = string.Empty;
    private string _outstandingCommissionAmountText = string.Empty;
    private int _selectedCommissionIndex;
    private DashboardCommissionSummary? _selectedCommissionSummary;
    private string _trendRangeLabel = string.Empty;
    private IReadOnlyList<double> _trendSales = Array.Empty<double>();
    private IReadOnlyList<double> _trendCommission = Array.Empty<double>();
    private IReadOnlyList<string> _trendLabels = Array.Empty<string>();

    public DashboardViewModel(DatabaseService database, LocalizationResourceManager localization)
    {
        _database = database;
        _localization = localization;
        MonthlySummaries = new ObservableCollection<DashboardMonthSummary>();
        CommissionSummaries = new ObservableCollection<DashboardCommissionSummary>();
        RefreshCommand = new Command(async () => await LoadAsync());
        OpenClientsCommand = new Command(async () => await Shell.Current.GoToAsync("//ClientsPage"));
        OpenSalesCommand = new Command(async () => await Shell.Current.GoToAsync("//SalesPage"));
        OpenCalendarCommand = new Command(async () => await Shell.Current.GoToAsync("//PayCalendarPage"));
        _localization.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == "Item[]" || args.PropertyName == nameof(LocalizationResourceManager.CurrentCulture))
            {
                UpdateCurrencyText();
                OnPropertyChanged(nameof(TrendRangeLabel));
            }
        };
    }

    public int ClientCount
    {
        get => _clientCount;
        set => SetProperty(ref _clientCount, value);
    }

    public int SaleCount
    {
        get => _saleCount;
        set => SetProperty(ref _saleCount, value);
    }

    public decimal MonthCommission
    {
        get => _monthCommission;
        set => SetProperty(ref _monthCommission, value);
    }

    public decimal TotalSalesAmount
    {
        get => _totalSalesAmount;
        set
        {
            if (SetProperty(ref _totalSalesAmount, value))
            {
                UpdateCurrencyText();
            }
        }
    }

    public string TotalSalesAmountText
    {
        get => _totalSalesAmountText;
        private set => SetProperty(ref _totalSalesAmountText, value);
    }

    public decimal TotalCommissionAmount
    {
        get => _totalCommissionAmount;
        set => SetProperty(ref _totalCommissionAmount, value);
    }

    public decimal OutstandingCommissionAmount
    {
        get => _outstandingCommissionAmount;
        set
        {
            if (SetProperty(ref _outstandingCommissionAmount, value))
            {
                UpdateCurrencyText();
            }
        }
    }

    public string OutstandingCommissionAmountText
    {
        get => _outstandingCommissionAmountText;
        private set => SetProperty(ref _outstandingCommissionAmountText, value);
    }

    public ObservableCollection<DashboardMonthSummary> MonthlySummaries { get; }
    public ObservableCollection<DashboardCommissionSummary> CommissionSummaries { get; }

    public int SelectedCommissionIndex
    {
        get => _selectedCommissionIndex;
        set => SetProperty(ref _selectedCommissionIndex, value);
    }

    public DashboardCommissionSummary? SelectedCommissionSummary
    {
        get => _selectedCommissionSummary;
        set => SetProperty(ref _selectedCommissionSummary, value);
    }

    public IReadOnlyList<double> TrendSales
    {
        get => _trendSales;
        set => SetProperty(ref _trendSales, value);
    }

    public IReadOnlyList<double> TrendCommission
    {
        get => _trendCommission;
        set => SetProperty(ref _trendCommission, value);
    }

    public IReadOnlyList<string> TrendLabels
    {
        get => _trendLabels;
        set => SetProperty(ref _trendLabels, value);
    }

    public string TrendRangeLabel
    {
        get => _trendRangeLabel;
        set => SetProperty(ref _trendRangeLabel, value);
    }

    public Command RefreshCommand { get; }
    public Command OpenClientsCommand { get; }
    public Command OpenSalesCommand { get; }
    public Command OpenCalendarCommand { get; }

    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            ClientCount = await _database.GetClientCountAsync();
            var sales = await _database.GetAllSalesAsync();
            SaleCount = sales.Count;
            TotalSalesAmount = sales.Sum(s => s.Amount);

            var payments = await _database.GetAllPaymentsAsync();
            TotalCommissionAmount = payments.Sum(p => p.Commission);
            OutstandingCommissionAmount = TotalCommissionAmount;
            UpdateCurrencyText();

            var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var monthPayments = await _database.GetPaymentsForMonthAsync(month);
            MonthCommission = monthPayments.Sum(p => p.Commission);

            var (selectedIndex, selectedSummary) = BuildCommissionSummaries(payments);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SelectedCommissionIndex = selectedIndex;
                SelectedCommissionSummary = selectedSummary;
            });

            BuildMonthlySummaries(sales, payments);
            BuildTrends();
        });
    }

    private void BuildMonthlySummaries(IReadOnlyCollection<Sale> sales, IReadOnlyCollection<Payment> payments)
    {
        MonthlySummaries.Clear();
        var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
        var months = Enumerable.Range(0, 12).Select(i => start.AddMonths(i)).ToList();
        var summaries = months.Select(month =>
        {
            var monthStart = new DateTime(month.Year, month.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var monthSales = sales.Where(s => s.SaleDate >= monthStart && s.SaleDate <= monthEnd).ToList();
            var monthPayments = payments.Where(p => p.PayDate >= monthStart && p.PayDate <= monthEnd).ToList();
            return new DashboardMonthSummary
            {
                MonthLabel = BuildMonthLabel(monthStart),
                SalesAmount = monthSales.Sum(s => s.Amount),
                CommissionAmount = monthPayments.Sum(p => p.Commission)
            };
        }).ToList();

        var maxSales = summaries.Max(s => s.SalesAmount);
        var maxCommission = summaries.Max(s => s.CommissionAmount);
        foreach (var summary in summaries)
        {
            summary.SalesRatio = maxSales == 0 ? 0 : (double)(summary.SalesAmount / maxSales);
            summary.CommissionRatio = maxCommission == 0 ? 0 : (double)(summary.CommissionAmount / maxCommission);
            MonthlySummaries.Add(summary);
        }

        if (months.Count > 0)
        {
            var first = months[0];
            var last = months[^1];
            TrendRangeLabel = $"{first:MMM yyyy} - {last:MMM yyyy}";
        }
    }

    private void BuildTrends()
    {
        TrendLabels = MonthlySummaries.Select(m => m.MonthLabel).ToArray();
        TrendSales = MonthlySummaries.Select(m => (double)m.SalesAmount).ToArray();
        TrendCommission = MonthlySummaries.Select(m => (double)m.CommissionAmount).ToArray();
    }

    private void UpdateCurrencyText()
    {
        var culture = CultureInfo.CurrentCulture;
        TotalSalesAmountText = TotalSalesAmount.ToString("C", culture);
        OutstandingCommissionAmountText = OutstandingCommissionAmount.ToString("C", culture);
    }

    private (int index, DashboardCommissionSummary? summary) BuildCommissionSummaries(IReadOnlyCollection<Payment> payments)
    {
        CommissionSummaries.Clear();
        var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
        var months = Enumerable.Range(0, 12).Select(i => start.AddMonths(i)).ToList();
        var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var selectedIndex = 0;
        DashboardCommissionSummary? selectedSummary = null;

        for (var i = 0; i < months.Count; i++)
        {
            var monthStart = new DateTime(months[i].Year, months[i].Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var midMonth = monthStart.AddDays(14);
            var monthPayments = payments
                .Where(p => p.PayDate >= monthStart && p.PayDate <= monthEnd)
                .ToList();
            var midMonthAmount = monthPayments
                .Where(p => p.PayDate.Date == midMonth.Date)
                .Sum(p => p.Commission);
            var endMonthAmount = monthPayments
                .Where(p => p.PayDate.Date == monthEnd.Date)
                .Sum(p => p.Commission);

            var monthLabel = monthStart.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
            var summary = new DashboardCommissionSummary
            {
                Title = string.Format(_localization["Dashboard_UpcomingCommission"], monthLabel),
                CommissionAmount = monthPayments.Sum(p => p.Commission),
                PaymentCount = monthPayments.Count,
                MidMonthAmount = midMonthAmount,
                EndMonthAmount = endMonthAmount
            };
            CommissionSummaries.Add(summary);

            if (monthStart == currentMonth)
            {
                selectedIndex = i;
                selectedSummary = summary;
            }
        }

        return (selectedIndex, selectedSummary ?? CommissionSummaries.ElementAtOrDefault(selectedIndex));
    }

    private static string BuildMonthLabel(DateTime monthStart)
    {
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            return monthStart.ToString("MMM", CultureInfo.CurrentCulture);
        }

        return monthStart.ToString("MMM yyyy", CultureInfo.CurrentCulture);
    }
}

public class DashboardMonthSummary
{
    public string MonthLabel { get; set; } = string.Empty;
    public decimal SalesAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public double SalesRatio { get; set; }
    public double CommissionRatio { get; set; }
}

public class DashboardCommissionSummary
{
    public string Title { get; set; } = string.Empty;
    public decimal CommissionAmount { get; set; }
    public int PaymentCount { get; set; }
    public decimal MidMonthAmount { get; set; }
    public decimal EndMonthAmount { get; set; }
}
