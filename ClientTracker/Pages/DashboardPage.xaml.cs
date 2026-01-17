using System.Collections.Specialized;
using System.Linq;
using ClientTracker.ViewModels;

namespace ClientTracker.Pages;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;
    private bool _commissionPositioned;

    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _viewModel.CommissionSummaries.CollectionChanged += OnCommissionSummariesChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
        ApplyCommissionPosition();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _commissionPositioned = false;
    }

    private void OnCommissionSummariesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ApplyCommissionPosition();
    }

    private async void ApplyCommissionPosition()
    {
        if (_commissionPositioned || CommissionCarousel is null || _viewModel.CommissionSummaries.Count == 0)
        {
            return;
        }

        _commissionPositioned = true;
        await Task.Delay(50);
        Dispatcher.Dispatch(() =>
        {
            CommissionCarousel.ScrollTo(_viewModel.SelectedCommissionIndex, position: ScrollToPosition.Center, animate: false);
        });
    }
}
