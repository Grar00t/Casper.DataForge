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
    public static KnowledgeGraph FromCasperResponse(
        string query,
        CasperResponse response)
    {
        var nodes = new List<GraphNode>
        {
            new("query", query.Trim(), "query")
        };
        var edges = new List<GraphEdge>();
        var usedIds = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < response.Sources.Count; index++)
        {
            CasperSource source = response.Sources[index];
            string identity = SourceTextNormalizer.NormalizeUrl(source.Url);

            if (identity.Length == 0)
                identity = source.Title?.Trim() ?? $"Source {index + 1}";

            string id = "source-" + (index + 1);
            while (!usedIds.Add(id))
                id = $"source-{index + 1}-{usedIds.Count}";

            string label = string.IsNullOrWhiteSpace(source.Title)
                ? identity
                : SourceTextNormalizer.DecodeHtml(source.Title).Trim();

            nodes.Add(new GraphNode(id, label, "source"));
            edges.Add(new GraphEdge(
                "query",
                id,
                $"score {source.Score.ToString("0.000", CultureInfo.InvariantCulture)}"));
        }

        return new KnowledgeGraph(nodes, edges);
    }
}
