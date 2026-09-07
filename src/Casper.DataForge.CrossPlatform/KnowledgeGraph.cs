using System;
using System.Collections.Generic;
using System.Globalization;
using Casper.DataForge.CrossPlatform.Engine;

namespace Casper.DataForge.CrossPlatform;

public sealed record GraphNode(string Id, string Label, string Kind);
public sealed record GraphEdge(string From, string To, string Label);

public sealed record KnowledgeGraph(
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges)
{
    public static KnowledgeGraph FromCasperResponse(string query, CasperResponse response)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty.", nameof(query));
        ArgumentNullException.ThrowIfNull(response);

        string normalizedQuery = query.Trim();
        IReadOnlyList<CasperSource> sources = response.Sources ?? Array.Empty<CasperSource>();

        var nodes = new List<GraphNode>
        {
            new("query", normalizedQuery, "query")
        };
        var edges = new List<GraphEdge>();
        var usedIds = new HashSet<string>(StringComparer.Ordinal) { "query" };
        var identityToEntry =
            new Dictionary<string, (string NodeId, int EdgeIndex, double Score)>(StringComparer.Ordinal);

        for (var index = 0; index < sources.Count; index++)
        {
            CasperSource source = sources[index];
            string normalizedUrl = SourceTextNormalizer.NormalizeUrl(source.Url);
            string normalizedTitle = SourceTextNormalizer.DecodeHtml(source.Title).Trim();
            string identity = BuildIdentity(normalizedUrl, normalizedTitle, index);

            double score = NormalizeScore(source.Score);

            if (identityToEntry.TryGetValue(identity, out var existing))
            {
                if (score > existing.Score)
                {
                    edges[existing.EdgeIndex] = new GraphEdge(
                        "query",
                        existing.NodeId,
                        FormatScore(score));

                    identityToEntry[identity] =
                        (existing.NodeId, existing.EdgeIndex, score);
                }

                continue;
            }

            string id = MakeUniqueId("source", index + 1, usedIds);
            string label = normalizedTitle.Length > 0
                ? normalizedTitle
                : normalizedUrl.Length > 0
                    ? normalizedUrl
                    : $"Source {index + 1}";

            nodes.Add(new GraphNode(id, label, "source"));

            int edgeIndex = edges.Count;
            edges.Add(new GraphEdge("query", id, FormatScore(score)));
            identityToEntry[identity] = (id, edgeIndex, score);
        }

        return new KnowledgeGraph(nodes, edges);
    }

    public void Validate()
    {
        if (Nodes is null)
            throw new InvalidDataException("Graph node collection cannot be null.");
        if (Edges is null)
            throw new InvalidDataException("Graph edge collection cannot be null.");

        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (GraphNode node in Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id) || !nodeIds.Add(node.Id))
                throw new InvalidDataException($"Invalid or duplicate graph node id: {node.Id}");
            if (string.IsNullOrWhiteSpace(node.Kind))
                throw new InvalidDataException($"Graph node kind is required: {node.Id}");
            if (string.IsNullOrWhiteSpace(node.Label))
                throw new InvalidDataException($"Graph node label is required: {node.Id}");
        }

        var edgeKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (GraphEdge edge in Edges)
        {
            if (!nodeIds.Contains(edge.From) || !nodeIds.Contains(edge.To))
                throw new InvalidDataException(
                    $"Graph edge references an unknown node: {edge.From} -> {edge.To}");
            if (string.IsNullOrWhiteSpace(edge.Label))
                throw new InvalidDataException("Graph edge label is required.");

            string key = $"{edge.From}\u001F{edge.To}\u001F{edge.Label}";
            if (!edgeKeys.Add(key))
                throw new InvalidDataException(
                    $"Duplicate graph edge: {edge.From} -> {edge.To} ({edge.Label}).");
        }
    }

    private static string BuildIdentity(
        string normalizedUrl,
        string normalizedTitle,
        int index)
    {
        if (normalizedUrl.Length > 0)
            return $"url:{normalizedUrl}";
        if (normalizedTitle.Length > 0)
            return $"title:{normalizedTitle.ToUpperInvariant()}";
        return $"ordinal:{index + 1}";
    }

    private static double NormalizeScore(double score)
    {
        if (double.IsNaN(score) || double.IsInfinity(score))
            return 0.0;
        return score;
    }

    private static string FormatScore(double score) =>
        $"score {score.ToString("0.000", CultureInfo.InvariantCulture)}";

    private static string MakeUniqueId(
        string prefix,
        int ordinal,
        HashSet<string> usedIds)
    {
        string id = $"{prefix}-{ordinal}";
        int suffix = 2;

        while (!usedIds.Add(id))
            id = $"{prefix}-{ordinal}-{suffix++}";

        return id;
    }
}
