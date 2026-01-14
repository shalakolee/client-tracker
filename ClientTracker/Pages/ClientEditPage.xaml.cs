using ClientTracker.ViewModels;

namespace ClientTracker.Pages;

public partial class ClientEditPage : ContentPage, IQueryAttributable
{
    private readonly ClientEditViewModel _viewModel;
    private int? _requestedClientId;

    public ClientEditPage(ClientEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("clientId", out var value) && int.TryParse(value?.ToString(), out var clientId))
        {
            _requestedClientId = clientId;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync(_requestedClientId ?? 0);
        _requestedClientId = null;
    }
}
