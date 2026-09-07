using Casper.DataForge.CrossPlatform;
using Casper.DataForge.CrossPlatform.Data;
using Casper.DataForge.CrossPlatform.Engine;

const string query = "duplicate source graph";

IReadOnlyList<CasperSource> sources =
[
    new CasperSource
    {
        Number = 1,
        Score = 0.60,
        Sha256 = new string('A', 64),
        Title = "Example",
        Url = "https://example.com/path",
        Snippet = "first"
    },
    new CasperSource
    {
        Number = 2,
        Score = 0.90,
        Sha256 = new string('B', 64),
        Title = "Example duplicate",
        Url = "https://example.com/path",
        Snippet = "second"
    }
];

var response = new CasperResponse
{
    Query = query,
    Answer = "ok",
    Confidence = 0.8,
    ElapsedMilliseconds = 2,
    Proof = new string('C', 64),
    SourceCount = sources.Count,
    Sources = sources,
    ExitCode = 0
};

CasperEngineClient.ValidateResponse(query, response);

KnowledgeGraph graph =
    KnowledgeGraph.FromCasperResponse(query, response);

graph.Validate();

bool graphDedupPass =
    graph.Nodes.Count == 2 &&
    graph.Edges.Count == 1 &&
    graph.Edges[0].Label == "score 0.900";

Console.WriteLine($"GRAPH_DEDUP_PASS={graphDedupPass}");

if (!graphDedupPass)
{
    Environment.ExitCode = 1;
    return;
}

string tempDirectory =
    Path.Combine(
        Path.GetTempPath(),
        "Casper.DataForge.Persistence.Smoke",
        Guid.NewGuid().ToString("N"));

Directory.CreateDirectory(tempDirectory);
string databasePath =
    Path.Combine(tempDirectory, "smoke;database.db");

try
{
    using var database = new LocalDatabase(databasePath);

    bool databaseReadyPass =
        database.IsReady &&
        string.Equals(
            database.DatabasePath,
            Path.GetFullPath(databasePath),
            StringComparison.Ordinal);

    Console.WriteLine($"DATABASE_READY_PASS={databaseReadyPass}");

    if (!databaseReadyPass)
    {
        Console.WriteLine(database.Error);
        Environment.ExitCode = 2;
        return;
    }

    KnowledgeBaseCatalog catalog =
        KnowledgeBaseCatalog.LoadDefault();

    database.SeedKnowledgeBase(catalog);

    bool seedPass =
        database.GetKnowledgeNodeCount() == catalog.Nodes.Count;

    Console.WriteLine($"KNOWLEDGE_SEED_PASS={seedPass}");

    if (!seedPass)
    {
        Environment.ExitCode = 3;
        return;
    }

    KnowledgeNodeSeed firstNode = catalog.Nodes[0];
    var reducedCatalog = new KnowledgeBaseCatalog(
        catalog.SchemaVersion,
        catalog.CatalogId + ".smoke",
        [firstNode],
        Array.Empty<KnowledgeEdgeSeed>());

    database.SeedKnowledgeBase(reducedCatalog);

    bool reconciliationPass =
        database.GetKnowledgeNodeCount() == 1;

    Console.WriteLine($"KNOWLEDGE_RECONCILIATION_PASS={reconciliationPass}");

    if (!reconciliationPass)
    {
        Environment.ExitCode = 4;
        return;
    }

    database.SeedKnowledgeBase(catalog);
    database.SaveSession(query, response, graph);

    IReadOnlyList<QuerySessionSummary> sessions =
        database.GetRecentSessions(10);

    bool persistencePass =
        sessions.Count == 1 &&
        sessions[0].Query == query &&
        sessions[0].SourceCount == sources.Count &&
        sessions[0].ExitCode == 0;

    Console.WriteLine($"SESSION_PERSISTENCE_PASS={persistencePass}");

    if (!persistencePass)
    {
        Environment.ExitCode = 5;
        return;
    }

    bool duplicateSourceNumberRejected = false;
    try
    {
        var invalid = response with
        {
            Sources =
            [
                sources[0],
                sources[1] with { Number = 1 }
            ]
        };

        CasperEngineClient.ValidateResponse(query, invalid);
    }
    catch (InvalidDataException)
    {
        duplicateSourceNumberRejected = true;
    }

    Console.WriteLine(
        $"DUPLICATE_SOURCE_NUMBER_REJECTED_PASS={duplicateSourceNumberRejected}");

    bool implicitSourceNumberCollisionRejected = false;
    try
    {
        var invalid = response with
        {
            Sources =
            [
                sources[0] with { Number = 0 },
                sources[1] with { Number = 1 }
            ]
        };

        CasperEngineClient.ValidateResponse(query, invalid);
    }
    catch (InvalidDataException)
    {
        implicitSourceNumberCollisionRejected = true;
    }

    Console.WriteLine(
        $"IMPLICIT_SOURCE_NUMBER_COLLISION_REJECTED_PASS={implicitSourceNumberCollisionRejected}");

    bool pass =
        graphDedupPass &&
        databaseReadyPass &&
        seedPass &&
        reconciliationPass &&
        persistencePass &&
        duplicateSourceNumberRejected &&
        implicitSourceNumberCollisionRejected;

    Console.WriteLine($"PERSISTENCE_SMOKE_PASS={pass}");
    Environment.ExitCode = pass ? 0 : 6;
}
finally
{
    try
    {
        Directory.Delete(tempDirectory, recursive: true);
    }
    catch
    {
    }
}
