using System.Linq;

namespace ClientTracker;

public partial class MobileShell : Shell
{
    private readonly Command _swipeBackCommand;

    public MobileShell()
    {
        InitializeComponent();
        BindingContext = new ViewModels.ShellViewModel();
        _swipeBackCommand = new Command(HandleSwipeBack);
        Navigated += OnNavigated;
    }

    private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        EnableSwipeBackOnCurrentPage();
    }

    private void EnableSwipeBackOnCurrentPage()
    {
        if (CurrentPage is not ContentPage page || page.Content is null)
        {
            return;
        }

        if (page.Content.GestureRecognizers.OfType<SwipeGestureRecognizer>()
            .Any(recognizer => recognizer.Direction == SwipeDirection.Right && recognizer.Command == _swipeBackCommand))
        {
            return;
        }

        page.Content.GestureRecognizers.Add(new SwipeGestureRecognizer
        {
            Direction = SwipeDirection.Right,
            Command = _swipeBackCommand
        });
    }

    private void HandleSwipeBack()
    {
        if (Shell.Current is null)
        {
            return;
        }

        if (Shell.Current.Navigation.ModalStack.Count > 0)
        {
            _ = Shell.Current.Navigation.PopModalAsync();
            return;
        }

        if (Shell.Current.Navigation.NavigationStack.Count > 1)
        {
            _ = Shell.Current.GoToAsync("..");
        }
    }
}
