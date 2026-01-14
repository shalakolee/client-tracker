using ClientTracker.Models;
using ClientTracker.ViewModels;

namespace ClientTracker.Pages;

public partial class PayCalendarPage : ContentPage
{
    private readonly CalendarViewModel _viewModel;

    public PayCalendarPage(CalendarViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.PaySummaries.Count == 0)
        {
            _ = _viewModel.LoadAsync();
        }
    }

    private async void OnPaymentCheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is not CheckBox checkbox)
        {
            return;
        }

        if (checkbox.BindingContext is Models.PaymentScheduleItem item)
        {
            item.IsPaid = e.Value;
            await _viewModel.UpdatePaymentStatusAsync(item);
        }
    }
}
