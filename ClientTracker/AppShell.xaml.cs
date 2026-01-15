namespace ClientTracker;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		BindingContext = new ViewModels.ShellViewModel();
	}
}
