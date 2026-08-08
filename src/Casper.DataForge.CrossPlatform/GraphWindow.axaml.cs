using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;

namespace Casper.DataForge.CrossPlatform;

public partial class GraphWindow : Window
{
    private static readonly IBrush QueryBrush = new SolidColorBrush(Color.Parse("#2C69A8"));
    private static readonly IBrush SourceBrush = new SolidColorBrush(Color.Parse("#263A4D"));
    private static readonly IBrush KnowledgeBrush = new SolidColorBrush(Color.Parse("#45326B"));
    private static readonly IBrush EdgeBrush = new SolidColorBrush(Color.Parse("#61758C"));

    private readonly DispatcherTimer _animationTimer;
    private KnowledgeGraph _graph;
    private double _rotation;

    public GraphWindow()
        : this(new KnowledgeGraph(
            Array.Empty<GraphNode>(),
            Array.Empty<GraphEdge>()))
    {
    }

    public GraphWindow(KnowledgeGraph graph)
    {
        InitializeComponent();
        _graph = graph;
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(32)
        };
        _animationTimer.Tick += AnimationTimer_Tick;
        _animationTimer.Start();
        Render();
    }

    public void UpdateGraph(KnowledgeGraph graph)
    {
        _graph = graph;
        Render();
    }

    protected override void OnClosed(EventArgs e)
    {
        _animationTimer.Stop();
        _animationTimer.Tick -= AnimationTimer_Tick;
        base.OnClosed(e);
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        _rotation += 0.012;
        if (_rotation > Math.PI * 2)
            _rotation -= Math.PI * 2;

        Render();
    }

    private void Render()
    {
        GraphCanvas.Children.Clear();
        SummaryText.Text =
            $"LIVE 3D PROJECTION · {_graph.Nodes.Count} nodes · " +
            $"{_graph.Edges.Count} links · persisted locally";

        var positions = new Dictionary<string, ProjectedNode>(StringComparer.Ordinal);
        GraphNode? queryNode = _graph.Nodes.FirstOrDefault(
            static node => node.Kind == "query");

        if (queryNode is not null)
        {
            positions[queryNode.Id] = Project(new Point3D(0, -170, 0), 1);
        }

        List<GraphNode> orbitNodes = _graph.Nodes
            .Where(static node => node.Kind != "query")
            .ToList();

        for (var index = 0; index < orbitNodes.Count; index++)
        {
            double orbit = index * (Math.PI * 2 / Math.Max(orbitNodes.Count, 1));
            double radius = orbitNodes.Count > 16 ? 260 : 230;
            double x = Math.Cos(orbit) * radius;
            double z = Math.Sin(orbit) * 155;
            double y = ((index % 3) - 1) * 78;
            positions[orbitNodes[index].Id] = Project(new Point3D(x, y, z), 1);
        }

        foreach (GraphEdge edge in _graph.Edges)
        {
            if (!positions.TryGetValue(edge.From, out ProjectedNode from) ||
                !positions.TryGetValue(edge.To, out ProjectedNode to))
                continue;

            GraphCanvas.Children.Add(new Line
            {
                StartPoint = new Point(from.X, from.Y),
                EndPoint = new Point(to.X, to.Y),
                Stroke = EdgeBrush,
                StrokeThickness = Math.Clamp(1.0 * Math.Min(from.Scale, to.Scale), 0.7, 2.2)
            });

            AddText(
                edge.Label,
                (from.X + to.X) / 2 - 45,
                (from.Y + to.Y) / 2 - 10,
                10,
                "#8FBCE8",
                90);
        }

        foreach ((GraphNode node, ProjectedNode position) in _graph.Nodes
                     .Where(node => positions.ContainsKey(node.Id))
                     .Select(node => (node, positions[node.Id]))
                     .OrderBy(item => item.Item2.Depth))
        {
            AddNode(node, position);
        }
    }

    private ProjectedNode Project(Point3D point, double scale)
    {
        double cos = Math.Cos(_rotation);
        double sin = Math.Sin(_rotation);
        double rotatedX = point.X * cos - point.Z * sin;
        double rotatedZ = point.X * sin + point.Z * cos;
        double perspective = 560 / Math.Max(240, 560 - rotatedZ);

        return new ProjectedNode(
            500 + rotatedX * perspective,
            310 + point.Y * perspective,
            rotatedZ,
            Math.Clamp(scale * perspective, 0.6, 1.35));
    }

    private void AddNode(GraphNode node, ProjectedNode position)
    {
        double diameter = 54 * position.Scale;
        var ellipse = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Fill = node.Kind == "query"
                ? QueryBrush
                : node.Kind == "knowledge"
                    ? KnowledgeBrush
                    : SourceBrush,
            Stroke = node.Kind == "query"
                ? Brushes.LightSkyBlue
                : Brushes.SlateGray,
            StrokeThickness = 1.5
        };
        Canvas.SetLeft(ellipse, position.X - diameter / 2);
        Canvas.SetTop(ellipse, position.Y - diameter / 2);
        GraphCanvas.Children.Add(ellipse);

        string label = node.Label.Length > 52
            ? node.Label[..52] + "…"
            : node.Label;

        AddText(
            label,
            position.X - 105,
            position.Y + diameter / 2 + 6,
            12,
            "#E8EAF0",
            210);
    }

    private void AddText(
        string text,
        double x,
        double y,
        double fontSize,
        string color,
        double width)
    {
        var block = new TextBlock
        {
            Text = text,
            Width = width,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            FontSize = fontSize,
            Foreground = new SolidColorBrush(Color.Parse(color))
        };
        Canvas.SetLeft(block, x);
        Canvas.SetTop(block, y);
        GraphCanvas.Children.Add(block);
    }

    private readonly record struct Point3D(double X, double Y, double Z);

    private readonly record struct ProjectedNode(
        double X,
        double Y,
        double Depth,
        double Scale);
}
