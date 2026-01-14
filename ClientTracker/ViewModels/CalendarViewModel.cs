using System.Collections.ObjectModel;
using System.Linq;
using ClientTracker.Models;
using ContactModel = ClientTracker.Models.Contact;
using ClientTracker.Services;

namespace ClientTracker.ViewModels;

public class CalendarViewModel : ViewModelBase
{
    private readonly DatabaseService _database;
    private DateTime _selectedMonth;
    private DateTime _monthPickerDate;
    private decimal _monthTotal;
    private string _statusMessage = string.Empty;
    private CalendarDay? _selectedCalendarDay;
    private string _selectedPayDateLabel = LocalizationResourceManager.Instance["PayCalendar_SelectDatePrompt"];
    private decimal _paidCommissionTotal;
    private decimal _unpaidCommissionTotal;
    private int _paidPaymentCount;
    private int _unpaidPaymentCount;

    public CalendarViewModel(DatabaseService database)
    {
        _database = database;
        _selectedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _monthPickerDate = _selectedMonth;
        PaySummaries = new ObservableCollection<PayDateSummary>();
        PaymentSchedule = new ObservableCollection<PaymentScheduleItem>();
        CalendarDays = new ObservableCollection<CalendarDay>();
        SelectedPayDatePayments = new ObservableCollection<PaymentScheduleItem>();

        PreviousMonthCommand = new Command(async () => await ShiftMonthAsync(-1));
        NextMonthCommand = new Command(async () => await ShiftMonthAsync(1));
        RefreshCommand = new Command(async () => await LoadAsync());
    }

    public ObservableCollection<PayDateSummary> PaySummaries { get; }
    public ObservableCollection<PaymentScheduleItem> PaymentSchedule { get; }
    public ObservableCollection<CalendarDay> CalendarDays { get; }
    public ObservableCollection<PaymentScheduleItem> SelectedPayDatePayments { get; }

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

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string MonthLabel => SelectedMonth.ToString("MMMM yyyy");

    public CalendarDay? SelectedCalendarDay
    {
        get => _selectedCalendarDay;
        set
        {
            if (value is not null && !value.IsPayDate)
            {
                SetProperty(ref _selectedCalendarDay, null);
                UpdateSelectedPayDatePayments();
                return;
            }

            if (SetProperty(ref _selectedCalendarDay, value))
            {
                UpdateSelectedDaySelection();
                UpdateSelectedPayDatePayments();
            }
        }
    }

    public string SelectedPayDateLabel
    {
        get => _selectedPayDateLabel;
        set => SetProperty(ref _selectedPayDateLabel, value);
    }

    public decimal PaidCommissionTotal
    {
        get => _paidCommissionTotal;
        set => SetProperty(ref _paidCommissionTotal, value);
    }

    public decimal UnpaidCommissionTotal
    {
        get => _unpaidCommissionTotal;
        set => SetProperty(ref _unpaidCommissionTotal, value);
    }

    public int PaidPaymentCount
    {
        get => _paidPaymentCount;
        set => SetProperty(ref _paidPaymentCount, value);
    }

    public int UnpaidPaymentCount
    {
        get => _unpaidPaymentCount;
        set => SetProperty(ref _unpaidPaymentCount, value);
    }

    public Command PreviousMonthCommand { get; }
    public Command NextMonthCommand { get; }
    public Command RefreshCommand { get; }

    public async Task LoadAsync()
    {
        StatusMessage = string.Empty;
        var payments = await _database.GetPaymentsForMonthAsync(SelectedMonth);
        var sales = await _database.GetAllSalesAsync();
        var clients = await _database.GetClientsAsync();
        var contacts = await _database.GetAllContactsAsync();

        var summaries = BuildPaySummaries(payments);
        var scheduleItems = BuildScheduleItems(payments, sales, clients, contacts);
        var calendarDays = BuildCalendarDays(SelectedMonth, payments);

        PaySummaries.Clear();
        foreach (var summary in summaries)
        {
            PaySummaries.Add(summary);
        }

        PaymentSchedule.Clear();
        foreach (var item in scheduleItems)
        {
            PaymentSchedule.Add(item);
        }

        CalendarDays.Clear();
        foreach (var day in calendarDays)
        {
            CalendarDays.Add(day);
        }

        MonthTotal = summaries.Sum(s => s.TotalCommission);
        OnPropertyChanged(nameof(MonthLabel));

        if (SelectedCalendarDay is not null)
        {
            var selectedDate = SelectedCalendarDay.Date.Date;
            SelectedCalendarDay = CalendarDays.FirstOrDefault(d => d.Date.Date == selectedDate);
        }

        UpdateSelectedPayDatePayments();
        RecalculateTotals();
    }

