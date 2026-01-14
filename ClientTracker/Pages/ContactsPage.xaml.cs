using ClientTracker.ViewModels;

namespace ClientTracker.Pages;

public partial class ContactsPage : ContentPage
{
    private readonly ContactsViewModel _viewModel;

    public ContactsPage(ContactsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.LoadAsync();
    }
}
