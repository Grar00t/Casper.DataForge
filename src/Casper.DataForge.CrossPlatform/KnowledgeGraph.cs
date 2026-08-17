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
        ArgumentNullException.ThrowIfNull(response);

        string normalizedQuery = query?.Trim() ?? string.Empty;
        var nodes = new List<GraphNode>
        {
            new("query", normalizedQuery, "query")
        };
        var edges = new List<GraphEdge>();
        var usedIds = new HashSet<string>(StringComparer.Ordinal) { "query" };
        var identityToNodeId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < response.Sources.Count; index++)
        {
            CasperSource source = response.Sources[index];
            string identity = SourceTextNormalizer.NormalizeUrl(source.Url);
            if (identity.Length == 0)
                identity = SourceTextNormalizer.DecodeHtml(source.Title).Trim();
            if (identity.Length == 0)
                identity = $"source-{index + 1}";

            if (!identityToNodeId.TryGetValue(identity, out string? id))
            {
                id = MakeUniqueId("source", index + 1, usedIds);
                identityToNodeId[identity] = id;
                string label = string.IsNullOrWhiteSpace(source.Title)
                    ? identity
                    : SourceTextNormalizer.DecodeHtml(source.Title).Trim();
                nodes.Add(new GraphNode(id, label, "source"));
            }

            double score = source.Score;
            if (double.IsNaN(score) || double.IsInfinity(score))
                score = 0.0;

            string relation = $"score {score.ToString("0.000", CultureInfo.InvariantCulture)}";
            edges.Add(new GraphEdge("query", id, relation));
        }

        return new KnowledgeGraph(nodes, edges);
    }

    public void Validate()
    {
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (GraphNode node in Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id) || !nodeIds.Add(node.Id))
                throw new InvalidDataException($"Invalid or duplicate graph node id: {node.Id}");
            if (string.IsNullOrWhiteSpace(node.Kind))
                throw new InvalidDataException($"Graph node kind is required: {node.Id}");
        }

        foreach (GraphEdge edge in Edges)
        {
            if (!nodeIds.Contains(edge.From) || !nodeIds.Contains(edge.To))
                throw new InvalidDataException($"Graph edge references an unknown node: {edge.From} -> {edge.To}");
            if (string.IsNullOrWhiteSpace(edge.Label))
                throw new InvalidDataException("Graph edge label is required.");
        }
    }

    private static string MakeUniqueId(string prefix, int ordinal, HashSet<string> usedIds)
    {
        string id = $"{prefix}-{ordinal}";
        int suffix = 2;
        while (!usedIds.Add(id))
            id = $"{prefix}-{ordinal}-{suffix++}";
        return id;
    }
}
