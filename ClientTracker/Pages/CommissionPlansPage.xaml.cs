using ClientTracker.ViewModels;

namespace ClientTracker.Pages;

public partial class CommissionPlansPage : ContentPage
{
    private readonly CommissionPlansViewModel _viewModel;

    public CommissionPlansPage(CommissionPlansViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
