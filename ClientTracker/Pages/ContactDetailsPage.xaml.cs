using ClientTracker.ViewModels;

namespace ClientTracker.Pages;

public partial class ContactDetailsPage : ContentPage, IQueryAttributable
{
    private readonly ContactDetailsViewModel _viewModel;

    public ContactDetailsPage(ContactDetailsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("contactId", out var value) && int.TryParse(value?.ToString(), out var contactId))
        {
            _ = _viewModel.LoadAsync(contactId);
        }
    }
}
