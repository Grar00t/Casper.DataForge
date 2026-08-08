using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Casper.DataForge.CrossPlatform.Engine;

namespace Casper.DataForge.CrossPlatform;

public partial class MainWindow : Window
{
    private readonly CasperEngineClient _engine = new();
    private OutputFormat _format = OutputFormat.Json;
    private bool _queryRunning;

    public MainWindow()
    {
        InitializeComponent();
        UpdateEngineStatus();
        Render();
    }

    private void InputTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
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
        AutoDirection();
    }

    private void RtlDirection_Click(object? sender, RoutedEventArgs e)
    {
        InputTextBox.FlowDirection =
            Avalonia.Media.FlowDirection.RightToLeft;
    }

    private void LtrDirection_Click(object? sender, RoutedEventArgs e)
    {
        InputTextBox.FlowDirection =
            Avalonia.Media.FlowDirection.LeftToRight;
    }

    private async void Copy_Click(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;

        if (clipboard is null)
            return;

        await clipboard.SetTextAsync(OutputTextBox.Text ?? string.Empty);
        StatusText.Text = "Copied";
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        string extension =
            _format == OutputFormat.Json ? "json" : "jsonl";

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
        SendButton.IsEnabled = false;
        SendButton.Content = "Running...";

        AnswerTextBox.Text = string.Empty;
        SourcesTextBox.Text = string.Empty;
        ConfidenceText.Text = "Confidence: -";
        ProofText.Text = "Proof: -";
        StatusText.Text = "Casper is processing the query";

        try
        {
            CasperResponse response =
                await _engine.QueryAsync(query);

            DisplayCasperResponse(response);
        }
        catch (Exception exception)
        {
            AnswerTextBox.Text = exception.ToString();
            StatusText.Text = "Casper engine query failed";
            EngineBadgeText.Text = "ERROR";
        }
        finally
        {
            _queryRunning = false;
            SendButton.IsEnabled = true;
            SendButton.Content = "Send";
        }
    }

    private void DisplayCasperResponse(CasperResponse response)
    {
        string answer =
            string.IsNullOrWhiteSpace(response.Answer)
                ? response.Error ?? "No answer returned."
                : response.Answer;

        AnswerTextBox.Text = WebUtility.HtmlDecode(answer);

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

        foreach (CasperSource source in sources)
        {
            builder.Append('[')
                .Append(source.Number)
                .Append("] ")
                .AppendLine(
                    WebUtility.HtmlDecode(
                        source.Title ?? "Untitled source"));

            builder.Append("Score: ")
                .AppendLine(source.Score.ToString("0.000"));

            if (!string.IsNullOrWhiteSpace(source.Url))
            {
                builder.Append("URL: ")
                    .AppendLine(SourceTextNormalizer.NormalizeUrl(source.Url));
            }

            if (!string.IsNullOrWhiteSpace(source.Snippet))
            {
                builder.AppendLine(
                    WebUtility.HtmlDecode(source.Snippet));
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
}

