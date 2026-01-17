using ClientTracker.ViewModels;

namespace ClientTracker.Pages;

public partial class SalesPage : ContentPage
{
    private readonly SalesViewModel _viewModel;
    private bool _isAnimatingSheet;
    private bool _isSubscribed;

    public SalesPage(SalesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_isSubscribed)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _isSubscribed = true;
        }
        if (_viewModel.Sales.Count == 0)
        {
            _ = _viewModel.LoadAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_isSubscribed)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _isSubscribed = false;
        }
    }

    private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SalesViewModel.IsFilterSheetOpen))
        {
            if (_viewModel.IsFilterSheetOpen)
            {
                await ShowFilterSheetAsync();
            }
            else
            {
                await HideFilterSheetAsync();
            }
        }
    }

    private async Task ShowFilterSheetAsync()
    {
        if (_isAnimatingSheet)
        {
            return;
        }

        _isAnimatingSheet = true;
        FilterSheetOverlay.IsVisible = true;
        FilterSheetPanel.TranslationX = FilterSheetPanel.Width > 0 ? FilterSheetPanel.Width : 360;
        FilterSheetScrim.Opacity = 0;

        await Task.WhenAll(
            FilterSheetPanel.TranslateToAsync(0, 0, 260, Easing.CubicOut),
            FilterSheetScrim.FadeToAsync(1, 200, Easing.CubicOut));

        _isAnimatingSheet = false;
    }

    private async Task HideFilterSheetAsync()
    {
        if (_isAnimatingSheet)
        {
            return;
        }

        _isAnimatingSheet = true;
        var targetX = FilterSheetPanel.Width > 0 ? FilterSheetPanel.Width : 360;
        await Task.WhenAll(
            FilterSheetPanel.TranslateToAsync(targetX, 0, 220, Easing.CubicIn),
            FilterSheetScrim.FadeToAsync(0, 180, Easing.CubicIn));

        FilterSheetOverlay.IsVisible = false;
        _isAnimatingSheet = false;
    }
}
