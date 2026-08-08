using System.Text.Json;
using Casper.DataForge.Core;

const string source = "قبل ```x < y && z``` وبعد $$a+b$$";
IReadOnlyList<Segment> segments = DeterministicConverter.Split(source);

bool segmentShapePass =
    segments.Count == 4 &&
    segments.Select((segment, index) =>
        segment.Index == index &&
        segment.Content == source.Substring(segment.Start, segment.Length))
        .All(static value => value);

string json = DeterministicConverter.Convert(source, OutputFormat.Json);
using JsonDocument document = JsonDocument.Parse(json);

bool jsonPass =
    document.RootElement.GetProperty("Original").GetString() == source &&
    document.RootElement.GetProperty("Segments").GetArrayLength() == segments.Count &&
    json.Contains("< y && z", StringComparison.Ordinal);

string jsonl = DeterministicConverter.Convert(source, OutputFormat.Jsonl);
bool jsonlPass = jsonl.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length == segments.Count;
bool arabicPass = DirectionDetector.ContainsArabic(source);

bool pass = segmentShapePass && jsonPass && jsonlPass && arabicPass;
Console.WriteLine($"SEGMENT_SHAPE_PASS={segmentShapePass}");
Console.WriteLine($"JSON_PASS={jsonPass}");
Console.WriteLine($"JSONL_PASS={jsonlPass}");
Console.WriteLine($"ARABIC_DIRECTION_PASS={arabicPass}");
Console.WriteLine($"CORE_SMOKE_PASS={pass}");
Environment.ExitCode = pass ? 0 : 1;
