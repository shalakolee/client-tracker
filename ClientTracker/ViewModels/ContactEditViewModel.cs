using System.Collections.ObjectModel;
using System.Linq;
using ClientTracker.Models;
using ContactModel = ClientTracker.Models.Contact;
using ClientTracker.Services;

namespace ClientTracker.ViewModels;

public class ContactEditViewModel : ViewModelBase
{
    private readonly DatabaseService _database;
    private int _contactId;
    private int _lockedClientId;
    private string _name = string.Empty;
    private string _email = string.Empty;
    private string _phone = string.Empty;
    private string _notes = string.Empty;
    private Client? _selectedClient;
    private string _statusMessage = string.Empty;

    public ContactEditViewModel(DatabaseService database)
    {
        _database = database;
        Clients = new ObservableCollection<Client>();
        SaveCommand = new Command(async () => await SaveAsync());
        DeleteCommand = new Command(async () => await DeleteAsync(), () => ContactId > 0);
    }

    public ObservableCollection<Client> Clients { get; }

    public int ContactId
    {
        get => _contactId;
        set
        {
            if (SetProperty(ref _contactId, value))
            {
                DeleteCommand.ChangeCanExecute();
                OnPropertyChanged(nameof(CanSelectClient));
            }
        }
    }

    public Client? SelectedClient
    {
        get => _selectedClient;
        set => SetProperty(ref _selectedClient, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public Command SaveCommand { get; }
    public Command DeleteCommand { get; }
    public bool CanSelectClient => ContactId == 0;

    public async Task LoadAsync(int? contactId = null, int? clientId = null)
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = string.Empty;
            _lockedClientId = 0;
            var clients = await _database.GetClientsAsync();
            Clients.Clear();
            foreach (var client in clients)
            {
                Clients.Add(client);
            }

            if (contactId.HasValue && contactId.Value > 0)
            {
                var contact = await _database.GetContactByIdAsync(contactId.Value);
                if (contact is null)
                {
                    StatusMessage = "Contact not found.";
                    return;
                }

                ContactId = contact.Id;
                _lockedClientId = contact.ClientId;
                Name = contact.Name;
                Email = contact.Email;
                Phone = contact.Phone;
                Notes = contact.Notes;
                SelectedClient = Clients.FirstOrDefault(c => c.Id == contact.ClientId);
            }
            else
            {
                ContactId = 0;
                _lockedClientId = 0;
                Name = string.Empty;
                Email = string.Empty;
                Phone = string.Empty;
                Notes = string.Empty;
                if (clientId.HasValue && clientId.Value > 0)
                {
                    SelectedClient = Clients.FirstOrDefault(c => c.Id == clientId.Value) ?? Clients.FirstOrDefault();
                }
                else
                {
                    SelectedClient = Clients.FirstOrDefault();
                }
            }
        });
    }

    private async Task SaveAsync()
    {
        StatusMessage = string.Empty;
        if (SelectedClient is null)
        {
            StatusMessage = "Select a client.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = "Enter a contact name.";
            return;
        }

        if (ContactId == 0)
        {
            var contact = new ContactModel
            {
                ClientId = SelectedClient.Id,
                Name = Name.Trim(),
                Email = Email.Trim(),
                Phone = Phone.Trim(),
                Notes = Notes.Trim()
            };

            await _database.AddContactAsync(contact);
            ContactId = contact.Id;
        }
        else
        {
            var contact = await _database.GetContactByIdAsync(ContactId);
            if (contact is null)
            {
                StatusMessage = "Contact not found.";
                return;
            }

            if (_lockedClientId > 0)
            {
                contact.ClientId = _lockedClientId;
            }
            contact.Name = Name.Trim();
            contact.Email = Email.Trim();
            contact.Phone = Phone.Trim();
            contact.Notes = Notes.Trim();

            await _database.UpdateContactAsync(contact);
        }

        await Shell.Current.GoToAsync("..");
    }

    private async Task DeleteAsync()
    {
        StatusMessage = string.Empty;
        if (ContactId == 0)
        {
            return;
        }

        var hasSales = await _database.ContactHasSalesAsync(ContactId);
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

        await _database.DeleteContactAsync(new ContactModel { Id = ContactId });
        await Shell.Current.GoToAsync("..");
    }
}
