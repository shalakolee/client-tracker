using System.Collections;
using System.Windows.Input;
using ClientTracker.Services;

namespace ClientTracker.Controls;

public partial class TopBar : ContentView
{
    public TopBar()
    {
        InitializeComponent();
        UpdateSearchVisibility();
        UpdateMenuAvailability();
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        UpdateMenuAvailability();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        UpdateMenuAvailability();
    }

    private async void OnNavigationClicked(object? sender, EventArgs e)
    {
        if (NavigationMode == TopBarNavigationMode.Back)
        {
            if (BackCommand?.CanExecute(null) == true)
            {
                BackCommand.Execute(null);
                return;
            }

            if (Shell.Current is null)
            {
                return;
            }

            await Shell.Current.GoToAsync("..");
            return;
        }

        if (OpenMenuCommand?.CanExecute(null) == true)
        {
            OpenMenuCommand.Execute(null);
            return;
        }

        if (Shell.Current is null)
        {
            return;
        }

        Shell.Current.FlyoutIsPresented = true;
    }

    private async void OnUpdateClicked(object? sender, EventArgs e)
    {
        var service = UpdateService.Instance;
        if (service is null)
        {
            return;
        }

        try
        {
            await service.ShowUpdatePromptIfAvailableAsync();
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex, "UpdateService.Prompt");
        }
    }

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(TopBar), default(string));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(TopBar), default(string), propertyChanged: (_, __, ___) => { });

    public string? Subtitle
    {
        get => (string?)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    public static readonly BindableProperty IsSearchVisibleProperty =
        BindableProperty.Create(nameof(IsSearchVisible), typeof(bool), typeof(TopBar), false, propertyChanged: OnSearchStateChanged);

    public bool IsSearchVisible
    {
        get => (bool)GetValue(IsSearchVisibleProperty);
        set => SetValue(IsSearchVisibleProperty, value);
    }

    public static readonly BindableProperty SearchPlaceholderProperty =
        BindableProperty.Create(nameof(SearchPlaceholder), typeof(string), typeof(TopBar), "Search…");

    public string SearchPlaceholder
    {
        get => (string)GetValue(SearchPlaceholderProperty);
        set => SetValue(SearchPlaceholderProperty, value);
    }

    public static readonly BindableProperty SearchTextProperty =
        BindableProperty.Create(nameof(SearchText), typeof(string), typeof(TopBar), default(string), BindingMode.TwoWay);

    public string? SearchText
    {
        get => (string?)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public static readonly BindableProperty SearchCommandProperty =
        BindableProperty.Create(nameof(SearchCommand), typeof(ICommand), typeof(TopBar), default(ICommand));

    public ICommand? SearchCommand
    {
        get => (ICommand?)GetValue(SearchCommandProperty);
        set => SetValue(SearchCommandProperty, value);
    }

    public static readonly BindableProperty RightContentProperty =
        BindableProperty.Create(nameof(RightContent), typeof(View), typeof(TopBar), default(View));

    public View? RightContent
    {
        get => (View?)GetValue(RightContentProperty);
        set => SetValue(RightContentProperty, value);
    }

    public static readonly BindableProperty OpenMenuCommandProperty =
        BindableProperty.Create(nameof(OpenMenuCommand), typeof(ICommand), typeof(TopBar), default(ICommand), propertyChanged: (bindable, _, _) =>
        {
            if (bindable is TopBar bar)
            {
                bar.UpdateMenuAvailability();
            }
        });

    public ICommand? OpenMenuCommand
    {
        get => (ICommand?)GetValue(OpenMenuCommandProperty);
        set => SetValue(OpenMenuCommandProperty, value);
    }

    public static readonly BindableProperty IsMenuAvailableProperty =
        BindableProperty.Create(nameof(IsMenuAvailable), typeof(bool), typeof(TopBar), true);

    public bool IsMenuAvailable
    {
        get => (bool)GetValue(IsMenuAvailableProperty);
        private set => SetValue(IsMenuAvailableProperty, value);
    }

    public static readonly BindableProperty IsSearchExpandableProperty =
        BindableProperty.Create(nameof(IsSearchExpandable), typeof(bool), typeof(TopBar), false, propertyChanged: OnSearchStateChanged);

    public bool IsSearchExpandable
    {
        get => (bool)GetValue(IsSearchExpandableProperty);
        set => SetValue(IsSearchExpandableProperty, value);
    }

    public static readonly BindableProperty IsSearchExpandedProperty =
        BindableProperty.Create(nameof(IsSearchExpanded), typeof(bool), typeof(TopBar), false, BindingMode.TwoWay, propertyChanged: OnSearchStateChanged);

    public bool IsSearchExpanded
    {
        get => (bool)GetValue(IsSearchExpandedProperty);
        set => SetValue(IsSearchExpandedProperty, value);
    }

    public static readonly BindableProperty NavigationModeProperty =
        BindableProperty.Create(nameof(NavigationMode), typeof(TopBarNavigationMode), typeof(TopBar), TopBarNavigationMode.Menu);

    public TopBarNavigationMode NavigationMode
    {
        get => (TopBarNavigationMode)GetValue(NavigationModeProperty);
        set => SetValue(NavigationModeProperty, value);
    }

    public static readonly BindableProperty BackCommandProperty =
        BindableProperty.Create(nameof(BackCommand), typeof(ICommand), typeof(TopBar), default(ICommand));

    public ICommand? BackCommand
    {
        get => (ICommand?)GetValue(BackCommandProperty);
        set => SetValue(BackCommandProperty, value);
    }

    private void OnSearchToggleClicked(object? sender, EventArgs e)
    {
        IsSearchExpanded = !IsSearchExpanded;
        UpdateSearchVisibility();
        if (IsSearchExpanded)
        {
            SearchInput?.Focus();
        }
    }

    private void UpdateSearchVisibility()
    {
        if (SearchToggleButton is null || SearchInput is null)
        {
            return;
        }

        SearchToggleButton.IsVisible = IsSearchVisible && IsSearchExpandable;
        SearchInput.IsVisible = IsSearchVisible && (!IsSearchExpandable || IsSearchExpanded);
    }

    private void UpdateMenuAvailability()
    {
        var shell = Shell.Current;
        var hasFlyout = shell is not null &&
                        shell.FlyoutBehavior != FlyoutBehavior.Disabled &&
                        shell.FlyoutItems is ICollection collection &&
                        collection.Count > 0;
        IsMenuAvailable = hasFlyout || OpenMenuCommand is not null;
    }

    private static void OnSearchStateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is TopBar topBar)
        {
            topBar.UpdateSearchVisibility();
        }
    }
}
