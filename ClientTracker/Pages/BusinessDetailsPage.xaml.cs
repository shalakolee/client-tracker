using ClientTracker.ViewModels;

namespace ClientTracker.Pages;

public partial class BusinessDetailsPage : ContentPage, IQueryAttributable
{
    private readonly BusinessDetailsViewModel _viewModel;

    public BusinessDetailsPage(BusinessDetailsViewModel viewModel)
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
