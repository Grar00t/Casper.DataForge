using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Casper.DataForge.CrossPlatform.Engine;

namespace Casper.DataForge.CrossPlatform.Data;

public sealed class LocalDatabase : IDisposable
{
    private const int SchemaVersion = 2;
    private readonly SqliteConnection _connection;
    private readonly object _gate = new();

    public LocalDatabase()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Casper.DataForge");

        DatabasePath = Path.Combine(root, "casper-dataforge.db");
        _connection = new SqliteConnection($"Data Source={DatabasePath};Cache=Shared");

        try
        {
            Directory.CreateDirectory(root);
            _connection.Open();
            ApplySchema();
            IsReady = true;
        }
        catch (Exception exception)
        {
            Error = exception.Message;
            IsReady = false;
        }
    }

    public string DatabasePath { get; }
    public bool IsReady { get; }
    public string? Error { get; }

    public IReadOnlyList<QuerySessionSummary> GetRecentSessions(int limit = 25)
    {
        if (!IsReady)
            return Array.Empty<QuerySessionSummary>();

        lock (_gate)
        {
            limit = Math.Clamp(limit, 1, 100);
            using var command = _connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, query, created_utc, exit_code, confidence, source_count
            FROM query_sessions
            ORDER BY created_utc DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        using SqliteDataReader reader = command.ExecuteReader();
        var sessions = new List<QuerySessionSummary>();

        while (reader.Read())
        {
            DateTimeOffset created = DateTimeOffset.TryParse(
                reader.GetString(2),
                out DateTimeOffset parsed)
                ? parsed
                : DateTimeOffset.MinValue;

            sessions.Add(new QuerySessionSummary(
                reader.GetString(0),
                reader.GetString(1),
                created,
                reader.GetInt32(3),
                reader.GetDouble(4),
                reader.GetInt32(5)));
        }

            return sessions;
        }
    }

    public void SeedKnowledgeBase(KnowledgeBaseCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (!IsReady)
            return;

        lock (_gate)
        {
            using var transaction = _connection.BeginTransaction();
            string updatedUtc = DateTimeOffset.UtcNow.ToString("O");

        foreach (KnowledgeNodeSeed node in catalog.Nodes)
        {
            Execute(
                transaction,
                """
                INSERT INTO knowledge_nodes
                    (id, name_en, name_ar, domain, summary_en, summary_ar, updated_utc)
                VALUES
                    ($id, $name_en, $name_ar, $domain, $summary_en, $summary_ar, $updated)
                ON CONFLICT(id) DO UPDATE SET
                    name_en = excluded.name_en,
                    name_ar = excluded.name_ar,
                    domain = excluded.domain,
                    summary_en = excluded.summary_en,
                    summary_ar = excluded.summary_ar,
                    updated_utc = excluded.updated_utc;
                """,
                ("$id", node.Id),
                ("$name_en", node.NameEn),
                ("$name_ar", node.NameAr),
                ("$domain", node.Domain),
                ("$summary_en", node.SummaryEn),
                ("$summary_ar", node.SummaryAr),
                ("$updated", updatedUtc));
        }

        foreach (KnowledgeEdgeSeed edge in catalog.Edges)
        {
            Execute(
                transaction,
                """
                INSERT OR IGNORE INTO knowledge_edges (from_id, to_id, relation)
                VALUES ($from, $to, $relation);
                """,
                ("$from", edge.From),
                ("$to", edge.To),
                ("$relation", edge.Relation));
        }

            transaction.Commit();
        }
    }

    public int GetKnowledgeNodeCount()
    {
        if (!IsReady)
            return 0;

        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM knowledge_nodes;";
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    public void SaveSession(string query, CasperResponse response, KnowledgeGraph graph)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty.", nameof(query));

        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(graph);

        if (!IsReady)
            return;

        lock (_gate)
        {
            using var transaction = _connection.BeginTransaction();
            string sessionId = Guid.NewGuid().ToString("N");

        Execute(
            transaction,
            """
            INSERT INTO query_sessions
                (id, query, created_utc, exit_code, confidence, elapsed_ms, proof, source_count)
            VALUES
                ($id, $query, $created, $exit, $confidence, $elapsed, $proof, $source_count);
            """,
            ("$id", sessionId),
            ("$query", query),
            ("$created", DateTimeOffset.UtcNow.ToString("O")),
            ("$exit", response.ExitCode),
            ("$confidence", response.Confidence),
            ("$elapsed", response.ElapsedMilliseconds),
            ("$proof", response.Proof),
            ("$source_count", response.SourceCount));

        for (var index = 0; index < response.Sources.Count; index++)
        {
            CasperSource source = response.Sources[index];
            Execute(
                transaction,
                """
                INSERT INTO sources
                    (session_id, source_number, score, sha256, title, url, snippet)
                VALUES
                    ($session, $number, $score, $sha256, $title, $url, $snippet);
                """,
                ("$session", sessionId),
                ("$number", source.Number == 0 ? index + 1 : source.Number),
                ("$score", source.Score),
                ("$sha256", source.Sha256),
                ("$title", SourceTextNormalizer.DecodeHtml(source.Title)),
                ("$url", SourceTextNormalizer.NormalizeUrl(source.Url)),
                ("$snippet", SourceTextNormalizer.DecodeHtml(source.Snippet)));
        }

        foreach (GraphNode node in graph.Nodes)
        {
            Execute(
                transaction,
                """
                INSERT INTO graph_nodes (session_id, node_key, kind, label)
                VALUES ($session, $key, $kind, $label);
                """,
                ("$session", sessionId),
                ("$key", node.Id),
                ("$kind", node.Kind),
                ("$label", node.Label));
        }

        foreach (GraphEdge edge in graph.Edges)
        {
            Execute(
                transaction,
                """
                INSERT INTO graph_edges (session_id, from_key, to_key, label)
                VALUES ($session, $from, $to, $label);
                """,
                ("$session", sessionId),
                ("$from", edge.From),
                ("$to", edge.To),
                ("$label", edge.Label));
        }

            transaction.Commit();
        }
    }

    public Task SaveSessionAsync(
        string query,
        CasperResponse response,
        KnowledgeGraph graph,
        CancellationToken cancellationToken = default) =>
        Task.Run(
            () => SaveSession(query, response, graph),
            cancellationToken);

    public void Dispose()
    {
        lock (_gate)
            _connection.Dispose();
    }

    private void ApplySchema()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
        command.ExecuteNonQuery();

        command.CommandText = "CREATE TABLE IF NOT EXISTS schema_migrations (version INTEGER PRIMARY KEY, applied_utc TEXT NOT NULL);";
        command.ExecuteNonQuery();

        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";
        var currentVersion = Convert.ToInt32(command.ExecuteScalar());

        if (currentVersion > SchemaVersion)
        {
            throw new InvalidOperationException(
                $"Database schema version {currentVersion} is newer than supported version {SchemaVersion}.");
        }

        if (currentVersion >= SchemaVersion)
            return;

        using var transaction = _connection.BeginTransaction();
        if (currentVersion < 1)
        {
            Execute(
                transaction,
                """
                CREATE TABLE IF NOT EXISTS query_sessions (
                    id TEXT PRIMARY KEY,
                    query TEXT NOT NULL,
                    created_utc TEXT NOT NULL,
                    exit_code INTEGER NOT NULL,
                    confidence REAL NOT NULL,
                    elapsed_ms INTEGER NOT NULL,
                    proof TEXT,
                    source_count INTEGER NOT NULL CHECK (source_count >= 0)
                );
                CREATE TABLE IF NOT EXISTS sources (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    session_id TEXT NOT NULL REFERENCES query_sessions(id) ON DELETE CASCADE,
                    source_number INTEGER NOT NULL CHECK (source_number > 0),
                    score REAL NOT NULL,
                    sha256 TEXT,
                    title TEXT,
                    url TEXT,
                    snippet TEXT,
                    UNIQUE(session_id, source_number)
                );
                CREATE TABLE IF NOT EXISTS graph_nodes (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    session_id TEXT NOT NULL REFERENCES query_sessions(id) ON DELETE CASCADE,
                    node_key TEXT NOT NULL,
                    kind TEXT NOT NULL,
                    label TEXT NOT NULL,
                    UNIQUE(session_id, node_key)
                );
                CREATE TABLE IF NOT EXISTS graph_edges (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    session_id TEXT NOT NULL REFERENCES query_sessions(id) ON DELETE CASCADE,
                    from_key TEXT NOT NULL,
                    to_key TEXT NOT NULL,
                    label TEXT NOT NULL,
                    UNIQUE(session_id, from_key, to_key, label)
                );
                CREATE INDEX IF NOT EXISTS ix_sources_session ON sources(session_id);
                CREATE INDEX IF NOT EXISTS ix_graph_nodes_session ON graph_nodes(session_id);
                CREATE INDEX IF NOT EXISTS ix_graph_edges_session ON graph_edges(session_id);
                """);

            RecordMigration(transaction, 1);
        }

        if (currentVersion < 2)
        {
            Execute(
                transaction,
                """
                CREATE TABLE IF NOT EXISTS knowledge_nodes (
                    id TEXT PRIMARY KEY,
                    name_en TEXT NOT NULL,
                    name_ar TEXT NOT NULL,
                    domain TEXT NOT NULL,
                    summary_en TEXT NOT NULL,
                    summary_ar TEXT NOT NULL,
                    updated_utc TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS knowledge_edges (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    from_id TEXT NOT NULL REFERENCES knowledge_nodes(id) ON DELETE CASCADE,
                    to_id TEXT NOT NULL REFERENCES knowledge_nodes(id) ON DELETE CASCADE,
                    relation TEXT NOT NULL,
                    UNIQUE(from_id, to_id, relation)
                );
                CREATE INDEX IF NOT EXISTS ix_knowledge_nodes_domain ON knowledge_nodes(domain);
                CREATE INDEX IF NOT EXISTS ix_knowledge_edges_from ON knowledge_edges(from_id);
                CREATE INDEX IF NOT EXISTS ix_knowledge_edges_to ON knowledge_edges(to_id);
                """);

            RecordMigration(transaction, 2);
        }

        transaction.Commit();
    }

    private static void RecordMigration(SqliteTransaction transaction, int version)
    {
        Execute(
            transaction,
            "INSERT INTO schema_migrations(version, applied_utc) VALUES ($version, $applied);",
            ("$version", version),
            ("$applied", DateTimeOffset.UtcNow.ToString("O")));
    }

    private static void Execute(
        SqliteTransaction transaction,
        string sql,
        params (string Name, object? Value)[] values)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        foreach ((string name, object? value) in values)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);

        command.ExecuteNonQuery();
    }
}

public sealed record QuerySessionSummary(
    string Id,
    string Query,
    DateTimeOffset CreatedUtc,
    int ExitCode,
    double Confidence,
    int SourceCount);
