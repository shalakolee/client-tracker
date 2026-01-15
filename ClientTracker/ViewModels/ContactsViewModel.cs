using System.Collections.ObjectModel;
using ClientTracker.Models;
using ContactModel = ClientTracker.Models.Contact;
using ClientTracker.Services;

namespace ClientTracker.ViewModels;

public class ContactsViewModel : ViewModelBase
{
    private readonly DatabaseService _database;
    private readonly LocalizationResourceManager _localization;
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;
    private ContactFilterOption? _selectedClientFilter;
    private bool _notesOnly;

    public ContactsViewModel(DatabaseService database, LocalizationResourceManager localization)
    {
        _database = database;
        _localization = localization;
        Contacts = new ObservableCollection<ContactOverview>();
        ClientFilters = new ObservableCollection<ContactFilterOption>();
        LoadCommand = new Command(async () => await LoadAsync());
        AddContactCommand = new Command(async () => await OpenAddAsync());
        ViewContactCommand = new Command<ContactOverview>(async contact => await OpenViewAsync(contact));
        EditContactCommand = new Command<ContactOverview>(async contact => await OpenEditAsync(contact));
        DeleteContactCommand = new Command<ContactOverview>(async contact => await DeleteAsync(contact));
    }

    public ObservableCollection<ContactOverview> Contacts { get; }
    public ObservableCollection<ContactFilterOption> ClientFilters { get; }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ContactFilterOption? SelectedClientFilter
    {
        get => _selectedClientFilter;
        set => SetProperty(ref _selectedClientFilter, value);
    }

    public bool NotesOnly
    {
        get => _notesOnly;
        set => SetProperty(ref _notesOnly, value);
    }

    public Command LoadCommand { get; }
    public Command AddContactCommand { get; }
    public Command<ContactOverview> ViewContactCommand { get; }
    public Command<ContactOverview> EditContactCommand { get; }
    public Command<ContactOverview> DeleteContactCommand { get; }

    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = string.Empty;
            await EnsureClientFiltersAsync();
            var contacts = await _database.GetContactsOverviewAsync(SearchText);
            var filtered = ApplyFilters(contacts);
            Contacts.Clear();
            foreach (var contact in filtered)
            {
                Contacts.Add(contact);
            }
        });
    }

    private static Task OpenAddAsync()
    {
        return Shell.Current.GoToAsync("contact-edit");
    }

    private static Task OpenViewAsync(ContactOverview? contact)
    {
        if (contact is null)
        {
            return Task.CompletedTask;
        }

        return Shell.Current.GoToAsync("contact-details", new Dictionary<string, object>
        {
            ["contactId"] = contact.ContactId
        });
    }

    private static Task OpenEditAsync(ContactOverview? contact)
    {
        if (contact is null)
        {
            return Task.CompletedTask;
        }

        return Shell.Current.GoToAsync("contact-edit", new Dictionary<string, object>
        {
            ["contactId"] = contact.ContactId
        });
    }

    private async Task DeleteAsync(ContactOverview? contact)
    {
        if (contact is null)
        {
            return;
        }

        var hasSales = await _database.ContactHasSalesAsync(contact.ContactId);
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

        await _database.DeleteContactAsync(new ContactModel { Id = contact.ContactId });
        await LoadAsync();
    }

    private async Task EnsureClientFiltersAsync()
    {
        var selectedId = SelectedClientFilter?.ClientId ?? 0;
        ClientFilters.Clear();
        var label = _localization["Contacts_Filter_AllClients"];
        ClientFilters.Add(new ContactFilterOption(0, label));
        var clients = await _database.GetClientsAsync();
        foreach (var client in clients)
        {
            ClientFilters.Add(new ContactFilterOption(client.Id, client.Name));
        }

        SelectedClientFilter = ClientFilters.FirstOrDefault(c => c.ClientId == selectedId) ?? ClientFilters.FirstOrDefault();
    }

    private IEnumerable<ContactOverview> ApplyFilters(IEnumerable<ContactOverview> contacts)
    {
        var filtered = contacts;
        if (SelectedClientFilter is { ClientId: > 0 } option)
        {
            filtered = filtered.Where(c => c.ClientId == option.ClientId);
        }

        if (NotesOnly)
        {
            filtered = filtered.Where(c => !string.IsNullOrWhiteSpace(c.Notes));
        }

        return filtered;
    }

    public record ContactFilterOption(int ClientId, string DisplayName);
}
