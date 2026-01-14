using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using ClientTracker.Models;
using ContactModel = ClientTracker.Models.Contact;
using ClientTracker.Services;

namespace ClientTracker.ViewModels;

public class ClientEditViewModel : ViewModelBase
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
    private string _pageTitle = LocalizationResourceManager.Instance["Title_ClientEdit"];

    private ContactModel? _selectedContact;
    private string _contactName = string.Empty;
    private string _contactEmail = string.Empty;
    private string _contactPhone = string.Empty;

    private SaleEntry? _selectedSale;
    private DateTime _saleDate = DateTime.Today;
    private string _saleAmount = string.Empty;
    private string _saleCommissionPercent = string.Empty;
    private string _saleInvoiceNumber = string.Empty;
    private ContactModel? _selectedSaleContact;

    public ClientEditViewModel(DatabaseService database)
    {
        _database = database;
        Contacts = new ObservableCollection<ContactModel>();
        Sales = new ObservableCollection<SaleEntry>();

        SaveClientCommand = new Command(async () => await SaveClientAsync());
        AddContactCommand = new Command(async () => await AddContactAsync(), () => ClientId > 0);
        SaveContactCommand = new Command(async () => await SaveContactAsync(), () => SelectedContact is not null);
        DeleteContactCommand = new Command(async () => await DeleteContactAsync(), () => SelectedContact is not null);

        AddSaleCommand = new Command(async () => await AddSaleAsync(), () => ClientId > 0);
        SaveSaleCommand = new Command(async () => await SaveSaleAsync(), () => SelectedSale is not null);
        DeleteSaleCommand = new Command(async () => await DeleteSaleAsync(), () => SelectedSale is not null);
    }

    public int ClientId
    {
        get => _clientId;
        set
        {
            if (SetProperty(ref _clientId, value))
            {
                SaveClientCommand.ChangeCanExecute();
                AddContactCommand.ChangeCanExecute();
                AddSaleCommand.ChangeCanExecute();
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

    public ObservableCollection<ContactModel> Contacts { get; }
    public ObservableCollection<SaleEntry> Sales { get; }

    public ContactModel? SelectedContact
    {
        get => _selectedContact;
        set
        {
            if (SetProperty(ref _selectedContact, value))
            {
                if (value is null)
                {
                    ClearContactInputs();
                }
                else
                {
                    ContactName = value.Name;
                    ContactEmail = value.Email;
                    ContactPhone = value.Phone;
                }

                SaveContactCommand.ChangeCanExecute();
                DeleteContactCommand.ChangeCanExecute();
            }
        }
    }

    public string ContactName
    {
        get => _contactName;
        set => SetProperty(ref _contactName, value);
    }

    public string ContactEmail
    {
        get => _contactEmail;
        set => SetProperty(ref _contactEmail, value);
    }

    public string ContactPhone
    {
        get => _contactPhone;
        set => SetProperty(ref _contactPhone, value);
    }

    public SaleEntry? SelectedSale
    {
        get => _selectedSale;
        set
        {
            if (SetProperty(ref _selectedSale, value))
            {
                if (value is null)
                {
                    ClearSaleInputs();
                }
                else
                {
                    SaleDate = value.SaleDate;
                    SaleAmount = value.Amount.ToString("0.00", CultureInfo.CurrentCulture);
                    SaleCommissionPercent = value.CommissionPercent.ToString("0.##", CultureInfo.CurrentCulture);
                    SaleInvoiceNumber = value.InvoiceNumber;
                    SelectedSaleContact = Contacts.FirstOrDefault(c => c.Id == value.ContactId);
                }

                SaveSaleCommand.ChangeCanExecute();
                DeleteSaleCommand.ChangeCanExecute();
            }
        }
    }

    public DateTime SaleDate
    {
        get => _saleDate;
        set => SetProperty(ref _saleDate, value);
    }

    public string SaleAmount
    {
        get => _saleAmount;
        set => SetProperty(ref _saleAmount, value);
    }

    public string SaleCommissionPercent
    {
        get => _saleCommissionPercent;
        set => SetProperty(ref _saleCommissionPercent, value);
    }

    public string SaleInvoiceNumber
    {
        get => _saleInvoiceNumber;
        set => SetProperty(ref _saleInvoiceNumber, value);
    }

    public ContactModel? SelectedSaleContact
    {
        get => _selectedSaleContact;
        set => SetProperty(ref _selectedSaleContact, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string PageTitle
    {
        get => _pageTitle;
        set => SetProperty(ref _pageTitle, value);
    }

    public bool IsExistingClient => ClientId > 0;

    public Command SaveClientCommand { get; }
    public Command AddContactCommand { get; }
    public Command SaveContactCommand { get; }
    public Command DeleteContactCommand { get; }
    public Command AddSaleCommand { get; }
    public Command SaveSaleCommand { get; }
    public Command DeleteSaleCommand { get; }

    public async Task LoadAsync(int clientId)
    {
        StatusMessage = string.Empty;
        if (clientId <= 0)
        {
            ClientId = 0;
            ClientName = string.Empty;
            AddressLine1 = string.Empty;
            AddressLine2 = string.Empty;
            City = string.Empty;
            StateProvince = string.Empty;
            PostalCode = string.Empty;
            Country = string.Empty;
            TaxId = string.Empty;
            Contacts.Clear();
            Sales.Clear();
            PageTitle = LocalizationResourceManager.Instance["Title_ClientNew"];
            OnPropertyChanged(nameof(IsExistingClient));
            return;
        }

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
        PageTitle = LocalizationResourceManager.Instance["Title_ClientEdit"];
        OnPropertyChanged(nameof(IsExistingClient));

        await LoadContactsAsync();
        await LoadSalesAsync();
    }

    private async Task SaveClientAsync()
    {
        StatusMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(ClientName))
        {
            StatusMessage = "Client name cannot be empty.";
            return;
        }

        if (ClientId == 0)
        {
            var newClient = new Client
            {
                Name = ClientName.Trim(),
                AddressLine1 = AddressLine1.Trim(),
                AddressLine2 = AddressLine2.Trim(),
                City = City.Trim(),
                StateProvince = StateProvince.Trim(),
                PostalCode = PostalCode.Trim(),
                Country = Country.Trim(),
                TaxId = TaxId.Trim()
            };

            await _database.AddClientAsync(newClient);
            ClientId = newClient.Id;
            PageTitle = LocalizationResourceManager.Instance["Title_ClientEdit"];
            StatusMessage = "Client created.";
            SaveClientCommand.ChangeCanExecute();
            AddContactCommand.ChangeCanExecute();
            AddSaleCommand.ChangeCanExecute();
            OnPropertyChanged(nameof(IsExistingClient));
        }
        else
        {
            var client = await _database.GetClientByIdAsync(ClientId);
            if (client is null)
            {
                StatusMessage = "Client not found.";
                return;
            }

            client.Name = ClientName.Trim();
            client.AddressLine1 = AddressLine1.Trim();
            client.AddressLine2 = AddressLine2.Trim();
            client.City = City.Trim();
            client.StateProvince = StateProvince.Trim();
            client.PostalCode = PostalCode.Trim();
            client.Country = Country.Trim();
            client.TaxId = TaxId.Trim();

            await _database.UpdateClientAsync(client);
            StatusMessage = "Client details saved.";
        }
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

    private async Task AddContactAsync()
    {
        StatusMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(ContactName))
        {
            StatusMessage = "Enter a contact name.";
            return;
        }

        var contact = new ContactModel
        {
            ClientId = ClientId,
            Name = ContactName.Trim(),
            Email = ContactEmail.Trim(),
            Phone = ContactPhone.Trim()
        };

        await _database.AddContactAsync(contact);
        await LoadContactsAsync();
        SelectedContact = Contacts.FirstOrDefault(c => c.Id == contact.Id);
    }

    private async Task SaveContactAsync()
    {
        StatusMessage = string.Empty;
        if (SelectedContact is null)
        {
            return;
        }

        SelectedContact.Name = ContactName.Trim();
        SelectedContact.Email = ContactEmail.Trim();
        SelectedContact.Phone = ContactPhone.Trim();

        await _database.UpdateContactAsync(SelectedContact);
        await LoadContactsAsync();
    }

    private async Task DeleteContactAsync()
    {
        StatusMessage = string.Empty;
        if (SelectedContact is null)
        {
            return;
        }

        var hasSales = await _database.ContactHasSalesAsync(SelectedContact.Id);
        if (hasSales)
        {
            StatusMessage = "Cannot delete a contact linked to sales.";
            return;
        }

        var confirm = await Shell.Current.DisplayAlertAsync("Confirm Delete", "Delete this contact?", "Delete", "Cancel");
        if (!confirm)
        {
            return;
        }

        await _database.DeleteContactAsync(SelectedContact);
        SelectedContact = null;
        await LoadContactsAsync();
    }

    private void ClearContactInputs()
    {
        ContactName = string.Empty;
        ContactEmail = string.Empty;
        ContactPhone = string.Empty;
        SelectedContact = null;
    }

    private async Task LoadSalesAsync()
    {
        Sales.Clear();
        var sales = await _database.GetSalesForClientAsync(ClientId);
        foreach (var sale in sales)
        {
            var contactName = Contacts.FirstOrDefault(c => c.Id == sale.ContactId)?.Name ?? string.Empty;
            Sales.Add(new SaleEntry
            {
                Id = sale.Id,
                SaleDate = sale.SaleDate,
                Amount = sale.Amount,
                CommissionPercent = sale.CommissionPercent,
                ContactId = sale.ContactId,
                ContactName = contactName,
                InvoiceNumber = sale.InvoiceNumber
            });
        }
    }

    private async Task AddSaleAsync()
    {
        StatusMessage = string.Empty;
        if (!ValidateSaleInputs(out var amount, out var commissionPercent, out var contactId))
        {
            return;
        }

        var sale = new Sale
        {
            ClientId = ClientId,
            ContactId = contactId,
            InvoiceNumber = SaleInvoiceNumber.Trim(),
            SaleDate = SaleDate.Date,
            Amount = amount,
            CommissionPercent = commissionPercent
        };

        await _database.AddSaleAsync(sale);
        await LoadSalesAsync();
        ClearSaleInputs();
    }

    private async Task SaveSaleAsync()
    {
        StatusMessage = string.Empty;
        if (SelectedSale is null)
        {
            return;
        }

        if (!ValidateSaleInputs(out var amount, out var commissionPercent, out var contactId))
        {
            return;
        }

        var sale = new Sale
        {
            Id = SelectedSale.Id,
            ClientId = ClientId,
            ContactId = contactId,
            InvoiceNumber = SaleInvoiceNumber.Trim(),
            SaleDate = SaleDate.Date,
            Amount = amount,
            CommissionPercent = commissionPercent
        };

        await _database.UpdateSaleAsync(sale, true);
        await LoadSalesAsync();
    }

    private async Task DeleteSaleAsync()
    {
        StatusMessage = string.Empty;
        if (SelectedSale is null)
        {
            return;
        }

        var confirm = await Shell.Current.DisplayAlertAsync("Confirm Delete", "Delete this sale and its payments?", "Delete", "Cancel");
        if (!confirm)
        {
            return;
        }

        await _database.DeleteSaleAsync(new Sale { Id = SelectedSale.Id });
        SelectedSale = null;
        await LoadSalesAsync();
    }

    private void ClearSaleInputs()
    {
        SaleDate = DateTime.Today;
        SaleAmount = string.Empty;
        SaleCommissionPercent = string.Empty;
        SaleInvoiceNumber = string.Empty;
        SelectedSaleContact = null;
        SelectedSale = null;
    }

    private bool ValidateSaleInputs(out decimal amount, out decimal commissionPercent, out int contactId)
    {
        amount = 0m;
        commissionPercent = 0m;
        contactId = 0;

        if (SelectedSaleContact is null)
        {
            StatusMessage = "Select a contact for the sale.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SaleInvoiceNumber))
        {
            StatusMessage = "Enter an invoice number.";
            return false;
        }

        if (!decimal.TryParse(SaleAmount, NumberStyles.Number, CultureInfo.CurrentCulture, out amount) || amount <= 0)
        {
            StatusMessage = "Enter a valid sale amount.";
            return false;
        }

        if (!decimal.TryParse(SaleCommissionPercent, NumberStyles.Number, CultureInfo.CurrentCulture, out commissionPercent) || commissionPercent < 0)
        {
            StatusMessage = "Enter a valid commission percent.";
            return false;
        }

        contactId = SelectedSaleContact.Id;
        return true;
    }
}
