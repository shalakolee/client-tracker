using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClientTracker.Models;
using ContactModel = ClientTracker.Models.Contact;
using ClientTracker.Services;
using Microsoft.Maui.ApplicationModel;

namespace ClientTracker.ViewModels;

public class CalendarViewModel : ViewModelBase
{
    private readonly DatabaseService _database;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private bool _requestedInitialLoad;
    private DateTime _selectedMonth;
    private DateTime _monthPickerDate;
    private decimal _monthTotal;
    private int _monthPaymentCount;
    private decimal _averageCommission;
    private string _payDateRangeText = string.Empty;
    private string _statusMessage = string.Empty;
    private IReadOnlyList<PayDateGroup> _payDateGroups = Array.Empty<PayDateGroup>();

    public CalendarViewModel(DatabaseService database)
    {
        _database = database;
        _selectedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _monthPickerDate = _selectedMonth;

        PreviousMonthCommand = new Command(async () => await ShiftMonthAsync(-1));
        NextMonthCommand = new Command(async () => await ShiftMonthAsync(1));
        RefreshCommand = new Command(async () => await LoadAsync());
        TogglePayDateCommand = new Command<PayDateGroup>(TogglePayDate);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_requestedInitialLoad)
            {
                return;
            }

            _requestedInitialLoad = true;
            _ = LoadAsync();
        });
    }

    public IReadOnlyList<PayDateGroup> PayDateGroups
    {
        get => _payDateGroups;
        private set => SetProperty(ref _payDateGroups, value);
    }

    public DateTime SelectedMonth
    {
        get => _selectedMonth;
        set
        {
            if (SetProperty(ref _selectedMonth, value))
            {
                var pickerDate = new DateTime(value.Year, value.Month, 1);
                if (_monthPickerDate != pickerDate)
                {
                    _monthPickerDate = pickerDate;
                    OnPropertyChanged(nameof(MonthPickerDate));
                }

                OnPropertyChanged(nameof(MonthLabel));
            }
        }
    }

    public DateTime MonthPickerDate
    {
        get => _monthPickerDate;
        set
        {
            if (SetProperty(ref _monthPickerDate, value))
            {
                SelectedMonth = new DateTime(value.Year, value.Month, 1);
                _ = LoadAsync();
            }
        }
    }

    public decimal MonthTotal
    {
        get => _monthTotal;
        set => SetProperty(ref _monthTotal, value);
    }

    public int MonthPaymentCount
    {
        get => _monthPaymentCount;
        set => SetProperty(ref _monthPaymentCount, value);
    }

    public decimal AverageCommission
    {
        get => _averageCommission;
        set => SetProperty(ref _averageCommission, value);
    }

    public string PayDateRangeText
    {
        get => _payDateRangeText;
        set => SetProperty(ref _payDateRangeText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string MonthLabel => SelectedMonth.ToString("MMMM yyyy");

    public Command PreviousMonthCommand { get; }
    public Command NextMonthCommand { get; }
    public Command RefreshCommand { get; }
    public Command<PayDateGroup> TogglePayDateCommand { get; }

    public async Task LoadAsync()
    {
        if (!await _loadGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            var month = SelectedMonth;
            StartupLog.Write($"CalendarViewModel.LoadAsync start month={month:yyyy-MM} db={_database.DatabasePath}");

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsBusy = true;
                StatusMessage = string.Empty;
            });

            var payments = await _database.GetPaymentsForMonthAsync(month).ConfigureAwait(false);
            var sales = await _database.GetAllSalesAsync().ConfigureAwait(false);
            var clients = await _database.GetClientsAsync().ConfigureAwait(false);
            var contacts = await _database.GetAllContactsAsync().ConfigureAwait(false);
            var expandedDates = PayDateGroups
                .Where(group => group.IsExpanded)
                .Select(group => group.PayDate.Date)
                .ToHashSet();

            var result = await Task.Run(() =>
            {
                var scheduleItems = BuildScheduleItems(payments, sales, clients, contacts);
                var groups = BuildPayDateGroups(scheduleItems, expandedDates);
                var monthTotal = groups.Sum(g => g.TotalCommission);
                var paymentCount = scheduleItems.Count;
                var averageCommission = paymentCount == 0 ? 0m : monthTotal / paymentCount;
                var rangeText = BuildRangeText(scheduleItems);
                return (groups, monthTotal, paymentCount, averageCommission, rangeText);
            }).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                PayDateGroups = result.groups;
                MonthTotal = result.monthTotal;
                MonthPaymentCount = result.paymentCount;
                AverageCommission = result.averageCommission;
                PayDateRangeText = result.rangeText;
                OnPropertyChanged(nameof(MonthLabel));

                if (payments.Count == 0)
                {
                    StatusMessage = "No payments found for this month.";
                }
            });

            if (payments.Count == 0)
            {
                var totalPaymentCount = await _database.GetPaymentCountAsync().ConfigureAwait(false);
                if (totalPaymentCount == 0)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                        StatusMessage = "No payments yet. Create a sale to generate a payment schedule.");
                }
                else
                {
                    var range = await _database.GetPaymentPayDateRangeAsync().ConfigureAwait(false);
                    if (range.MinPayDate is not null && range.MaxPayDate is not null)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                            StatusMessage = $"No payments found for {month:MMMM yyyy}. Existing pay dates: {range.MinPayDate.Value:d} – {range.MaxPayDate.Value:d}.");
                    }
                }
            }

            StartupLog.Write($"CalendarViewModel.LoadAsync done payments={payments.Count} groups={result.groups.Count}");
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex, "CalendarViewModel.LoadAsync");
            await MainThread.InvokeOnMainThreadAsync(() => StatusMessage = "Unable to load payment calendar.");
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            _loadGate.Release();
        }
    }

    private async Task ShiftMonthAsync(int offset)
    {
        SelectedMonth = SelectedMonth.AddMonths(offset);
        await LoadAsync();
    }

    private void TogglePayDate(PayDateGroup? group)
    {
        if (group is null)
        {
            return;
        }

        group.IsExpanded = !group.IsExpanded;
    }

    private static List<PayDateGroup> BuildPayDateGroups(IReadOnlyList<PaymentScheduleItem> scheduleItems, ISet<DateTime> expandedDates)
    {
        return scheduleItems
            .GroupBy(item => item.PayDate.Date)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var payments = group
                    .OrderBy(item => item.ClientName)
                    .ThenBy(item => item.PaymentDate)
                    .ToList();

                return new PayDateGroup
                {
                    PayDate = group.Key,
                    TotalCommission = group.Sum(item => item.Commission),
                    PaymentCount = group.Count(),
                    Payments = payments,
                    IsExpanded = expandedDates.Contains(group.Key)
                };
            })
            .ToList();
    }

    private static string BuildRangeText(IReadOnlyList<PaymentScheduleItem> scheduleItems)
    {
        if (scheduleItems.Count == 0)
        {
            return LocalizationResourceManager.Instance["PayCalendar_NoRange"];
        }

        var minDate = scheduleItems.Min(item => item.PayDate.Date);
        var maxDate = scheduleItems.Max(item => item.PayDate.Date);
        var culture = CultureInfo.CurrentCulture;

        if (minDate == maxDate)
        {
            return minDate.ToString("MMM d, yyyy", culture);
        }

        if (minDate.Year == maxDate.Year)
        {
            if (minDate.Month == maxDate.Month)
            {
                return $"{minDate.ToString("MMM d", culture)} - {maxDate.ToString("MMM d, yyyy", culture)}";
            }

            return $"{minDate.ToString("MMM d", culture)} - {maxDate.ToString("MMM d, yyyy", culture)}";
        }

        return $"{minDate.ToString("MMM d, yyyy", culture)} - {maxDate.ToString("MMM d, yyyy", culture)}";
    }

    private static List<PaymentScheduleItem> BuildScheduleItems(IEnumerable<Payment> payments, IEnumerable<Sale> sales, IEnumerable<Client> clients, IEnumerable<ContactModel> contacts)
    {
        var salesById = sales.ToDictionary(s => s.Id, s => s);
        var clientsById = clients.ToDictionary(c => c.Id, c => c);
        var contactsById = contacts.ToDictionary(c => c.Id, c => c);

        return payments.Select(payment =>
        {
            salesById.TryGetValue(payment.SaleId, out var sale);
            var clientName = string.Empty;
            if (sale is not null && clientsById.TryGetValue(sale.ClientId, out var client))
            {
                clientName = client.Name;
            }

            var contactName = string.Empty;
            if (sale is not null && contactsById.TryGetValue(sale.ContactId, out var contact))
            {
                contactName = contact.Name;
            }

            return new PaymentScheduleItem
            {
                PaymentId = payment.Id,
                PayDate = payment.PayDate,
                PaymentDate = payment.PaymentDate,
                PaymentNumber = sale is null ? 0 : GetPaymentNumber(sale.SaleDate, payment.PaymentDate),
                ClientName = clientName,
                ContactName = contactName,
                InvoiceNumber = sale?.InvoiceNumber ?? string.Empty,
                SaleAmount = sale?.Amount ?? 0m,
                PaymentAmount = payment.Amount,
                Commission = payment.Commission,
                IsPaid = true,
                PaidDateUtc = payment.PaidDateUtc
            };
        }).OrderBy(p => p.PayDate).ThenBy(p => p.PaymentDate).ToList();
    }

    private static int GetPaymentNumber(DateTime saleDate, DateTime paymentDate)
    {
        var days = (paymentDate.Date - saleDate.Date).Days;
        return days switch
        {
            25 => 1,
            30 => 2,
            35 => 3,
            _ => 0
        };
    }
}
