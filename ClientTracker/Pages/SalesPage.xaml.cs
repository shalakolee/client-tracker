using ClientTracker.ViewModels;

namespace ClientTracker.Pages;

public partial class SalesPage : ContentPage
{
    private readonly SalesViewModel _viewModel;

    public SalesPage(SalesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.Sales.Count == 0)
        {
            _ = _viewModel.LoadAsync();
        }
    }
}
