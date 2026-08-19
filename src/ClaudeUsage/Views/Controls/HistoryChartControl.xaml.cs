using System.Collections.ObjectModel;
using System.Collections.Specialized;
using ClaudeUsage.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace ClaudeUsage.Views.Controls;

/// <summary>Lightweight sparkline-style chart: one Polyline + one filled Polygon, redrawn on size/data change. No charting library dependency.</summary>
public sealed partial class HistoryChartControl : UserControl
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points),
        typeof(ObservableCollection<UsageHistoryPoint>),
        typeof(HistoryChartControl),
        new PropertyMetadata(null, OnPointsChanged));

    public ObservableCollection<UsageHistoryPoint>? Points
    {
        get => (ObservableCollection<UsageHistoryPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public HistoryChartControl()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
    }

    private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HistoryChartControl)d;

        if (e.OldValue is ObservableCollection<UsageHistoryPoint> oldCollection)
        {
            oldCollection.CollectionChanged -= control.OnCollectionChanged;
        }

        if (e.NewValue is ObservableCollection<UsageHistoryPoint> newCollection)
        {
            newCollection.CollectionChanged += control.OnCollectionChanged;
        }

        control.Redraw();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        LinePolyline.Points.Clear();
        FillPolygon.Points.Clear();

        var points = Points;
        var width = ActualWidth;
        var height = ActualHeight;

        if (points is null || points.Count < 2 || width <= 0 || height <= 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;

        const double topPadding = 6.0;
        var usableHeight = height - topPadding;
        var stepX = width / (points.Count - 1);

        var linePoints = new PointCollection();
        for (var i = 0; i < points.Count; i++)
        {
            var x = i * stepX;
            var normalized = Math.Clamp(points[i].UsagePercent, 0, 100) / 100.0;
            var y = topPadding + (1 - normalized) * usableHeight;
            linePoints.Add(new Point(x, y));
        }

        foreach (var point in linePoints)
        {
            LinePolyline.Points.Add(point);
        }

        foreach (var point in linePoints)
        {
            FillPolygon.Points.Add(point);
        }

        FillPolygon.Points.Add(new Point(width, height));
        FillPolygon.Points.Add(new Point(0, height));
    }
}
