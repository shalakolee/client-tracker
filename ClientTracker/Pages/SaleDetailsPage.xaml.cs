using ClientTracker.ViewModels;

namespace ClientTracker.Pages;

public partial class SaleDetailsPage : ContentPage, IQueryAttributable
{
    private readonly SaleDetailsViewModel _viewModel;

    public SaleDetailsPage(SaleDetailsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("saleId", out var value) && int.TryParse(value?.ToString(), out var saleId))
        {
            _ = _viewModel.LoadAsync(saleId);
        }
    }
}
