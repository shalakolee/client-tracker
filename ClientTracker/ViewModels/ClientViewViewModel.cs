using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ClientTracker.Models;
using ContactModel = ClientTracker.Models.Contact;
using ClientTracker.Services;

namespace ClientTracker.ViewModels;

public class ClientViewViewModel : ViewModelBase
{
    private readonly DatabaseService _database;
    private int _clientId;
    private string _clientName = string.Empty;
    private string _addressLine1 = string.Empty;
    private string _addressLine2 = string.Empty;
    private string _city = string.Empty;
    private string _stateProvince = string.Empty;
    private string _postalCode = string.Empty;
    private string _country = string.Empty;
    private string _taxId = string.Empty;
    private string _statusMessage = string.Empty;
    private string _locationLine = string.Empty;
    private int _saleCount;
    private decimal _totalSaleAmount;
    private decimal _totalCommissionAmount;
    private decimal _outstandingCommissionAmount;

    public ClientViewViewModel(DatabaseService database)
    {
        _database = database;
        Contacts = new ObservableCollection<ContactModel>();
        Sales = new ObservableCollection<SaleEntry>();
        EditCommand = new Command(async () => await OpenEditAsync(), () => ClientId > 0);
        DeleteCommand = new Command(async () => await DeleteClientAsync(), () => ClientId > 0);
        AddContactCommand = new Command(async () => await OpenAddContactAsync(), () => ClientId > 0);
        ViewContactCommand = new Command<ContactModel>(async contact => await OpenContactAsync(contact));
        EditContactCommand = new Command<ContactModel>(async contact => await OpenEditContactAsync(contact));
        ViewSaleCommand = new Command<SaleEntry>(async sale => await OpenSaleAsync(sale));
        EditSaleCommand = new Command<SaleEntry>(async sale => await OpenSaleAsync(sale));
        AddSaleCommand = new Command(async () => await OpenAddSaleAsync(), () => ClientId > 0);
    }

    public int ClientId
    {
        get => _clientId;
        set
        {
            if (SetProperty(ref _clientId, value))
            {
                EditCommand.ChangeCanExecute();
                AddSaleCommand.ChangeCanExecute();
                AddContactCommand.ChangeCanExecute();
                DeleteCommand.ChangeCanExecute();
            }
        }
    }

    public string ClientName
    {
        get => _clientName;
        set => SetProperty(ref _clientName, value);
    }

    public string AddressLine1
    {
        get => _addressLine1;
        set => SetProperty(ref _addressLine1, value);
    }

    public string AddressLine2
    {
        get => _addressLine2;
        set => SetProperty(ref _addressLine2, value);
    }

    public string City
    {
        get => _city;
        set => SetProperty(ref _city, value);
    }

    public string StateProvince
    {
        get => _stateProvince;
        set => SetProperty(ref _stateProvince, value);
    }

    public string PostalCode
    {
        get => _postalCode;
        set => SetProperty(ref _postalCode, value);
    }

    public string Country
    {
        get => _country;
        set => SetProperty(ref _country, value);
    }

    public string TaxId
    {
        get => _taxId;
        set => SetProperty(ref _taxId, value);
    }

    public string LocationLine
    {
        get => _locationLine;
        set => SetProperty(ref _locationLine, value);
    }

