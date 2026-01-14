using ClientTracker.Models;
using ContactModel = ClientTracker.Models.Contact;
using ClientTracker.Services;

namespace ClientTracker.ViewModels;

public class ContactDetailsViewModel : ViewModelBase
{
    private readonly DatabaseService _database;
    private string _clientName = string.Empty;
    private string _contactName = string.Empty;
    private string _contactEmail = string.Empty;
    private string _contactPhone = string.Empty;
    private string _notes = string.Empty;
    private string _statusMessage = string.Empty;
    private int _contactId;

    public ContactDetailsViewModel(DatabaseService database)
    {
        _database = database;
        EditCommand = new Command(async () => await EditAsync(), () => ContactId > 0);
        DeleteCommand = new Command(async () => await DeleteAsync(), () => ContactId > 0);
    }

    public int ContactId
    {
        get => _contactId;
        set
        {
            if (SetProperty(ref _contactId, value))
            {
                EditCommand.ChangeCanExecute();
                DeleteCommand.ChangeCanExecute();
            }
        }
    }

    public string ClientName
    {
        get => _clientName;
        set => SetProperty(ref _clientName, value);
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

    public string Notes
    {
        get => _notes;
        set
        {
            if (SetProperty(ref _notes, value))
            {
                OnPropertyChanged(nameof(NotesDisplay));
            }
        }
    }

    public string NotesDisplay => string.IsNullOrWhiteSpace(Notes) ? "No notes added." : Notes;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public Command EditCommand { get; }
    public Command DeleteCommand { get; }

    public async Task LoadAsync(int contactId)
    {
        StatusMessage = string.Empty;
        var contact = await _database.GetContactByIdAsync(contactId);
        if (contact is null)
        {
            StatusMessage = "Contact not found.";
            return;
        }

        ContactId = contact.Id;
        ContactName = contact.Name;
        ContactEmail = contact.Email;
        ContactPhone = contact.Phone;
        Notes = contact.Notes;

        var client = await _database.GetClientByIdAsync(contact.ClientId);
        ClientName = client?.Name ?? string.Empty;
    }

    private Task EditAsync()
    {
        if (ContactId == 0)
        {
            return Task.CompletedTask;
        }

        return Shell.Current.GoToAsync("contact-edit", new Dictionary<string, object>
        {
            ["contactId"] = ContactId
        });
    }

    private async Task DeleteAsync()
    {
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
