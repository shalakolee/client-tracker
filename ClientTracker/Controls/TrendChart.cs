using System.Globalization;
using System.Linq;
using ClientTracker.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace ClientTracker.Controls;

public sealed class TrendChart : GraphicsView
{
    private RectF _lastPlot;

    public TrendChart()
    {
        Drawable = new TrendChartDrawable(this);
        HeightRequest = 260;
        StartInteraction += OnStartInteraction;
        DragInteraction += OnDragInteraction;
    }

    public static readonly BindableProperty LabelsProperty =
        BindableProperty.Create(nameof(Labels), typeof(IReadOnlyList<string>), typeof(TrendChart), Array.Empty<string>(),
            propertyChanged: (bindable, _, _) => ((TrendChart)bindable).Invalidate());

    public IReadOnlyList<string> Labels
    {
        get => (IReadOnlyList<string>)GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    public static readonly BindableProperty SeriesAProperty =
        BindableProperty.Create(nameof(SeriesA), typeof(IReadOnlyList<double>), typeof(TrendChart), Array.Empty<double>(),
            propertyChanged: (bindable, _, _) => ((TrendChart)bindable).Invalidate());

    public IReadOnlyList<double> SeriesA
    {
        get => (IReadOnlyList<double>)GetValue(SeriesAProperty);
        set => SetValue(SeriesAProperty, value);
    }

    public static readonly BindableProperty SeriesBProperty =
        BindableProperty.Create(nameof(SeriesB), typeof(IReadOnlyList<double>), typeof(TrendChart), Array.Empty<double>(),
            propertyChanged: (bindable, _, _) => ((TrendChart)bindable).Invalidate());

    public IReadOnlyList<double> SeriesB
    {
        get => (IReadOnlyList<double>)GetValue(SeriesBProperty);
        set => SetValue(SeriesBProperty, value);
    }

    public static readonly BindableProperty SelectedIndexProperty =
        BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(TrendChart), -1,
            BindingMode.TwoWay,
            propertyChanged: (bindable, _, _) => ((TrendChart)bindable).Invalidate());

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public static readonly BindableProperty IsSelectionEnabledProperty =
        BindableProperty.Create(nameof(IsSelectionEnabled), typeof(bool), typeof(TrendChart), true,
            propertyChanged: (bindable, _, _) => ((TrendChart)bindable).Invalidate());

    public bool IsSelectionEnabled
    {
        get => (bool)GetValue(IsSelectionEnabledProperty);
        set => SetValue(IsSelectionEnabledProperty, value);
    }

    public Color SeriesAColor { get; set; } = Color.FromArgb("#6366F1");
    public Color SeriesBColor { get; set; } = Color.FromArgb("#14B8A6");

    private void OnStartInteraction(object? sender, TouchEventArgs e) => UpdateSelectionFromTouch(e);

    private void OnDragInteraction(object? sender, TouchEventArgs e) => UpdateSelectionFromTouch(e);

    private void UpdateSelectionFromTouch(TouchEventArgs e)
    {
        if (!IsSelectionEnabled || _lastPlot.Width <= 1 || _lastPlot.Height <= 1)
        {
            return;
        }

        var count = Math.Min(SeriesA?.Count ?? 0, SeriesB?.Count ?? 0);
        if (count <= 0)
        {
            return;
        }

        var location = e.Touches.FirstOrDefault();
        if (location.X < _lastPlot.Left)
        {
            SelectedIndex = 0;
            return;
        }

        if (location.X > _lastPlot.Right)
        {
            SelectedIndex = count - 1;
            return;
        }

        var stepX = count <= 1 ? _lastPlot.Width : _lastPlot.Width / (count - 1);
        var index = (int)Math.Round((location.X - _lastPlot.Left) / stepX);
        SelectedIndex = Math.Clamp(index, 0, count - 1);
    }

