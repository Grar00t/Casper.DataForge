using System;
using Casper.DataForge.CrossPlatform.Engine;

string encodedUrl =
    "//duckduckgo.com/l/?uddg=https%3A%2F%2Fn8n.io%2F&amp;amp;rut=abc";

string normalizedUrl =
    SourceTextNormalizer.NormalizeUrl(encodedUrl);

string expectedUrl =
    "https:" + "//duckduckgo.com/l/?uddg=https%3A%2F%2Fn8n.io%2F" + "&" + "rut=abc";

bool urlNormalizationPass =
    string.Equals(
        normalizedUrl,
        expectedUrl,
        StringComparison.Ordinal);

Console.WriteLine($"NormalizedUrl={normalizedUrl}");
Console.WriteLine($"ExpectedUrl={expectedUrl}");
Console.WriteLine($"URL_NORMALIZATION_PASS={urlNormalizationPass}");

if (!urlNormalizationPass)
{
    Environment.ExitCode = 5;
    return;
}

var client = new CasperEngineClient();

Console.WriteLine($"EnginePath={client.ExecutablePath}");
Console.WriteLine($"EngineAvailable={client.IsAvailable}");

if (!client.IsAvailable)
{
    Environment.ExitCode = 2;
    return;
}

try
{
    CasperResponse result =
        await client.QueryAsync("who is n8n");

    Console.WriteLine($"ExitCode={result.ExitCode}");
    Console.WriteLine($"Query={result.Query}");
    Console.WriteLine($"Confidence={result.Confidence}");
    Console.WriteLine($"Sources={result.SourceCount}");
    Console.WriteLine($"Proof={result.Proof}");
    Console.WriteLine($"Error={result.Error}");

    bool clientPass =
        result.ExitCode == 0 &&
        string.Equals(
            result.Query,
            "who is n8n",
            StringComparison.Ordinal) &&
        result.SourceCount > 0 &&
        !string.IsNullOrWhiteSpace(result.Proof);

    Console.WriteLine($"CLIENT_SMOKE_PASS={clientPass}");
    Environment.ExitCode = clientPass ? 0 : 3;
}
catch (Exception exception)
{
    Console.WriteLine("CLIENT_SMOKE_PASS=False");
    Console.WriteLine(exception);
    Environment.ExitCode = 4;
}
