using ClientTracker.Services;

namespace ClientTracker;

public partial class App : Application
{
	private readonly UpdateService _updateService;

	public App(UpdateService updateService)
	{
		InitializeComponent();
		_updateService = updateService;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());
		_ = _updateService.CheckForUpdatesAsync(false);
		return window;
	}
}

