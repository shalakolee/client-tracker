using ClientTracker.Services;

namespace ClientTracker.ViewModels;

public class BusinessDetailsViewModel : ViewModelBase
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

    public BusinessDetailsViewModel(DatabaseService database)
    {
        _database = database;
        SaveCommand = new Command(async () => await SaveAsync(), () => ClientId > 0);
    }

    public int ClientId
    {
        get => _clientId;
        set
        {
            if (SetProperty(ref _clientId, value))
            {
                SaveCommand.ChangeCanExecute();
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

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public Command SaveCommand { get; }

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
        });
    }

    private async Task SaveAsync()
    {
        StatusMessage = string.Empty;
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
        StatusMessage = "Business details saved.";
    }
}
