using Microsoft.Maui.Graphics;

namespace ClientTracker.Controls;

public sealed class TrendChart : GraphicsView
{
    public TrendChart()
    {
        Drawable = new TrendChartDrawable(this);
        HeightRequest = 260;
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

    public Color SeriesAColor { get; set; } = Color.FromArgb("#6366F1");
    public Color SeriesBColor { get; set; } = Color.FromArgb("#14B8A6");

    private sealed class TrendChartDrawable(TrendChart owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.SaveState();

            canvas.Antialias = true;
            canvas.FillColor = Colors.Transparent;
            canvas.FillRectangle(dirtyRect);

            var paddingLeft = 10f;
            var paddingTop = 10f;
            var paddingRight = 10f;
            var paddingBottom = 24f;

            var plot = new RectF(
                dirtyRect.Left + paddingLeft,
                dirtyRect.Top + paddingTop,
                dirtyRect.Width - paddingLeft - paddingRight,
                dirtyRect.Height - paddingTop - paddingBottom);

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
    }
}