    private async Task ShiftMonthAsync(int offset)
    {
        SelectedMonth = SelectedMonth.AddMonths(offset);
        await LoadAsync();
    }

    private static List<PayDateSummary> BuildPaySummaries(IEnumerable<Payment> payments)
    {
        return payments
            .GroupBy(p => p.PayDate)
            .OrderBy(g => g.Key)
            .Select(g => new PayDateSummary
            {
                PayDate = g.Key,
                TotalCommission = g.Sum(p => p.Commission),
                PaymentCount = g.Count()
            })
            .ToList();
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
                IsPaid = payment.IsPaid,
                PaidDateUtc = payment.PaidDateUtc
            };
        }).OrderBy(p => p.PayDate).ThenBy(p => p.PaymentDate).ToList();
    }

    public async Task UpdatePaymentStatusAsync(PaymentScheduleItem item)
    {
        var payment = new Payment
        {
            Id = item.PaymentId,
            IsPaid = item.IsPaid,
            PaidDateUtc = item.IsPaid ? DateTime.UtcNow : null
        };

        await _database.UpdatePaymentAsync(payment);
        item.PaidDateUtc = payment.PaidDateUtc;
        RecalculateTotals();
    }

    private void UpdateSelectedPayDatePayments()
    {
        SelectedPayDatePayments.Clear();
        if (SelectedCalendarDay is null || SelectedCalendarDay.CommissionTotal <= 0m)
        {
            SelectedPayDateLabel = LocalizationResourceManager.Instance["PayCalendar_SelectDatePrompt"];
            return;
        }

        var selectedDate = SelectedCalendarDay.Date.Date;
        var matches = PaymentSchedule
            .Where(p => p.PayDate.Date == selectedDate)
            .OrderBy(p => p.ClientName)
            .ThenBy(p => p.PaymentDate)
            .ToList();

        foreach (var item in matches)
        {
            SelectedPayDatePayments.Add(item);
        }

        var labelTemplate = LocalizationResourceManager.Instance["PayCalendar_SelectedDatePayments"];
        SelectedPayDateLabel = string.Format(labelTemplate, selectedDate.ToString("MMMM dd, yyyy"));
    }

    private void RecalculateTotals()
    {
        PaidCommissionTotal = PaymentSchedule.Where(p => p.IsPaid).Sum(p => p.Commission);
        UnpaidCommissionTotal = PaymentSchedule.Where(p => !p.IsPaid).Sum(p => p.Commission);
        PaidPaymentCount = PaymentSchedule.Count(p => p.IsPaid);
        UnpaidPaymentCount = PaymentSchedule.Count(p => !p.IsPaid);
    }

    private void UpdateSelectedDaySelection()
    {
        var selectedDate = SelectedCalendarDay?.Date.Date;
        foreach (var day in CalendarDays)
        {
            day.IsSelected = selectedDate.HasValue && day.Date.Date == selectedDate.Value;
        }
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

    private static List<CalendarDay> BuildCalendarDays(DateTime month, IEnumerable<Payment> payments)
    {
        var firstDay = new DateTime(month.Year, month.Month, 1);
        var startOffset = (int)firstDay.DayOfWeek;
        var startDate = firstDay.AddDays(-startOffset);
        var paymentTotals = payments
            .GroupBy(p => p.PayDate.Date)
            .ToDictionary(g => g.Key, g => new
            {
                Total = g.Sum(p => p.Commission),
                Count = g.Count()
            });

        var days = new List<CalendarDay>();
        for (var i = 0; i < 42; i++)
        {
            var date = startDate.AddDays(i);
            paymentTotals.TryGetValue(date.Date, out var summary);
            days.Add(new CalendarDay
            {
                Date = date,
                DayLabel = date.Day.ToString(),
                IsCurrentMonth = date.Month == month.Month,
                CommissionTotal = summary?.Total ?? 0m,
                PaymentCount = summary?.Count ?? 0,
                IsPayDate = summary is not null
            });
        }

        return days;
    }
}
