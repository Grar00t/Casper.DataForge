using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using Casper.DataForge.CrossPlatform.Data;

namespace Casper.DataForge.CrossPlatform;

public partial class HistoryWindow : Window
{
    public HistoryWindow()
    {
        InitializeComponent();
    }

    public void Load(IReadOnlyList<QuerySessionSummary> sessions)
    {
        SessionList.Items.Clear();

        foreach (QuerySessionSummary session in sessions)
        {
            SessionList.Items.Add(new TextBlock
            {
                Text = Format(session),
                Foreground = new SolidColorBrush(Color.Parse("#E8EAF0")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(8)
            });
        }

        if (sessions.Count == 0)
        {
            SessionList.Items.Add(new TextBlock
            {
                Text = "No saved queries yet. / لا توجد استعلامات محفوظة بعد.",
                Foreground = new SolidColorBrush(Color.Parse("#9FA8B8")),
                Margin = new Avalonia.Thickness(8)
            });
        }
    }

    private static string Format(QuerySessionSummary session)
    {
        string query = session.Query.ReplaceLineEndings(" ").Trim();
        if (query.Length > 180)
            query = query[..180] + "…";

        return $"{session.CreatedUtc.LocalDateTime:g}  ·  {session.SourceCount} sources  ·  " +
               $"confidence {session.Confidence:0.000}  ·  exit {session.ExitCode}\n{query}";
    }
}
