using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Casper.DataForge.CrossPlatform.Data;
using Casper.DataForge.CrossPlatform.Engine;
using Casper.DataForge.Core;
using Avalonia.Media;

namespace Casper.DataForge.CrossPlatform;

public partial class MainWindow : Window
{
    private const int MaxChatMessages = 100;
    private readonly CasperEngineClient _engine = new();
    private readonly LocalDatabase _database = new();
    private OutputFormat _format = OutputFormat.Json;
    private DirectionMode _directionMode = DirectionMode.Auto;
    private bool _queryRunning;
    private CasperResponse? _lastResponse;
    private string _lastQuery = string.Empty;
    private KnowledgeBaseCatalog? _knowledgeBase;
    private HistoryWindow? _historyWindow;
    private GraphWindow? _graphWindow;
    private CancellationTokenSource? _queryCancellation;

    public MainWindow()
    {
        InitializeComponent();
        UpdateEngineStatus();
        UpdateDatabaseStatus();
        LoadKnowledgeBase();
        Render();
        AddChatMessage(
            "CASPER / كاسبر",
            "Ready for a grounded query. / جاهز لاستعلام موثق.",
            isUser: false);
    }

    protected override void OnClosed(EventArgs e)
    {
        _queryCancellation?.Cancel();
        _graphWindow?.Close();
        _historyWindow?.Close();
        _database.Dispose();
        base.OnClosed(e);
    }

