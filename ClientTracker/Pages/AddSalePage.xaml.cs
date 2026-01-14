using ClientTracker.ViewModels;

namespace ClientTracker.Pages;

public partial class AddSalePage : ContentPage, IQueryAttributable
{
    private readonly AddSaleViewModel _viewModel;
    private int? _requestedClientId;

    public AddSalePage(AddSaleViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("clientId", out var value))
        {
            if (value is int clientId)
            {
                _requestedClientId = clientId;
            }
            else if (value is string text && int.TryParse(text, out var parsed))
            {
                _requestedClientId = parsed;
            }
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync(_requestedClientId);
        _requestedClientId = null;
    }
}
