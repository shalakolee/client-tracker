using ClientTracker.ViewModels;

namespace ClientTracker.Pages;

public partial class ClientsPage : ContentPage
{
    private readonly ClientsViewModel _viewModel;

    public ClientsPage(ClientsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.Clients.Count == 0)
        {
            _ = _viewModel.LoadAsync();
        }
    }
}
