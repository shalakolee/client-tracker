using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using ClientTracker.Models;
using ContactModel = ClientTracker.Models.Contact;
using ClientTracker.Services;

namespace ClientTracker.ViewModels;

public class AddSaleViewModel : ViewModelBase
{
    private readonly DatabaseService _database;
    private Client? _selectedClient;
    private ContactModel? _selectedContact;
    private DateTime _saleDate = DateTime.Today;
    private string _amount = string.Empty;
    private string _commissionPercent = string.Empty;
    private string _invoiceNumber = string.Empty;
    private string _statusMessage = string.Empty;

    public AddSaleViewModel(DatabaseService database)
    {
        _database = database;
        Clients = new ObservableCollection<Client>();
        Contacts = new ObservableCollection<ContactModel>();
        SaveCommand = new Command(async () => await SaveAsync());
    }

    public ObservableCollection<Client> Clients { get; }
    public ObservableCollection<ContactModel> Contacts { get; }

    public Client? SelectedClient
    {
        get => _selectedClient;
        set
        {
            if (SetProperty(ref _selectedClient, value))
            {
                _ = LoadContactsAsync();
            }
        }
    }

    public ContactModel? SelectedContact
    {
        get => _selectedContact;
        set => SetProperty(ref _selectedContact, value);
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

    public string InvoiceNumber
    {
        get => _invoiceNumber;
        set => SetProperty(ref _invoiceNumber, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public Command SaveCommand { get; }

    public async Task LoadAsync(int? clientId = null)
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = string.Empty;
            var selectedId = clientId ?? SelectedClient?.Id;
            var clients = await _database.GetClientsAsync();
            Clients.Clear();
            foreach (var client in clients)
            {
                Clients.Add(client);
            }

            if (selectedId.HasValue)
            {
                SelectedClient = Clients.FirstOrDefault(c => c.Id == selectedId.Value);
            }
        });
    }

    private async Task LoadContactsAsync()
    {
        Contacts.Clear();
        if (SelectedClient is null)
        {
            return;
        }

        var contacts = await _database.GetContactsForClientAsync(SelectedClient.Id);
        foreach (var contact in contacts)
        {
            Contacts.Add(contact);
        }
    }

    private async Task SaveAsync()
    {
        StatusMessage = string.Empty;
        if (SelectedClient is null)
        {
            StatusMessage = "Select a client.";
            return;
        }

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

        if (!decimal.TryParse(Amount, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount <= 0)
        {
            StatusMessage = "Enter a valid sale amount.";
            return;
        }

        if (!decimal.TryParse(CommissionPercent, NumberStyles.Number, CultureInfo.CurrentCulture, out var commissionPercent) || commissionPercent < 0)
        {
            StatusMessage = "Enter a valid commission percent.";
            return;
        }

        var sale = new Sale
        {
            ClientId = SelectedClient.Id,
            ContactId = SelectedContact.Id,
            InvoiceNumber = InvoiceNumber.Trim(),
            SaleDate = SaleDate.Date,
            Amount = amount,
            CommissionPercent = commissionPercent
        };

        await _database.AddSaleAsync(sale);
        await Shell.Current.GoToAsync("sale-details", new Dictionary<string, object>
        {
            ["saleId"] = sale.Id
        });
    }
}
