# Casper.DataForge

Deterministic local text structuring for JSON and JSONL, with an optional Casper native-engine query path.

## Scope

The Forge converter:

- preserves the original input
- segments text, fenced code, and LaTeX markers
- emits deterministic JSON or JSONL
- tracks source offsets and lengths
- computes a SHA-256 digest for the exact rendered output
- runs locally without a network dependency
- does not generate, summarize, rewrite, or complete source text

The Casper query path is separate. It launches a native Casper executable through a JSON stdout protocol. Whether a query is offline depends on the Casper engine build and its retrieval configuration.

## Repository layout

```text
Casper.DataForge/
├── src/Casper.DataForge.Core/
│   └── DeterministicConverter.cs
├── src/Casper.DataForge.CrossPlatform/
│   ├── Assets/
│   ├── Data/
│   │   └── LocalDatabase.cs
│   ├── Engine/
│   │   ├── CasperEngineClient.cs
│   │   ├── SourceTextNormalizer.cs
│   │   └── bin/
│   ├── GraphWindow.axaml
│   ├── HistoryWindow.axaml
│   ├── KnowledgeBase.cs
│   ├── KnowledgeGraph.cs
│   └── MainWindow.axaml
├── tests/
│   ├── Casper.DataForge.Core.Smoke/
│   ├── Casper.EngineClient.Smoke/
│   └── Casper.Persistence.Smoke/
├── .github/workflows/ci.yml
├── Casper.DataForge.csproj
└── README.MD
```

`src/Casper.DataForge.CrossPlatform` is the primary Avalonia application.

The repository-root WPF project is the legacy Windows implementation. It remains included as a Windows compatibility build target but is not the primary application.

## Deterministic output

JSON:

```json
{
  "Original": "Source text",
  "Segments": [
    {
      "Index": 0,
      "Type": "text",
      "Content": "Source text",
      "Start": 0,
      "Length": 11
    }
  ]
}
```

JSONL emits one compact segment object per line.

For a fixed application version, the converter does not add timestamps, random identifiers, generated text, or external data.

The primary UI displays the full SHA-256 digest of the exact JSON or JSONL output. `Copy SHA` copies that digest without modifying the output.

## Casper engine integration

The repository currently includes a Windows `casper.exe` plus a SHA-256 manifest. Linux and macOS native Casper binaries are not included.

Engine resolution order:

1. explicit path passed to `CasperEngineClient`
2. `CASPER_DATAFORGE_ENGINE`
3. runtime-specific bundled path such as `Engine/bin/win-x64/casper.exe`
4. legacy bundled path such as `Engine/bin/casper.exe`

Optional external-engine integrity pin:

```text
CASPER_DATAFORGE_ENGINE_SHA256=<64-hex-sha256>
```

Bundled engines require a `CASPER-EXE-MANIFEST.txt` located beside the executable. A missing, malformed, file-name-mismatched, or digest-mismatched bundled manifest prevents the engine from being treated as valid.

External configured engines may run without a pin, but the UI reports them as `UNPINNED` rather than `VERIFIED`.

Engine integrity states are:

- `MISSING`: executable does not exist
- `UNPINNED`: configured external executable exists but no expected SHA-256 was supplied
- `VERIFIED`: executable SHA-256 matches its configured pin or required bundled manifest
- `INVALID`: manifest parsing, file access, or SHA-256 validation failed

The engine client also validates:

- process exit code
- JSON parsing
- source count
- effective source-number uniqueness, including implicit numbering
- finite source scores
- confidence range
- elapsed time
- echoed query identity
- proof SHA-256 format
- source SHA-256 format
- proof-file existence
- `NIYAH-PROOF-V1` proof-file header
- proof-file declared hash binding to the response `proof` digest when both are present

Relative `proof_file` values are resolved against the native engine working directory.

`ProofFileBound=true` means the proof file declares the same digest returned in the response. It does not mean the application independently re-executed or mathematically proved the native engine computation.

## Local persistence

Query sessions are stored in SQLite under the platform local-application-data directory by default.

`LocalDatabase` also accepts an explicit database path, which is used by tests and can be used by host applications that need a controlled storage location.

Stored data includes:

- query session metadata
- confidence and elapsed time
- proof digest
- source records
- evidence graph nodes and edges
- knowledge-base nodes and edges