    public ObservableCollection<ContactModel> Contacts { get; }
    public ObservableCollection<SaleEntry> Sales { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public int SaleCount
    {
        get => _saleCount;
        set => SetProperty(ref _saleCount, value);
    }

    public decimal TotalSaleAmount
    {
        get => _totalSaleAmount;
        set => SetProperty(ref _totalSaleAmount, value);
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

    public Command EditCommand { get; }
    public Command DeleteCommand { get; }
    public Command AddContactCommand { get; }
    public Command<ContactModel> ViewContactCommand { get; }
    public Command<ContactModel> EditContactCommand { get; }
    public Command<SaleEntry> ViewSaleCommand { get; }
    public Command<SaleEntry> EditSaleCommand { get; }
    public Command AddSaleCommand { get; }

    public async Task LoadAsync(int clientId)
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = string.Empty;
            var client = await _database.GetClientByIdAsync(clientId);
            if (client is null)
            {
                StatusMessage = "Client not found.";
                return;
            }

            ClientId = client.Id;
            ClientName = client.Name;
            AddressLine1 = client.AddressLine1;
            AddressLine2 = client.AddressLine2;
            City = client.City;
            StateProvince = client.StateProvince;
            PostalCode = client.PostalCode;
            Country = client.Country;
            TaxId = client.TaxId;
            LocationLine = BuildLocationLine(client);

            await LoadContactsAsync();
            await LoadSalesAsync();
        });
    }

    private async Task LoadContactsAsync()
    {
        Contacts.Clear();
        var contacts = await _database.GetContactsForClientAsync(ClientId);
        foreach (var contact in contacts)
        {
            Contacts.Add(contact);
        }
    }

    private async Task LoadSalesAsync()
    {
        Sales.Clear();
        var sales = await _database.GetSalesForClientAsync(ClientId);
        var saleIds = sales.Select(s => s.Id).ToList();
        var payments = await _database.GetPaymentsForSaleIdsAsync(saleIds);
        foreach (var sale in sales)
        {
            var contactName = Contacts.FirstOrDefault(c => c.Id == sale.ContactId)?.Name ?? string.Empty;
            var salePayments = payments.Where(p => p.SaleId == sale.Id).ToList();
            var paidCount = salePayments.Count(p => p.IsPaid);
            var totalCount = salePayments.Count;
            var progress = totalCount == 0 ? 0 : (double)paidCount / totalCount;

            Sales.Add(new SaleEntry
            {
                Id = sale.Id,
                SaleDate = sale.SaleDate,
                Amount = sale.Amount,
                CommissionPercent = sale.CommissionPercent,
                ContactId = sale.ContactId,
                ContactName = contactName,
                InvoiceNumber = sale.InvoiceNumber,
                PaidPaymentCount = paidCount,
                TotalPaymentCount = totalCount,
                PaymentProgress = progress
            });
        }

        SaleCount = Sales.Count;
        TotalSaleAmount = Sales.Sum(s => s.Amount);
        TotalCommissionAmount = Sales.Sum(s => s.Amount * (s.CommissionPercent / 100m));
        OutstandingCommissionAmount = payments.Where(p => !p.IsPaid).Sum(p => p.Commission);
    }

    private async Task OpenEditAsync()
    {
        if (ClientId <= 0)
        {
            return;
        }

        await Shell.Current.GoToAsync("client-edit", new Dictionary<string, object>
        {
            ["clientId"] = ClientId
        });
    }

    private async Task OpenAddSaleAsync()
    {
        if (ClientId <= 0)
        {
            return;
        }

        await Shell.Current.GoToAsync("sale-add", new Dictionary<string, object>
        {
            ["clientId"] = ClientId
        });
    }

    private async Task OpenAddContactAsync()
    {
        if (ClientId <= 0)
        {
            return;
        }

        await Shell.Current.GoToAsync("contact-edit", new Dictionary<string, object>
        {
            ["clientId"] = ClientId
        });
    }

    private static Task OpenContactAsync(ContactModel? contact)
    {
        if (contact is null)
        {
            return Task.CompletedTask;
        }

        return Shell.Current.GoToAsync("contact-details", new Dictionary<string, object>
        {
            ["contactId"] = contact.Id
        });
    }

    private static Task OpenEditContactAsync(ContactModel? contact)
    {
        if (contact is null)
        {
            return Task.CompletedTask;
        }

        return Shell.Current.GoToAsync("contact-edit", new Dictionary<string, object>
        {
            ["contactId"] = contact.Id
        });
    }

    private async Task OpenSaleAsync(SaleEntry? sale)
    {
        if (sale is null)
        {
            return;
        }

        await Shell.Current.GoToAsync("sale-details", new Dictionary<string, object>
        {
            ["saleId"] = sale.Id
        });
    }

    private async Task DeleteClientAsync()
    {
        if (ClientId <= 0)
        {
            return;
        }

        var confirm = await Shell.Current.DisplayAlertAsync("Confirm Delete", "Delete this client and all related sales?", "Delete", "Cancel");
        if (!confirm)
        {
            return;
        }

        await _database.DeleteClientAsync(new Client { Id = ClientId });
        await Shell.Current.GoToAsync("..");
    }

    private static string BuildLocationLine(Client client)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(client.City))
        {
            parts.Add(client.City.Trim());
        }

        if (!string.IsNullOrWhiteSpace(client.StateProvince))
        {
            parts.Add(client.StateProvince.Trim());
        }

        if (!string.IsNullOrWhiteSpace(client.PostalCode))
        {
            parts.Add(client.PostalCode.Trim());
        }

        var location = string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        if (string.IsNullOrWhiteSpace(location))
        {
            return client.Country?.Trim() ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(client.Country))
        {
            location = $"{location} · {client.Country.Trim()}";
        }

        return location;
    }
}
