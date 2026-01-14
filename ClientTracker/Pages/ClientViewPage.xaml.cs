using ClientTracker.ViewModels;

namespace ClientTracker.Pages;

public partial class ClientViewPage : ContentPage, IQueryAttributable
{
    private readonly ClientViewViewModel _viewModel;

    public ClientViewPage(ClientViewViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("clientId", out var value) && int.TryParse(value?.ToString(), out var clientId))
        {
            _ = _viewModel.LoadAsync(clientId);
        }
    }
}
