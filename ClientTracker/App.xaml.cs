using ClientTracker.Services;

namespace ClientTracker;

public partial class App : Application
{
	private readonly UpdateService _updateService;

	public App(UpdateService updateService)
	{
		InitializeComponent();
		_updateService = updateService;
        StartupLog.Write("App starting");

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                StartupLog.Write(ex, "AppDomain");
            }
            else
            {
                StartupLog.Write($"AppDomain unhandled: {args.ExceptionObject}");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            StartupLog.Write(args.Exception, "TaskScheduler");
            args.SetObserved();
        };
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
        StartupLog.Write("CreateWindow start");
        AppShell shell;
        try
        {
            StartupLog.Write("CreateWindow: constructing AppShell");
            shell = new AppShell();
            StartupLog.Write("CreateWindow: AppShell constructed");
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex, "CreateWindow: AppShell construction");
            throw;
        }

        Window window;
        try
        {
            window = new Window(shell);
            StartupLog.Write("CreateWindow: Window constructed");
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex, "CreateWindow: Window construction");
            throw;
        }

        window.Dispatcher.Dispatch(() => StartupLog.Write("Window dispatcher alive"));
        window.Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(1500);
            try
            {
                _updateService.StartBackgroundChecks();
                await _updateService.CheckForUpdatesInBackgroundAsync();
            }
            catch (Exception ex)
            {
                StartupLog.Write(ex, "UpdateService");
            }
        });
		return window;
	}
}

