# Local database schema

Casper.DataForge stores local query history in SQLite at:

```text
%LOCALAPPDATA%/Casper.DataForge/casper-dataforge.db
```

The database is application-owned and does not require a server, account, or network connection.

Knowledge-base seed data is stored under `src/Casper.DataForge.CrossPlatform/Assets/KnowledgeBase/knowledge.seed.json`. Its contract is documented by `knowledge.schema.json`; runtime loading also validates required fields, duplicate IDs, and edge endpoints before writing to SQLite.

## Tables

- `schema_migrations`: versioned schema history.
- `query_sessions`: one row per Casper query and response.
- `sources`: evidence returned for a query session.
- `graph_nodes`: nodes rendered in the evidence graph.
- `graph_edges`: directed relationships between graph nodes.
- `knowledge_nodes`: curated bilingual knowledge-base concepts.
- `knowledge_edges`: directed relationships in the knowledge base.

## Guarantees

- Foreign keys are enabled and child records cascade with their session.
- Query values are parameterized; user text is never interpolated into SQL.
- `UNIQUE` constraints prevent duplicate source numbers, nodes, and edges per session.
- Indexes cover session lookups for sources and graph data.
- WAL mode improves read/write behavior while the UI is open.
- Schema changes are recorded in `schema_migrations` and must increment the schema version.
- Knowledge-base seeding is idempotent for nodes and edges and runs inside a transaction.
