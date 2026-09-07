using System.Text.Json;
using Casper.DataForge.Core;

const string source =
    "قبل ```x < y && z``` وبعد $$a+b$$";

IReadOnlyList<Segment> segments =
    DeterministicConverter.Split(source);

bool segmentShapePass =
    segments.Count == 4 &&
    segments
        .Select(
            (segment, index) =>
                segment.Index == index &&
                segment.Content ==
                source.Substring(
                    segment.Start,
                    segment.Length))
        .All(static value => value);

string json =
    DeterministicConverter.Convert(
        source,
        OutputFormat.Json);

using JsonDocument document =
    JsonDocument.Parse(json);

bool jsonPass =
    document.RootElement
        .GetProperty("Original")
        .GetString() == source &&
    document.RootElement
        .GetProperty("Segments")
        .GetArrayLength() == segments.Count &&
    json.Contains(
        "< y && z",
        StringComparison.Ordinal);

string jsonl =
    DeterministicConverter.Convert(
        source,
        OutputFormat.Jsonl);

bool jsonlPass =
    jsonl
        .Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries)
        .Length == segments.Count;

string jsonDigest =
    DeterministicConverter.ComputeOutputSha256(json);
string jsonDigestAgain =
    DeterministicConverter.ComputeOutputSha256(json);
string jsonlDigest =
    DeterministicConverter.ComputeOutputSha256(jsonl);

bool outputDigestPass =
    jsonDigest.Length == 64 &&
    string.Equals(jsonDigest, jsonDigestAgain, StringComparison.Ordinal) &&
    !string.Equals(jsonDigest, jsonlDigest, StringComparison.Ordinal);

bool arabicPass =
    DirectionDetector.ContainsArabic(source);

bool arabicExtendedPass =
    DirectionDetector.ContainsArabic("\u0870") &&
    DirectionDetector.ContainsArabic("\U0001EE00");

bool nullAndEmptyPass =
    DeterministicConverter.Split(null).Count == 0 &&
    DeterministicConverter.Convert(
        null,
        OutputFormat.Jsonl) == string.Empty &&
    !DirectionDetector.ContainsArabic(null) &&
    !DirectionDetector.ContainsArabic(string.Empty);

bool pass =
    segmentShapePass &&
    jsonPass &&
    jsonlPass &&
    outputDigestPass &&
    arabicPass &&
    arabicExtendedPass &&
    nullAndEmptyPass;

Console.WriteLine(
    $"SEGMENT_SHAPE_PASS={segmentShapePass}");
Console.WriteLine(
    $"JSON_PASS={jsonPass}");
Console.WriteLine(
    $"JSONL_PASS={jsonlPass}");
Console.WriteLine(
    $"OUTPUT_DIGEST_PASS={outputDigestPass}");
Console.WriteLine(
    $"OUTPUT_SHA256={jsonDigest}");
Console.WriteLine(
    $"ARABIC_DIRECTION_PASS={arabicPass}");
Console.WriteLine(
    $"ARABIC_EXTENDED_PASS={arabicExtendedPass}");
Console.WriteLine(
    $"NULL_EMPTY_PASS={nullAndEmptyPass}");
Console.WriteLine(
    $"CORE_SMOKE_PASS={pass}");

Environment.ExitCode = pass ? 0 : 1;
