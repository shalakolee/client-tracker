using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using ClientTracker.Models;
using ContactModel = ClientTracker.Models.Contact;
using ClientTracker.Services;

namespace ClientTracker.ViewModels;

public class SaleDetailsViewModel : ViewModelBase
{
    private readonly DatabaseService _database;
    private int _saleId;
    private int _clientId;
    private string _clientName = string.Empty;
    private string _invoiceNumber = string.Empty;
    private DateTime _saleDate = DateTime.Today;
    private string _amount = string.Empty;
    private string _commissionPercent = string.Empty;
    private ContactModel? _selectedContact;
    private string _statusMessage = string.Empty;

    public SaleDetailsViewModel(DatabaseService database)
    {
        _database = database;
        Contacts = new ObservableCollection<ContactModel>();
        Payments = new ObservableCollection<PaymentEditItem>();
        RefreshCommand = new Command(async () => await RefreshAsync());
        SaveSaleCommand = new Command(async () => await SaveSaleAsync(), () => SaleId > 0);
        SavePaymentsCommand = new Command(async () => await SavePaymentsAsync(), () => Payments.Count > 0);
        DeleteSaleCommand = new Command(async () => await DeleteSaleAsync(), () => SaleId > 0);
    }

    public int SaleId
    {
        get => _saleId;
        set
        {
            if (SetProperty(ref _saleId, value))
            {
                SaveSaleCommand.ChangeCanExecute();
                DeleteSaleCommand.ChangeCanExecute();
            }
        }
    }

    public int ClientId
    {
        get => _clientId;
        set => SetProperty(ref _clientId, value);
    }

    public string ClientName
    {
        get => _clientName;
        set => SetProperty(ref _clientName, value);
    }

    public string InvoiceNumber
    {
        get => _invoiceNumber;
        set => SetProperty(ref _invoiceNumber, value);
    }

    public DateTime SaleDate
    {
        get => _saleDate;
        set => SetProperty(ref _saleDate, value);
    }

    public string Amount
    {
        get => _amount;
        set => SetProperty(ref _amount, value);
    }

    public string CommissionPercent
    {
        get => _commissionPercent;
        set => SetProperty(ref _commissionPercent, value);
    }

    public ContactModel? SelectedContact
    {
        get => _selectedContact;
        set => SetProperty(ref _selectedContact, value);
    }

    public ObservableCollection<ContactModel> Contacts { get; }
    public ObservableCollection<PaymentEditItem> Payments { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public Command SaveSaleCommand { get; }
    public Command RefreshCommand { get; }
    public Command SavePaymentsCommand { get; }
    public Command DeleteSaleCommand { get; }

    public async Task LoadAsync(int saleId)
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = string.Empty;
            var sale = await _database.GetSaleByIdAsync(saleId);
            if (sale is null)
            {
                StatusMessage = "Sale not found.";
                return;
            }

            SaleId = sale.Id;
            ClientId = sale.ClientId;
            InvoiceNumber = sale.InvoiceNumber;
            SaleDate = sale.SaleDate;
            Amount = sale.Amount.ToString("0.00", CultureInfo.CurrentCulture);
            CommissionPercent = sale.CommissionPercent.ToString("0.##", CultureInfo.CurrentCulture);

            var client = await _database.GetClientByIdAsync(sale.ClientId);
            ClientName = client?.Name ?? string.Empty;

            Contacts.Clear();
            var contacts = await _database.GetContactsForClientAsync(sale.ClientId);
            foreach (var contact in contacts)
            {
                Contacts.Add(contact);
            }

            SelectedContact = Contacts.FirstOrDefault(c => c.Id == sale.ContactId);
            await LoadPaymentsAsync(sale);
        });
    }

    private Task RefreshAsync()
    {
        if (SaleId <= 0)
        {
            return Task.CompletedTask;
        }

        return LoadAsync(SaleId);
    }

    private async Task LoadPaymentsAsync(Sale sale)
    {
        Payments.Clear();
        var payments = await _database.GetPaymentsForSaleAsync(sale.Id);
        foreach (var payment in payments)
        {
            Payments.Add(new PaymentEditItem
            {
                PaymentId = payment.Id,
                SaleId = sale.Id,
                PaymentDate = payment.PaymentDate,
                PayDate = payment.PayDate,
                Amount = payment.Amount,
                Commission = payment.Commission,
                IsPaid = true,
                PaymentNumber = GetPaymentNumber(sale.SaleDate, payment.PaymentDate)
            });
        }

        SavePaymentsCommand.ChangeCanExecute();
    }

    private async Task SaveSaleAsync()
    {
        StatusMessage = string.Empty;
        if (SelectedContact is null)
        {
            StatusMessage = "Select a contact.";
            return;
        }

        if (string.IsNullOrWhiteSpace(InvoiceNumber))
        {
            StatusMessage = "Enter an invoice number.";
            return;
        }

        if (!TryGetDecimal(Amount, out var amount) || amount <= 0)
        {
            StatusMessage = "Enter a valid sale amount.";
            return;
        }

        if (!TryGetDecimal(CommissionPercent, out var commissionPercent) || commissionPercent < 0)
        {
            StatusMessage = "Enter a valid commission percent.";
            return;
        }

        var sale = new Sale
        {
            Id = SaleId,
            ClientId = ClientId,
            ContactId = SelectedContact.Id,
            InvoiceNumber = InvoiceNumber.Trim(),
            SaleDate = SaleDate.Date,
            Amount = amount,
            CommissionPercent = commissionPercent
        };

        await _database.UpdateSaleAsync(sale, true);
        await LoadPaymentsAsync(sale);
        StatusMessage = "Sale updated.";
    }

    private async Task SavePaymentsAsync()
    {
        StatusMessage = string.Empty;
        if (!TryGetDecimal(CommissionPercent, out var commissionPercent) || commissionPercent < 0)
        {
            StatusMessage = "Enter a valid commission percent before saving payments.";
            return;
        }

        foreach (var payment in Payments)
        {
            var payDate = GetCommissionPayDate(payment.PaymentDate);
            var commission = decimal.Round(payment.Amount * (commissionPercent / 100m), 2, MidpointRounding.AwayFromZero);

            await _database.UpdatePaymentDetailsAsync(new Payment
            {
                Id = payment.PaymentId,
                PaymentDate = payment.PaymentDate.Date,
                PayDate = payDate,
                Amount = payment.Amount,
                Commission = commission,
                IsPaid = true,
                PaidDateUtc = DateTime.UtcNow
            });

            payment.PayDate = payDate;
            payment.Commission = commission;
            payment.PaymentNumber = GetPaymentNumber(SaleDate, payment.PaymentDate);
        }

        StatusMessage = "Payments updated.";
    }

    private async Task DeleteSaleAsync()
    {
        StatusMessage = string.Empty;
        if (SaleId <= 0)
        {
            return;
        }

        var confirm = await Shell.Current.DisplayAlertAsync("Confirm Delete", "Delete this sale and its payments?", "Delete", "Cancel");
        if (!confirm)
        {
            return;
        }

        await _database.DeleteSaleAsync(new Sale { Id = SaleId });
        await Shell.Current.GoToAsync("..");
    }

    private static bool TryGetDecimal(string input, out decimal value)
    {
        return decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
    }

    private static DateTime GetCommissionPayDate(DateTime paymentDate)
    {
        if (paymentDate.Day <= 15)
        {
            return new DateTime(paymentDate.Year, paymentDate.Month, 15);
        }

        var lastDay = DateTime.DaysInMonth(paymentDate.Year, paymentDate.Month);
        return new DateTime(paymentDate.Year, paymentDate.Month, lastDay);
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
