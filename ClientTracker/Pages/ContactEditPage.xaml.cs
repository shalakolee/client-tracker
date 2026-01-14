using ClientTracker.ViewModels;

namespace ClientTracker.Pages;

public partial class ContactEditPage : ContentPage, IQueryAttributable
{
    private readonly ContactEditViewModel _viewModel;
    private int? _requestedContactId;
    private int? _requestedClientId;

    public ContactEditPage(ContactEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("contactId", out var value))
        {
            if (value is int contactId)
            {
                _requestedContactId = contactId;
            }
            else if (value is string text && int.TryParse(text, out var parsed))
            {
                _requestedContactId = parsed;
            }
        }

        if (query.TryGetValue("clientId", out var clientValue))
        {
            if (clientValue is int clientId)
            {
                _requestedClientId = clientId;
            }
            else if (clientValue is string clientText && int.TryParse(clientText, out var clientParsed))
            {
                _requestedClientId = clientParsed;
            }
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync(_requestedContactId, _requestedClientId);
        _requestedContactId = null;
        _requestedClientId = null;
    }
}
