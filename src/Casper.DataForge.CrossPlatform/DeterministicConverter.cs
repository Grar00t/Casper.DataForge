using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Casper.DataForge.CrossPlatform;

public enum OutputFormat { Json, Jsonl }

public sealed record Segment(int Index, string Type, string Content, int Start, int Length);
public sealed record ForgeDocument(string Original, IReadOnlyList<Segment> Segments);

public static class DeterministicConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions JsonLineOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Convert(string source, OutputFormat format)
    {
        source ??= string.Empty;
        var segments = Split(source);
        if (format == OutputFormat.Json)
            return JsonSerializer.Serialize(new ForgeDocument(source, segments), JsonOptions);

        var builder = new StringBuilder();
        foreach (var segment in segments)
            builder.AppendLine(JsonSerializer.Serialize(segment, JsonLineOptions));
        return builder.ToString().TrimEnd('\r', '\n');
    }

    public static IReadOnlyList<Segment> Split(string source)
    {
        var result = new List<Segment>();
        var textStart = 0;
        var position = 0;

        while (position < source.Length)
        {
            var marker = FindNextMarker(source, position);
            if (marker.Start < 0) break;

            Add(result, "text", source, textStart, marker.Start - textStart);
            var end = FindClosingMarker(source, marker);
            Add(result, marker.Type, source, marker.Start, end - marker.Start);
            position = end;
            textStart = end;
        }

        Add(result, "text", source, textStart, source.Length - textStart);
        return result;
    }

    private static (int Start, string Open, string Close, string Type) FindNextMarker(string source, int from)
    {
        var markers = new[]
        {
            (Open: "```", Close: "```", Type: "code"),
            (Open: "$$", Close: "$$", Type: "latex"),
            (Open: "\\[", Close: "\\]", Type: "latex"),
            (Open: "\\(", Close: "\\)", Type: "latex")
        };

        var best = (-1, string.Empty, string.Empty, string.Empty);
        foreach (var marker in markers)
        {
            var index = source.IndexOf(marker.Open, from, StringComparison.Ordinal);
            if (index >= 0 && (best.Item1 < 0 || index < best.Item1))
                best = (index, marker.Open, marker.Close, marker.Type);
        }
        return best;
    }

    private static int FindClosingMarker(string source, (int Start, string Open, string Close, string Type) marker)
    {
        var searchFrom = marker.Start + marker.Open.Length;
        var close = source.IndexOf(marker.Close, searchFrom, StringComparison.Ordinal);
        return close < 0 ? source.Length : close + marker.Close.Length;
    }

    private static void Add(List<Segment> target, string type, string source, int start, int length)
    {
        if (length <= 0) return;
        target.Add(new Segment(target.Count, type, source.Substring(start, length), start, length));
    }
}

public static class DirectionDetector
{
    public static bool ContainsArabic(string value)
    {
        foreach (var c in value)
            if (c is >= '\u0600' and <= '\u06FF' or >= '\u0750' and <= '\u077F' or >= '\u08A0' and <= '\u08FF')
                return true;
        return false;
    }
}




