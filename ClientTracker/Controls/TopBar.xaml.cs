using System.Windows.Input;

namespace ClientTracker.Controls;

public partial class TopBar : ContentView
{
    public TopBar()
    {
        InitializeComponent();
    }

    private void OnMenuClicked(object? sender, EventArgs e)
    {
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
        BindableProperty.Create(nameof(IsSearchVisible), typeof(bool), typeof(TopBar), false);

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

    public static readonly BindableProperty RightContentProperty =
        BindableProperty.Create(nameof(RightContent), typeof(View), typeof(TopBar), default(View));

    public View? RightContent
    {
        get => (View?)GetValue(RightContentProperty);
        set => SetValue(RightContentProperty, value);
    }

    public static readonly BindableProperty OpenMenuCommandProperty =
        BindableProperty.Create(nameof(OpenMenuCommand), typeof(ICommand), typeof(TopBar), default(ICommand));

    public ICommand? OpenMenuCommand
    {
        get => (ICommand?)GetValue(OpenMenuCommandProperty);
        set => SetValue(OpenMenuCommandProperty, value);
    }
}
