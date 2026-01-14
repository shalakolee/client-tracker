using ClientTracker.Services;
using ClientTracker.Models;
using ClientTracker.ViewModels;

namespace ClientTracker.Pages;

public partial class PayCalendarPage : ContentPage
{
    private readonly CalendarViewModel _viewModel;
    private bool _requestedInitialLoad;

    public PayCalendarPage(CalendarViewModel viewModel)
    {
        _viewModel = viewModel;

        try
        {
            InitializeComponent();
            BindingContext = _viewModel;
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex, "PayCalendarPage.InitializeComponent");
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(20),
                    Spacing = 10,
                    Children =
                    {
                        new Label { Text = "Pay Calendar failed to render.", FontAttributes = FontAttributes.Bold },
                        new Label { Text = ex.Message },
                        new Label { Text = ex.ToString(), FontSize = 12, Opacity = 0.7 }
                    }
                }
            };
            return;
        }

        StartupLog.Write("PayCalendarPage constructed (xaml ok)");
        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(50);
            if (!_viewModel.IsBusy && _viewModel.PaySummaries.Count == 0)
            {
                _requestedInitialLoad = true;
                _viewModel.StatusMessage = "Loading...";
                _ = _viewModel.LoadAsync();
            }
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        StartupLog.Write("PayCalendarPage.OnAppearing");
        if (_requestedInitialLoad || _viewModel.IsBusy)
        {
            return;
        }

        _requestedInitialLoad = true;
        if (_viewModel.PaySummaries.Count == 0)
        {
            _viewModel.StatusMessage = "Loading...";
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
            try
            {
                item.IsPaid = e.Value;
                await _viewModel.UpdatePaymentStatusAsync(item);
            }
            catch (Exception ex)
            {
                StartupLog.Write(ex, "PayCalendarPage.OnPaymentCheckedChanged");
                _viewModel.StatusMessage = "Unable to update payment status.";
            }
        }
    }
}