Knowledge-base seeding is reconciled transactionally to the current bundled catalog. Removed seed nodes or edges are not left behind as stale rows.

## Evidence graph

Casper source results are converted into a query-to-source graph.

Duplicate source identities are collapsed. When duplicate sources report different scores, the graph keeps one source node and the highest score for the query-to-source edge.

Graph validation rejects:

- null node or edge collections
- duplicate node IDs
- blank node labels or kinds
- edges referencing missing nodes
- blank edge labels
- duplicate edges

The graph window visualizes the graph. It does not claim persistence status; persistence is reported separately by the database status.

## Build

Required SDK:

```text
8.0.423
```

Build the primary application:

```bash
dotnet build src/Casper.DataForge.CrossPlatform/Casper.DataForge.CrossPlatform.csproj -c Release
```

Linux and macOS builds do not require a bundled native Casper executable. The Forge UI remains buildable and usable; the Casper query feature reports the engine as missing until a compatible executable is configured.

On Windows, bundled-engine presence is validated by the project configuration.

To force bundled-engine validation on any build:

```bash
dotnet build src/Casper.DataForge.CrossPlatform/Casper.DataForge.CrossPlatform.csproj \
  -c Release \
  -p:ValidateBundledCasper=true
```

## Smoke validation

```bash
dotnet run --project tests/Casper.DataForge.Core.Smoke/Casper.DataForge.Core.Smoke.csproj -c Release
dotnet run --project tests/Casper.EngineClient.Smoke/Casper.EngineClient.Smoke.csproj -c Release
dotnet run --project tests/Casper.Persistence.Smoke/Casper.Persistence.Smoke.csproj -c Release
```

The core smoke test verifies deterministic conversion, Unicode direction detection, and stable output SHA-256 calculation.

The engine-client smoke test does not depend on the bundled Casper executable. It verifies path configuration, SHA-256 pinning, explicit integrity states, URL normalization, response validation, and proof-file digest binding.

The persistence smoke test verifies SQLite initialization with a path containing a semicolon, knowledge-base reconciliation, duplicate-source graph collapse, and session persistence.

## Continuous integration

The GitHub Actions workflow is configured to target Ubuntu and Windows with:

- Avalonia restore and Release build
- deterministic converter smoke validation
- engine-client smoke validation
- persistence smoke validation
- bundled Windows Casper SHA-256 verification
- legacy WPF restore and build on Windows

A workflow definition is not evidence of a successful run. CI success must be confirmed from the GitHub Actions result for the specific commit being evaluated.

## Windows publish

```powershell
dotnet publish .\src\Casper.DataForge.CrossPlatform\Casper.DataForge.CrossPlatform.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:ValidateBundledCasper=true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o .\release\win-x64
```

The native Casper executable must remain in the published `Engine/bin` directory with its manifest.

## Current limitations

- bundled Casper native executable: Windows only
- Linux Casper package: not included
- macOS Casper package: not included
- external engines can be intentionally used without SHA-256 pinning, but are reported as `UNPINNED`
- proof-file binding checks file metadata against the returned digest; it does not independently prove native-engine semantics
- code signing: not implemented
- automated release publishing: not implemented
- Forge conversion is offline; Casper query networking depends on the configured engine

## Release

The latest documented packaged release remains `v1.1.2`. The source tree is on the unreleased `1.2.0` line.

## Security and privacy

- Forge conversion does not require network access.
- No telemetry is intentionally implemented in this repository.
- Casper queries may use network access through the configured native engine.
- Bundled engines require a valid adjacent SHA-256 manifest before query execution.
- External engine binaries can be pinned with `CASPER_DATAFORGE_ENGINE_SHA256`.
- The UI distinguishes verified, unpinned, invalid, and missing engine states.
- The current Windows executable is not code-signed.

## License

Casper.DataForge is source-available for noncommercial use under the PolyForm Noncommercial License 1.0.0.

Commercial, corporate, production, internal-business, hosted-service, resale, integration, AI-training, model-training, or revenue-generating use requires prior written permission and a separate commercial license from:

```text
Gratech
Commercial Registration: 7050426415
Riyadh, Saudi Arabia
```

See `LICENSE` for the complete notice.
