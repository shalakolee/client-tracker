using System.Linq;

namespace ClientTracker;

public partial class MobileShell : Shell
{
    private readonly Command _swipeBackCommand;
    private readonly List<string> _navigationHistory = new();
    private bool _suppressHistory;

    public MobileShell()
    {
        InitializeComponent();
        BindingContext = new ViewModels.ShellViewModel();
        _swipeBackCommand = new Command(HandleSwipeBack);
        Navigated += OnNavigated;
    }

    private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        UpdateNavigationHistory(e);
        EnableSwipeBackOnCurrentPage();
    }

    private void UpdateNavigationHistory(ShellNavigatedEventArgs e)
    {
        if (_suppressHistory)
        {
            return;
        }

        var location = e.Current.Location.OriginalString;
        if (string.IsNullOrWhiteSpace(location))
        {
            return;
        }

        if (e.Source == ShellNavigationSource.Pop || e.Source == ShellNavigationSource.PopToRoot)
        {
            while (_navigationHistory.Count > 0 && !string.Equals(_navigationHistory[^1], location, StringComparison.Ordinal))
            {
                _navigationHistory.RemoveAt(_navigationHistory.Count - 1);
            }

            if (_navigationHistory.Count == 0 || !string.Equals(_navigationHistory[^1], location, StringComparison.Ordinal))
            {
                _navigationHistory.Add(location);
            }

            return;
        }

        if (_navigationHistory.Count == 0 || !string.Equals(_navigationHistory[^1], location, StringComparison.Ordinal))
        {
            _navigationHistory.Add(location);
            if (_navigationHistory.Count > 50)
            {
                _navigationHistory.RemoveAt(0);
            }
        }
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
        _ = HandleBackAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        if (!CanHandleBack())
        {
            return base.OnBackButtonPressed();
        }

        _ = HandleBackAsync();
        return true;
    }

    private bool CanHandleBack()
    {
        if (Shell.Current is null)
        {
            return false;
        }

        if (Shell.Current.Navigation.ModalStack.Count > 0)
        {
            return true;
        }

        if (Shell.Current.Navigation.NavigationStack.Count > 1)
        {
            return true;
        }

        return _navigationHistory.Count > 1;
    }

    private async Task HandleBackAsync()
    {
        if (Shell.Current is null)
        {
            return;
        }

        if (Shell.Current.Navigation.ModalStack.Count > 0)
        {
            await Shell.Current.Navigation.PopModalAsync();
            return;
        }

        if (Shell.Current.Navigation.NavigationStack.Count > 1)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        if (_navigationHistory.Count > 1)
        {
            var target = _navigationHistory[^2];
            _navigationHistory.RemoveAt(_navigationHistory.Count - 1);
            _suppressHistory = true;
            try
            {
                await Shell.Current.GoToAsync(target);
            }
            finally
            {
                _suppressHistory = false;
            }
        }
    }
}
