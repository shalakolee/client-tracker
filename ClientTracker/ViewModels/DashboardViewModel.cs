using System.Collections.ObjectModel;
using System.Linq;
using ClientTracker.Models;
using ClientTracker.Services;

namespace ClientTracker.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly DatabaseService _database;
    private int _clientCount;
    private int _saleCount;
    private decimal _monthCommission;
    private decimal _totalSalesAmount;
    private decimal _totalCommissionAmount;
    private decimal _outstandingCommissionAmount;
    private int _upcomingPaymentCount;
    private decimal _upcomingCommissionAmount;

    public DashboardViewModel(DatabaseService database)
    {
        _database = database;
        MonthlySummaries = new ObservableCollection<DashboardMonthSummary>();
        RefreshCommand = new Command(async () => await LoadAsync());
        OpenClientsCommand = new Command(async () => await Shell.Current.GoToAsync("//Clients"));
        OpenSalesCommand = new Command(async () => await Shell.Current.GoToAsync("//Sales"));
        OpenCalendarCommand = new Command(async () => await Shell.Current.GoToAsync("//PayCalendar"));
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
        set => SetProperty(ref _totalSalesAmount, value);
    }

    public decimal TotalCommissionAmount
    {
        get => _totalCommissionAmount;
        set => SetProperty(ref _totalCommissionAmount, value);
    }

    public decimal OutstandingCommissionAmount
    {
        get => _outstandingCommissionAmount;
        set => SetProperty(ref _outstandingCommissionAmount, value);
    }

    public int UpcomingPaymentCount
    {
        get => _upcomingPaymentCount;
        set => SetProperty(ref _upcomingPaymentCount, value);
    }

    public decimal UpcomingCommissionAmount
    {
        get => _upcomingCommissionAmount;
        set => SetProperty(ref _upcomingCommissionAmount, value);
    }

    public ObservableCollection<DashboardMonthSummary> MonthlySummaries { get; }

    public Command RefreshCommand { get; }
    public Command OpenClientsCommand { get; }
    public Command OpenSalesCommand { get; }
    public Command OpenCalendarCommand { get; }

    public async Task LoadAsync()
    {
        ClientCount = await _database.GetClientCountAsync();
        var sales = await _database.GetAllSalesAsync();
        SaleCount = sales.Count;
        TotalSalesAmount = sales.Sum(s => s.Amount);

        var payments = await _database.GetAllPaymentsAsync();
        TotalCommissionAmount = payments.Sum(p => p.Commission);
        OutstandingCommissionAmount = payments.Where(p => !p.IsPaid).Sum(p => p.Commission);

        var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var monthPayments = await _database.GetPaymentsForMonthAsync(month);
        MonthCommission = monthPayments.Sum(p => p.Commission);

        var upcomingEnd = DateTime.Today.AddDays(30);
        var upcomingPayments = payments.Where(p => p.PayDate >= DateTime.Today && p.PayDate <= upcomingEnd && !p.IsPaid).ToList();
        UpcomingPaymentCount = upcomingPayments.Count;
        UpcomingCommissionAmount = upcomingPayments.Sum(p => p.Commission);

        BuildMonthlySummaries(sales, payments);
    }

    private void BuildMonthlySummaries(IReadOnlyCollection<Sale> sales, IReadOnlyCollection<Payment> payments)
    {
        MonthlySummaries.Clear();
        var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
        var months = Enumerable.Range(0, 6).Select(i => start.AddMonths(i)).ToList();
        var summaries = months.Select(month =>
        {
            var monthStart = new DateTime(month.Year, month.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var monthSales = sales.Where(s => s.SaleDate >= monthStart && s.SaleDate <= monthEnd).ToList();
            var monthPayments = payments.Where(p => p.PayDate >= monthStart && p.PayDate <= monthEnd).ToList();
            return new DashboardMonthSummary
            {
                MonthLabel = monthStart.ToString("MMM yyyy"),
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
