using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Microsoft.Win32;

namespace Casper.DataForge;

public partial class MainWindow : Window
{
    private OutputFormat _format = OutputFormat.Json;

    public MainWindow()
    {
        InitializeComponent();
        Render();
    }

    private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsInitialized) return;
        if (InputTextBox.FlowDirection == FlowDirection.LeftToRight ||
            InputTextBox.FlowDirection == FlowDirection.RightToLeft)
        {
            AutoDirection();
        }
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

    private void AutoDirection_Click(object sender, RoutedEventArgs e) => AutoDirection();
    private void RtlDirection_Click(object sender, RoutedEventArgs e) => InputTextBox.FlowDirection = FlowDirection.RightToLeft;
    private void LtrDirection_Click(object sender, RoutedEventArgs e) => InputTextBox.FlowDirection = FlowDirection.LeftToRight;

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(OutputTextBox.Text);
        StatusText.Text = "Copied";
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

        if (dialog.ShowDialog() != true) return;
        File.WriteAllText(dialog.FileName, OutputTextBox.Text, new System.Text.UTF8Encoding(false));
        StatusText.Text = $"Saved: {dialog.FileName}";
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
}