    private sealed class TrendChartDrawable(TrendChart owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.SaveState();

            canvas.Antialias = true;
            canvas.FillColor = Colors.Transparent;
            canvas.FillRectangle(dirtyRect);

            var paddingLeft = 36f;
            var paddingTop = 10f;
            var paddingRight = 10f;
            var paddingBottom = 24f;

            var plot = new RectF(
                dirtyRect.Left + paddingLeft,
                dirtyRect.Top + paddingTop,
                dirtyRect.Width - paddingLeft - paddingRight,
                dirtyRect.Height - paddingTop - paddingBottom);

            owner._lastPlot = plot;

            DrawGrid(canvas, plot);

            var a = owner.SeriesA ?? Array.Empty<double>();
            var b = owner.SeriesB ?? Array.Empty<double>();
            var max = Math.Max(a.Count > 0 ? a.Max() : 0, b.Count > 0 ? b.Max() : 0);
            if (max <= 0 || plot.Width <= 1 || plot.Height <= 1)
            {
                canvas.RestoreState();
                return;
            }

            DrawSeries(canvas, plot, a, owner.SeriesAColor, max);
            DrawSeries(canvas, plot, b, owner.SeriesBColor, max);
            DrawLabels(canvas, plot, owner.Labels ?? Array.Empty<string>());
            DrawAxisLabels(canvas, plot, max);
            DrawSelection(canvas, plot, max, owner);

            canvas.RestoreState();
        }

        private static void DrawGrid(ICanvas canvas, RectF plot)
        {
            canvas.StrokeColor = Color.FromArgb("#B4BCCB");
            canvas.StrokeSize = 1;
            canvas.Alpha = 0.25f;

            const int lines = 4;
            for (var i = 0; i <= lines; i++)
            {
                var y = plot.Top + plot.Height * (i / (float)lines);
                canvas.DrawLine(plot.Left, y, plot.Right, y);
            }

            canvas.Alpha = 1;
        }

        private static void DrawAxisLabels(ICanvas canvas, RectF plot, double max)
        {
            canvas.FontColor = Color.FromArgb("#94A3B8");
            canvas.FontSize = 10;

            var top = FormatAxisValue(max);
            var mid = FormatAxisValue(max / 2d);
            var bottom = FormatAxisValue(0);

            canvas.DrawString(top, plot.Left - 26, plot.Top - 2, HorizontalAlignment.Left);
            canvas.DrawString(mid, plot.Left - 26, plot.Top + plot.Height / 2f - 6, HorizontalAlignment.Left);
            canvas.DrawString(bottom, plot.Left - 26, plot.Bottom - 10, HorizontalAlignment.Left);
        }

        private static string FormatAxisValue(double value)
        {
            if (value >= 1_000_000)
            {
                return $"{value / 1_000_000d:0.#}M";
            }

            if (value >= 1_000)
            {
                return $"{value / 1_000d:0.#}K";
            }

            return value.ToString("0");
        }

        private static void DrawSeries(ICanvas canvas, RectF plot, IReadOnlyList<double> values, Color color, double max)
        {
            if (values.Count < 2)
            {
                return;
            }

            var stepX = plot.Width / (values.Count - 1);
            var points = new PointF[values.Count];
            for (var i = 0; i < values.Count; i++)
            {
                var v = values[i];
                var x = plot.Left + stepX * i;
                var y = plot.Bottom - (float)(plot.Height * (v / max));
                points[i] = new PointF(x, y);
            }

            canvas.StrokeColor = color;
            canvas.StrokeSize = 3;
            var stroke = new PathF();
            stroke.MoveTo(points[0]);
            for (var i = 1; i < points.Length; i++)
            {
                stroke.LineTo(points[i]);
            }
            canvas.DrawPath(stroke);

            canvas.FillColor = color.WithAlpha(0.08f);
            var path = new PathF();
            path.MoveTo(points[0]);
            for (var i = 1; i < points.Length; i++)
            {
                path.LineTo(points[i]);
            }
            path.LineTo(points[^1].X, plot.Bottom);
            path.LineTo(points[0].X, plot.Bottom);
            path.Close();
            canvas.FillPath(path);
        }

