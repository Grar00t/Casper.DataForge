using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Casper.DataForge.Core;

namespace Casper.DataForge;

public partial class MainWindow : Window
{
    private OutputFormat _format = OutputFormat.Json;
    private DirectionMode _directionMode = DirectionMode.Auto;

    public MainWindow()
    {
        InitializeComponent();
        Render();
    }

    private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsInitialized) return;
        if (_directionMode == DirectionMode.Auto)
            AutoDirection();

        Render();
    }

    private void Json_Click(object sender, RoutedEventArgs e)
    {
        _format = OutputFormat.Json;
        Render();
    }

    private void Jsonl_Click(object sender, RoutedEventArgs e)
    {
        _format = OutputFormat.Jsonl;
        Render();
    }

    private void AutoDirection_Click(object sender, RoutedEventArgs e)
    {
        _directionMode = DirectionMode.Auto;
        AutoDirection();
    }

    private void RtlDirection_Click(object sender, RoutedEventArgs e)
    {
        _directionMode = DirectionMode.Rtl;
        InputTextBox.FlowDirection = FlowDirection.RightToLeft;
    }

    private void LtrDirection_Click(object sender, RoutedEventArgs e)
    {
        _directionMode = DirectionMode.Ltr;
        InputTextBox.FlowDirection = FlowDirection.LeftToRight;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(OutputTextBox.Text ?? string.Empty);
            StatusText.Text = "Copied";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Copy failed: {exception.Message}";
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var extension = _format == OutputFormat.Json ? ".json" : ".jsonl";
        var dialog = new SaveFileDialog
        {
            FileName = "dataforge" + extension,
            DefaultExt = extension,
            Filter = _format == OutputFormat.Json ? "JSON (*.json)|*.json" : "JSON Lines (*.jsonl)|*.jsonl"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            File.WriteAllText(
                dialog.FileName,
                OutputTextBox.Text ?? string.Empty,
                new System.Text.UTF8Encoding(false));
            StatusText.Text = $"Saved: {dialog.FileName}";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Save failed: {exception.Message}";
        }
    }

    private void AutoDirection()
    {
        InputTextBox.FlowDirection = DirectionDetector.ContainsArabic(InputTextBox.Text)
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    private void Render()
    {
        OutputTextBox.Text = DeterministicConverter.Convert(InputTextBox.Text, _format);
        StatusText.Text = $"{_format.ToString().ToUpperInvariant()} | {InputTextBox.Text.Length} chars";
    }

    private enum DirectionMode
    {
        Auto,
        Rtl,
        Ltr
    }
}
