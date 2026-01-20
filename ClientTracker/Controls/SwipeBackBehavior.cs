using ClientTracker.Services;
using Microsoft.Maui.Controls;

namespace ClientTracker.Controls;

public class SwipeBackBehavior : Behavior<ContentPage>
{
    private SwipeGestureRecognizer? _swipe;
    private View? _targetView;

    protected override void OnAttachedTo(ContentPage bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.PropertyChanged += OnPagePropertyChanged;
        _swipe = new SwipeGestureRecognizer { Direction = SwipeDirection.Right };
        _swipe.Swiped += OnSwiped;
        AttachToContent(bindable.Content);
    }

    protected override void OnDetachingFrom(ContentPage bindable)
    {
        bindable.PropertyChanged -= OnPagePropertyChanged;
        if (_swipe is not null)
        {
            _swipe.Swiped -= OnSwiped;
            DetachFromContent();
            _swipe = null;
        }

        base.OnDetachingFrom(bindable);
    }

    private void OnPagePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ContentPage.Content) && sender is ContentPage page)
        {
            AttachToContent(page.Content);
        }
    }

    private void AttachToContent(View? content)
    {
        if (_swipe is null || content is null)
        {
            return;
        }

        if (_targetView == content)
        {
            return;
        }

        DetachFromContent();
        _targetView = content;
        _targetView.GestureRecognizers.Add(_swipe);
    }

    private void DetachFromContent()
    {
        if (_targetView is null || _swipe is null)
        {
            return;
        }

        _targetView.GestureRecognizers.Remove(_swipe);
        _targetView = null;
    }

    private static async void OnSwiped(object? sender, SwipedEventArgs e)
    {
        if (e.Direction != SwipeDirection.Right)
        {
            return;
        }

        try
        {
            var shell = Shell.Current;
            if (shell?.Navigation is null)
            {
                return;
            }

            if (shell.Navigation.ModalStack.Count > 0)
            {
                await shell.Navigation.PopModalAsync();
                return;
            }

            if (shell.Navigation.NavigationStack.Count > 1)
            {
                await shell.Navigation.PopAsync();
                return;
            }

            await shell.GoToAsync("..");
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex, "SwipeBackBehavior");
        }
    }
}