        private static void DrawLabels(ICanvas canvas, RectF plot, IReadOnlyList<string> labels)
        {
            if (labels.Count == 0)
            {
                return;
            }

            canvas.FontColor = Color.FromArgb("#64748B");
            canvas.FontSize = 11;

            var show = Math.Min(labels.Count, 6);
            var step = labels.Count <= show ? 1 : (int)Math.Ceiling(labels.Count / (double)show);
            var items = new List<(int index, string label)>();
            for (var i = 0; i < labels.Count; i += step)
            {
                items.Add((i, labels[i]));
            }

            var stepX = labels.Count <= 1 ? plot.Width : plot.Width / (labels.Count - 1);
            foreach (var (index, label) in items)
            {
                var x = plot.Left + stepX * index;
                canvas.DrawString(label, x, plot.Bottom + 6, HorizontalAlignment.Center);
            }
        }

        private static void DrawSelection(ICanvas canvas, RectF plot, double max, TrendChart owner)
        {
            if (!owner.IsSelectionEnabled)
            {
                return;
            }

            var labels = owner.Labels ?? Array.Empty<string>();
            var a = owner.SeriesA ?? Array.Empty<double>();
            var b = owner.SeriesB ?? Array.Empty<double>();
            var count = Math.Min(Math.Min(labels.Count, a.Count), b.Count);
            if (count == 0)
            {
                return;
            }

            if (owner.SelectedIndex < 0 || owner.SelectedIndex >= count)
            {
                owner.SelectedIndex = count - 1;
            }

            var index = owner.SelectedIndex;
            var stepX = count <= 1 ? plot.Width : plot.Width / (count - 1);
            var x = plot.Left + stepX * index;

            canvas.StrokeColor = Color.FromArgb("#94A3B8");
            canvas.StrokeSize = 1;
            canvas.Alpha = 0.6f;
            canvas.DrawLine(x, plot.Top, x, plot.Bottom);
            canvas.Alpha = 1;

            var sales = a[index];
            var commission = b[index];
            var label = labels[index];
            var localization = LocalizationResourceManager.Instance;
            var salesLabel = localization["Dashboard_Sales"];
            var commissionLabel = localization["Dashboard_Commission"];
            var salesText = string.Format(CultureInfo.CurrentCulture, "{0}: {1:C0}", salesLabel, sales);
            var commissionText = string.Format(CultureInfo.CurrentCulture, "{0}: {1:C0}", commissionLabel, commission);

            canvas.FontSize = 11;
            var padding = 8f;
            var lineHeight = 14f;
            var width = Math.Max(EstimateTextWidth(label, 11),
                        Math.Max(EstimateTextWidth(salesText, 11), EstimateTextWidth(commissionText, 11)));
            var tooltipWidth = width + padding * 2;
            var tooltipHeight = lineHeight * 3 + padding * 2;
            var left = Math.Clamp(x - tooltipWidth / 2f, plot.Left, plot.Right - tooltipWidth);
            var top = Math.Max(plot.Top, plot.Top + 8);

            canvas.FillColor = Color.FromArgb("#FFFFFF");
            canvas.StrokeColor = Color.FromArgb("#E2E8F0");
            canvas.StrokeSize = 1;
            canvas.FillRoundedRectangle(left, top, tooltipWidth, tooltipHeight, 10);
            canvas.DrawRoundedRectangle(left, top, tooltipWidth, tooltipHeight, 10);

            canvas.FontColor = Color.FromArgb("#0F172A");
            canvas.DrawString(label, left + padding, top + padding, HorizontalAlignment.Left);
            canvas.FontColor = owner.SeriesAColor;
            canvas.DrawString(salesText, left + padding, top + padding + lineHeight, HorizontalAlignment.Left);
            canvas.FontColor = owner.SeriesBColor;
            canvas.DrawString(commissionText, left + padding, top + padding + lineHeight * 2, HorizontalAlignment.Left);
        }

        private static float EstimateTextWidth(string text, float fontSize)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            return text.Length * fontSize * 0.55f;
        }
    }
}
