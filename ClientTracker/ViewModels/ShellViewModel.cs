namespace ClientTracker.ViewModels;

public class ShellViewModel : ViewModelBase
{
    public Command OpenMenuCommand { get; }

    public ShellViewModel()
    {
        OpenMenuCommand = new Command(() =>
        {
            if (Shell.Current is null)
            {
                return;
            }

            Shell.Current.FlyoutIsPresented = true;
        });
    }
}

