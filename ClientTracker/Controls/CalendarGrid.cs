using System.Collections;
using System.Windows.Input;
using ClientTracker.Models;
using Microsoft.Maui.Controls.Shapes;

namespace ClientTracker.Controls;

public sealed class CalendarGrid : Grid
{
    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(
            nameof(ItemsSource),
            typeof(IList),
            typeof(CalendarGrid),
            default(IList),
            propertyChanged: (_, __, ___) => { });

    public IList? ItemsSource
    {
        get => (IList?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly BindableProperty DayTappedCommandProperty =
        BindableProperty.Create(
            nameof(DayTappedCommand),
            typeof(ICommand),
            typeof(CalendarGrid),
            default(ICommand));

    public ICommand? DayTappedCommand
    {
        get => (ICommand?)GetValue(DayTappedCommandProperty);
        set => SetValue(DayTappedCommandProperty, value);
    }

    public CalendarGrid()
    {
        ColumnSpacing = 6;
        RowSpacing = 6;

        for (var i = 0; i < 7; i++)
        {
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (var i = 0; i < 6; i++)
        {
            RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == ItemsSourceProperty.PropertyName)
        {
            Rebuild();
        }
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        Rebuild();
    }

    private void Rebuild()
    {
        Children.Clear();

        if (ItemsSource is null || ItemsSource.Count == 0)
        {
            return;
        }

        var count = Math.Min(42, ItemsSource.Count);
        for (var index = 0; index < count; index++)
        {
            if (ItemsSource[index] is not CalendarDay day)
            {
                continue;
            }

            var border = CreateDayCell(day);
            var row = index / 7;
            var col = index % 7;
            border.SetValue(RowProperty, row);
            border.SetValue(ColumnProperty, col);
            Children.Add(border);
        }
    }

    private Border CreateDayCell(CalendarDay day)
    {
        var border = new Border
        {
            StrokeThickness = 1,
            BindingContext = day,
            BackgroundColor = GetColorResource("Surface", Colors.White),
            Stroke = GetColorResource("BorderSubtle", Colors.LightGray),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Padding = new Thickness(6)
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, __) =>
        {
            var command = DayTappedCommand;
            if (command?.CanExecute(day) == true)
            {
                command.Execute(day);
            }
        };
        border.GestureRecognizers.Add(tap);

        border.Triggers.Add(new DataTrigger(typeof(Border))
        {
            Binding = new Binding(nameof(CalendarDay.IsCurrentMonth)),
            Value = false,
            Setters = { new Setter { Property = VisualElement.OpacityProperty, Value = 0.4 } }
        });

        border.Triggers.Add(new DataTrigger(typeof(Border))
        {
            Binding = new Binding(nameof(CalendarDay.IsPayDate)),
            Value = true,
            Setters =
            {
                new Setter { Property = Border.BackgroundColorProperty, Value = Color.FromArgb("#E0F2F1") },
                new Setter { Property = Border.StrokeProperty, Value = GetColorResource("Primary", Colors.Blue) }
            }
        });

        border.Triggers.Add(new DataTrigger(typeof(Border))
        {
            Binding = new Binding(nameof(CalendarDay.IsSelected)),
            Value = true,
            Setters =
            {
                new Setter { Property = Border.BackgroundColorProperty, Value = GetColorResource("Secondary", Colors.LightBlue) },
                new Setter { Property = Border.StrokeProperty, Value = GetColorResource("Tertiary", Colors.Blue) }
            }
        });

        var dayLabel = new Label { FontAttributes = FontAttributes.Bold };
        dayLabel.SetBinding(Label.TextProperty, nameof(CalendarDay.DayLabel));

        var commissionLabel = new Label { FontSize = 11 };
        commissionLabel.SetBinding(Label.TextProperty, new Binding(nameof(CalendarDay.CommissionTotal), stringFormat: "{0:C}"));
        commissionLabel.SetBinding(IsVisibleProperty, nameof(CalendarDay.IsPayDate));

        var pill = new Border
        {
            BackgroundColor = GetColorResource("SurfaceAlt", Colors.WhiteSmoke),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
            Padding = new Thickness(8, 3),
            HorizontalOptions = LayoutOptions.Start
        };
        pill.SetBinding(IsVisibleProperty, nameof(CalendarDay.IsPayDate));

        var countLabel = new Label();
        if (TryGetStyleResource("MutedLabel", out var mutedLabelStyle))
        {
            countLabel.Style = mutedLabelStyle;
        }
        countLabel.SetBinding(Label.TextProperty, nameof(CalendarDay.PaymentCount));
        pill.Content = countLabel;

        border.Content = new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                dayLabel,
                commissionLabel,
                pill
            }
        };

        return border;
    }

    private static Color GetColorResource(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
        {
            return color;
        }

        return fallback;
    }

    private static bool TryGetStyleResource(string key, out Style style)
    {
        style = default!;
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Style s)
        {
            style = s;
            return true;
        }

        return false;
    }
}