    private void InputTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_directionMode == DirectionMode.Auto)
            AutoDirection();

        Render();
    }

    private void Json_Click(object? sender, RoutedEventArgs e)
    {
        _format = OutputFormat.Json;
        Render();
    }

    private void Jsonl_Click(object? sender, RoutedEventArgs e)
    {
        _format = OutputFormat.Jsonl;
        Render();
    }

    private void AutoDirection_Click(object? sender, RoutedEventArgs e)
    {
        _directionMode = DirectionMode.Auto;
        AutoDirection();
    }

    private void RtlDirection_Click(object? sender, RoutedEventArgs e)
    {
        _directionMode = DirectionMode.Rtl;
        InputTextBox.FlowDirection =
            Avalonia.Media.FlowDirection.RightToLeft;
    }

    private void LtrDirection_Click(object? sender, RoutedEventArgs e)
    {
        _directionMode = DirectionMode.Ltr;
        InputTextBox.FlowDirection =
            Avalonia.Media.FlowDirection.LeftToRight;
    }

    private async void Copy_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;

            if (clipboard is null)
            {
                StatusText.Text = "Clipboard unavailable";
                return;
            }

            await clipboard.SetTextAsync(OutputTextBox.Text ?? string.Empty);
            StatusText.Text = "Copied";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Copy failed: {exception.Message}";
        }
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        string extension =
            _format == OutputFormat.Json ? "json" : "jsonl";

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    SuggestedFileName = $"dataforge.{extension}",
                    DefaultExtension = extension,
                    FileTypeChoices =
                    [
                        new FilePickerFileType(
                            _format == OutputFormat.Json
                                ? "JSON"
                                : "JSON Lines")
                        {
                            Patterns = [$"*.{extension}"]
                        }
                    ]
                });

            if (file is null)
                return;

            await using Stream stream = await file.OpenWriteAsync();
            stream.SetLength(0);

            await using var writer =
                new StreamWriter(stream, new UTF8Encoding(false));

            await writer.WriteAsync(OutputTextBox.Text ?? string.Empty);
            StatusText.Text = $"Saved: {file.Name}";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Save failed: {exception.Message}";
        }
    }

    private async void SendQuery_Click(object? sender, RoutedEventArgs e)
    {
        if (_queryRunning)
            return;

        string query = QueryTextBox.Text?.Trim() ?? string.Empty;

        if (query.Length == 0)
        {
            StatusText.Text = "Enter a Casper query";
            return;
        }

        if (!_engine.IsAvailable)
        {
            UpdateEngineStatus();
            StatusText.Text = "Casper engine is unavailable";
            return;
        }

        _queryRunning = true;
        var cancellation = new CancellationTokenSource();
        _queryCancellation = cancellation;
        SendButton.IsEnabled = false;
        SendButton.Content = "Running...";

        SourcesTextBox.Text = string.Empty;
        ConfidenceText.Text = "Confidence: -";
        ProofText.Text = "Proof: -";
        StatusText.Text = "Casper is processing the query";
        AddChatMessage("YOU / أنت", query, isUser: true);
        ShowGraph(new KnowledgeGraph(
            [new GraphNode("query", query, "query")],
            Array.Empty<GraphEdge>()));

        try
        {
            CasperResponse response =
                await _engine.QueryAsync(query, cancellation.Token);

            _lastResponse = response;
            _lastQuery = query;
            DisplayCasperResponse(response);

            try
            {
                KnowledgeGraph graph = KnowledgeGraph.FromCasperResponse(query, response);
                await _database.SaveSessionAsync(
                    query,
                    response,
                    graph,
                    cancellation.Token);

                if (_graphWindow is not null)
                    _graphWindow.UpdateGraph(graph);
            }
            catch (Exception databaseException)
            {
                DatabaseStatusText.Text = "Database write failed";
                StatusText.Text = $"Casper completed · storage warning: {databaseException.Message}";
            }
        }
        catch (Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                StatusText.Text = "Casper query cancelled";
                return;
            }

            AddChatMessage("ERROR / خطأ", exception.ToString(), isUser: false);
            StatusText.Text = "Casper engine query failed";
            EngineBadgeText.Text = "ERROR";
        }
        finally
        {
            if (ReferenceEquals(_queryCancellation, cancellation))
                _queryCancellation = null;

            cancellation.Dispose();
            _queryRunning = false;
            SendButton.IsEnabled = true;
            SendButton.Content = "Send";
        }
    }

    private void Graph_Click(object? sender, RoutedEventArgs e)
    {
        if (_lastResponse is null)
        {
            StatusText.Text = "Run a Casper query before opening the graph";
            return;
        }

        KnowledgeGraph graph = KnowledgeGraph.FromCasperResponse(
            _lastQuery,
            _lastResponse);

        ShowGraph(graph);
    }

    private void Knowledge_Click(object? sender, RoutedEventArgs e)
    {
        if (_knowledgeBase is null)
        {
            StatusText.Text = "Knowledge base unavailable";
            return;
        }

        ShowGraph(_knowledgeBase.ToGraph());
    }

    private void ShowGraph(KnowledgeGraph graph)
    {

        if (_graphWindow is null)
        {
            _graphWindow = new GraphWindow(graph);
            _graphWindow.Closed += (_, _) => _graphWindow = null;
        }
        else
        {
            _graphWindow.UpdateGraph(graph);
        }

        if (_graphWindow.IsVisible)
            _graphWindow.Activate();
        else
            _graphWindow.Show(this);
    }

    private void History_Click(object? sender, RoutedEventArgs e)
    {
        if (_historyWindow is null)
        {
            _historyWindow = new HistoryWindow();
            _historyWindow.Closed += (_, _) => _historyWindow = null;
        }

        _historyWindow.Load(_database.GetRecentSessions());

        if (_historyWindow.IsVisible)
            _historyWindow.Activate();
        else
            _historyWindow.Show(this);
    }

    private void DisplayCasperResponse(CasperResponse response)
    {
        string answer =
            string.IsNullOrWhiteSpace(response.Answer)
                ? response.Error ?? "No answer returned."
                : response.Answer;

        AddChatMessage("CASPER / كاسبر", WebUtility.HtmlDecode(answer), isUser: false);

        ConfidenceText.Text =
            $"Confidence: {response.Confidence:0.000} | " +
            $"{response.ElapsedMilliseconds} ms";

        ProofText.Text =
            string.IsNullOrWhiteSpace(response.Proof)
                ? "Proof: -"
                : $"Proof: {ShortHash(response.Proof)}";

        SourcesTextBox.Text = FormatSources(response.Sources);

        StatusText.Text =
            $"Casper completed | Exit {response.ExitCode} | " +
            $"{response.SourceCount} sources";

        EngineBadgeText.Text =
            response.ExitCode == 0
                ? "ONLINE"
                : $"EXIT {response.ExitCode}";
    }

    private static string FormatSources(
        IReadOnlyList<CasperSource> sources)
    {
        if (sources.Count == 0)
            return "No sources returned.";

        var builder = new StringBuilder();

        for (var index = 0; index < sources.Count; index++)
        {
            CasperSource source = sources[index];
            builder.Append('[')
                .Append(source.Number == 0 ? index + 1 : source.Number)
                .Append("] ")
                .AppendLine(
                    string.IsNullOrWhiteSpace(source.Title)
                        ? "Untitled source"
                        : SourceTextNormalizer.DecodeHtml(source.Title));

            builder.Append("Score: ")
                .AppendLine(source.Score.ToString(
                    "0.000",
                    CultureInfo.InvariantCulture));

            if (!string.IsNullOrWhiteSpace(source.Url))
            {
                builder.Append("URL: ")
                    .AppendLine(SourceTextNormalizer.NormalizeUrl(source.Url));
            }

            if (!string.IsNullOrWhiteSpace(source.Snippet))
            {
                builder.AppendLine(
                        SourceTextNormalizer.DecodeHtml(source.Snippet));
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private void UpdateEngineStatus()
    {
        if (_engine.IsAvailable)
        {
            EngineStatusText.Text = "Engine executable detected";
            EngineBadgeText.Text = "READY";
        }
        else
        {
            EngineStatusText.Text = "Engine executable not found";
            EngineBadgeText.Text = "MISSING";
        }
    }

    private void UpdateDatabaseStatus()
    {
        DatabaseStatusText.Text = _database.IsReady
            ? "Local database ready"
            : "Local database unavailable";
    }

    private void LoadKnowledgeBase()
    {
        try
        {
            _knowledgeBase = KnowledgeBaseCatalog.LoadDefault();

            if (_database.IsReady)
                _database.SeedKnowledgeBase(_knowledgeBase);

            UpdateKnowledgeStatus();
        }
        catch
        {
            _knowledgeBase = null;
            UpdateKnowledgeStatus();
        }
    }

    private void UpdateKnowledgeStatus()
    {
        KnowledgeStatusText.Text = _knowledgeBase is null
            ? "Knowledge base unavailable"
            : $"Knowledge base ready · {_knowledgeBase.Nodes.Count} nodes";
    }

    private void AddChatMessage(string role, string text, bool isUser)
    {
        while (ChatMessagesList.Items.Count >= MaxChatMessages)
            ChatMessagesList.Items.RemoveAt(0);

        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Text = role,
            Foreground = new SolidColorBrush(Color.Parse(isUser ? "#8FC7FF" : "#9FE0B5")),
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 5)
        });
        body.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.Parse("#E8EAF0")),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13
        });

        ChatMessagesList.Items.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse(isUser ? "#172A40" : "#172A20")),
            BorderBrush = new SolidColorBrush(Color.Parse(isUser ? "#2F5D83" : "#2F6B48")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 8),
            Child = body
        });
    }

    private void AutoDirection()
    {
        InputTextBox.FlowDirection =
            DirectionDetector.ContainsArabic(
                InputTextBox.Text ?? string.Empty)
                ? Avalonia.Media.FlowDirection.RightToLeft
                : Avalonia.Media.FlowDirection.LeftToRight;
    }

    private void Render()
    {
        string source = InputTextBox.Text ?? string.Empty;

        OutputTextBox.Text =
            DeterministicConverter.Convert(source, _format);

        StatusText.Text =
            $"{_format.ToString().ToUpperInvariant()} | " +
            $"{source.Length} chars";
    }

    private static string ShortHash(string value)
    {
        return value.Length <= 12
            ? value
            : value[..12];
    }

    private enum DirectionMode
    {
        Auto,
        Rtl,
        Ltr
    }
}
