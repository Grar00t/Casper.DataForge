using Casper.DataForge.CrossPlatform.Engine;
using Casper.DataForge.CrossPlatform.Data;
using Casper.DataForge.CrossPlatform;

string normalizedUrl = SourceTextNormalizer.NormalizeUrl(
    "//duckduckgo.com/l/?uddg=https%3A%2F%2Fn8n.io%2F&amp;amp;rut=abc");

string expectedUrl =
    "https://duckduckgo.com/l/?uddg=https%3A%2F%2Fn8n.io%2F&rut=abc";

bool urlPass = string.Equals(
    normalizedUrl,
    expectedUrl,
    StringComparison.Ordinal);

Console.WriteLine($"NormalizedUrl={normalizedUrl}");
Console.WriteLine($"URL_NORMALIZATION_PASS={urlPass}");

if (!urlPass)
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
    CasperResponse result = await client.QueryAsync("who is n8n");

    Console.WriteLine($"ExitCode={result.ExitCode}");
    Console.WriteLine($"Query={result.Query}");
    Console.WriteLine($"Confidence={result.Confidence}");
    Console.WriteLine($"Sources={result.SourceCount}");
    Console.WriteLine($"Proof={result.Proof}");
    Console.WriteLine($"Error={result.Error}");

    bool valid =
        result.ExitCode == 0 &&
        result.Query == "who is n8n" &&
        result.SourceCount > 0 &&
        !string.IsNullOrWhiteSpace(result.Proof);

    Console.WriteLine($"CLIENT_SMOKE_PASS={valid}");

    using var database = new LocalDatabase();
    bool databasePass = database.IsReady;
    if (databasePass)
    {
        KnowledgeBaseCatalog catalog = KnowledgeBaseCatalog.LoadDefault();
        database.SeedKnowledgeBase(catalog);

        KnowledgeGraph graph = KnowledgeGraph.FromCasperResponse(
            "who is n8n",
            result);
        database.SaveSession("who is n8n", result, graph);
        databasePass =
            database.GetRecentSessions(1).Count > 0 &&
            database.GetKnowledgeNodeCount() == catalog.Nodes.Count;
    }

    Console.WriteLine($"DatabasePath={database.DatabasePath}");
    Console.WriteLine($"DATABASE_SCHEMA_PASS={databasePass}");

    Environment.ExitCode = valid && databasePass ? 0 : 3;
}
catch (Exception exception)
{
    Console.WriteLine("CLIENT_SMOKE_PASS=False");
    Console.WriteLine(exception);
    Environment.ExitCode = 4;
}
