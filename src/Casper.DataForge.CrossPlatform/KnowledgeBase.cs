using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Casper.DataForge.CrossPlatform;

public sealed record KnowledgeNodeSeed(
    string Id,
    string NameEn,
    string NameAr,
    string Domain,
    string SummaryEn,
    string SummaryAr);

public sealed record KnowledgeEdgeSeed(
    string From,
    string To,
    string Relation);

public sealed record KnowledgeBaseCatalog(
    int SchemaVersion,
    string CatalogId,
    IReadOnlyList<KnowledgeNodeSeed> Nodes,
    IReadOnlyList<KnowledgeEdgeSeed> Edges)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = false
    };

    public static KnowledgeBaseCatalog LoadDefault()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "KnowledgeBase", "knowledge.seed.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("Knowledge base seed was not found.", path);

        KnowledgeBaseCatalog? catalog = JsonSerializer.Deserialize<KnowledgeBaseCatalog>(File.ReadAllText(path), JsonOptions);
        if (catalog is null)
            throw new InvalidDataException("Knowledge base seed is empty.");

        catalog.Validate();
        return catalog;
    }

    public KnowledgeGraph ToGraph()
    {
        var nodes = Nodes.Select(node => new GraphNode(node.Id, $"{node.NameEn} / {node.NameAr}", "knowledge")).ToList();
        var edges = Edges.Select(edge => new GraphEdge(edge.From, edge.To, edge.Relation)).ToList();
        return new KnowledgeGraph(nodes, edges);
    }

    private void Validate()
    {
        if (SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported knowledge base schema version: {SchemaVersion}.");
        if (string.IsNullOrWhiteSpace(CatalogId))
            throw new InvalidDataException("Knowledge base catalog id is required.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (KnowledgeNodeSeed node in Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id) ||
                string.IsNullOrWhiteSpace(node.NameEn) ||
                string.IsNullOrWhiteSpace(node.NameAr) ||
                string.IsNullOrWhiteSpace(node.Domain) ||
                string.IsNullOrWhiteSpace(node.SummaryEn) ||
                string.IsNullOrWhiteSpace(node.SummaryAr))
                throw new InvalidDataException($"Knowledge node '{node.Id}' is incomplete.");

            if (!ids.Add(node.Id))
                throw new InvalidDataException($"Duplicate knowledge node id: {node.Id}.");
        }

        var edgeKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (KnowledgeEdgeSeed edge in Edges)
        {
            if (!ids.Contains(edge.From) || !ids.Contains(edge.To))
                throw new InvalidDataException($"Knowledge edge references an unknown node: {edge.From} -> {edge.To}.");
            if (edge.From == edge.To || string.IsNullOrWhiteSpace(edge.Relation))
                throw new InvalidDataException("Knowledge edges must be directed and labelled.");

            string key = $"{edge.From}\u001F{edge.To}\u001F{edge.Relation}";
            if (!edgeKeys.Add(key))
                throw new InvalidDataException($"Duplicate knowledge edge: {key}.");
        }
    }
}
