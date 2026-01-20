using ClientTracker.ViewModels;

namespace ClientTracker.Pages;

public partial class CommissionPlanEditPage : ContentPage
{
    public CommissionPlanEditPage(CommissionPlanEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
