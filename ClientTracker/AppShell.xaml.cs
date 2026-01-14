namespace ClientTracker;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		BindingContext = new ViewModels.ShellViewModel();
		Routing.RegisterRoute("client-view", typeof(Pages.ClientViewPage));
		Routing.RegisterRoute("client-edit", typeof(Pages.ClientEditPage));
		Routing.RegisterRoute("sale-details", typeof(Pages.SaleDetailsPage));
		Routing.RegisterRoute("sale-add", typeof(Pages.AddSalePage));
		Routing.RegisterRoute("contact-details", typeof(Pages.ContactDetailsPage));
		Routing.RegisterRoute("contact-edit", typeof(Pages.ContactEditPage));
	}
}
