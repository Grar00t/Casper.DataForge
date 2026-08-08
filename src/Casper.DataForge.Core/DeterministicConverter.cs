using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Casper.DataForge.Core;

public enum OutputFormat
{
    Json,
    Jsonl
}

public sealed record Segment(
    int Index,
    string Type,
    string Content,
    int Start,
    int Length);

public sealed record ForgeDocument(
    string Original,
    IReadOnlyList<Segment> Segments);

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

    private static readonly (string Open, string Close, string Type)[] Markers =
    [
        ("```", "```", "code"),
        ("$$", "$$", "latex"),
        ("\\[", "\\]", "latex"),
        ("\\(", "\\)", "latex")
    ];

    public static string Convert(string? source, OutputFormat format)
    {
        source ??= string.Empty;
        IReadOnlyList<Segment> segments = Split(source);

        if (format == OutputFormat.Json)
            return JsonSerializer.Serialize(
                new ForgeDocument(source, segments),
                JsonOptions);

        var builder = new StringBuilder();
        foreach (Segment segment in segments)
        {
            builder.AppendLine(JsonSerializer.Serialize(
                segment,
                JsonLineOptions));
        }

        return builder.ToString().TrimEnd('\r', '\n');
    }

    public static IReadOnlyList<Segment> Split(string? source)
    {
        source ??= string.Empty;
        var result = new List<Segment>();
        var textStart = 0;
        var position = 0;

        while (position < source.Length)
        {
            (int Start, string Open, string Close, string Type) marker =
                FindNextMarker(source, position);

            if (marker.Start < 0)
                break;

            Add(result, "text", source, textStart, marker.Start - textStart);

            int end = FindClosingMarker(source, marker);
            Add(result, marker.Type, source, marker.Start, end - marker.Start);

            position = end;
            textStart = end;
        }

        Add(result, "text", source, textStart, source.Length - textStart);
        return result;
    }

    private static (int Start, string Open, string Close, string Type) FindNextMarker(
        string source,
        int from)
    {
        var best = (-1, string.Empty, string.Empty, string.Empty);

        foreach ((string open, string close, string type) in Markers)
        {
            int index = source.IndexOf(open, from, StringComparison.Ordinal);

            if (index >= 0 && (best.Item1 < 0 || index < best.Item1))
                best = (index, open, close, type);
        }

        return best;
    }

    private static int FindClosingMarker(
        string source,
        (int Start, string Open, string Close, string Type) marker)
    {
        int searchFrom = marker.Start + marker.Open.Length;
        int close = source.IndexOf(marker.Close, searchFrom, StringComparison.Ordinal);

        return close < 0
            ? source.Length
            : close + marker.Close.Length;
    }

    private static void Add(
        List<Segment> target,
        string type,
        string source,
        int start,
        int length)
    {
        if (length <= 0)
            return;

        target.Add(new Segment(
            target.Count,
            type,
            source.Substring(start, length),
            start,
            length));
    }
}

public static class DirectionDetector
{
    public static bool ContainsArabic(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        foreach (char character in value)
        {
            if (character is >= '\u0600' and <= '\u06FF' or
                >= '\u0750' and <= '\u077F' or
                >= '\u08A0' and <= '\u08FF' or
                >= '\uFB50' and <= '\uFDFF' or
                >= '\uFE70' and <= '\uFEFF')
            {
                return true;
            }
        }

        return false;
    }
}
