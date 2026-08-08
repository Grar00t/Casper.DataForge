<p align="center">
  <img src="src/Casper.DataForge.CrossPlatform/Assets/Casper.DataForge.png" width="128" alt="Casper.DataForge logo">
</p>

<h1 align="center">Casper.DataForge</h1>

<p align="center">Deterministic local text structuring for JSON and JSONL.</p>

<p align="center">
  <a href="https://github.com/Grar00t/Casper.DataForge/releases/download/v1.1.2/Casper.DataForge-v1.1.2-win-x64.zip"><img src="https://img.shields.io/badge/Download-Windows_x64_ZIP-2563EB?style=for-the-badge&amp;logo=windows&amp;logoColor=white" alt="Download Windows x64 ZIP"></a>
  <a href="https://github.com/Grar00t/Casper.DataForge/releases/tag/v1.1.2"><img src="https://img.shields.io/badge/Release-v1.1.2-238636?style=for-the-badge&amp;logo=github&amp;logoColor=white" alt="Release v1.1.2"></a>
</p>

## Overview

Casper.DataForge is a desktop application that converts mixed Arabic and English text into structured JSON or JSONL. It preserves the original input and does not generate, summarize, rewrite, complete, or reinterpret source content.

The Forge conversion path is deterministic and local. The optional Casper query path is separate: it runs the bundled Casper engine and may use its configured web-retrieval backend. A Casper query must not be described as offline unless the engine is configured and verified for offline use.

## Why This Exists

Some text-processing tasks require repeatable transformation rather than probabilistic generation. Casper.DataForge provides a narrow, inspectable workflow:

- identical input produces identical structured output
- conversion runs locally
- source text remains preserved
- segment positions remain inspectable
- output can be verified without an external service
- no cloud account or API key is required by the Forge converter

The goal is not to replace generative systems. The goal is to use deterministic software where deterministic transformation is sufficient.

## Download

### Windows x64

[Download Casper.DataForge-v1.1.2-win-x64.zip](https://github.com/Grar00t/Casper.DataForge/releases/download/v1.1.2/Casper.DataForge-v1.1.2-win-x64.zip)

The package is self-contained and does not require a separate .NET installation. Keep the included `Engine` directory beside the application executable.

| Property | Value |
|---|---|
| Released version | `1.1.2` |
| Current project version | `1.2.0` (unreleased) |
| Platform | Windows x64 |
| Distribution | Self-contained multi-file ZIP |
| File | `Casper.DataForge-v1.1.2-win-x64.zip` |
| Size | `41,896,701 bytes` |
| SHA-256 | `58FECA10F98180B3ECC89E4920D79B80D8751F5A38E1299D79F55E99324CBA32` |

Verify the downloaded archive:

```powershell
Get-FileHash .\Casper.DataForge-v1.1.2-win-x64.zip -Algorithm SHA256
```

## Features

- Deterministic JSON and JSONL serialization
- Arabic and English text support
- Automatic and manual RTL/LTR controls
- Plain-text, fenced-code, and display-LaTeX segmentation
- Original-source preservation
- Character offset and length tracking
- UTF-8 export without BOM
- Clipboard copy
- Local processing with `System.Text.Json`
- Local SQLite history for query sessions, sources, and evidence graphs
- Animated 3D-projection evidence and knowledge graphs
- Modern bilingual Chat surface with bounded message history
- Bilingual knowledge-base seed with a versioned JSON Schema
- Dark dual-pane Avalonia interface
- No required network connection for Forge conversion

The graph view uses an animated 3D projection on an Avalonia Canvas. It is intentionally dependency-light rather than a full GPU scene engine; node depth, perspective, and rotation are deterministic and UI-safe.

## Output Model

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

Supported segment types: `text`, `code`, and `latex`. JSONL mode emits one compact JSON object per segment.

## Behavioral Guarantees

For a fixed application version:

1. Source text is not rewritten.
2. Segment order follows source order.
3. `Start` and `Length` refer to the original .NET string.
4. No timestamps or random identifiers are inserted.
5. Raw operators such as `<`, `>`, `&&`, and `*` remain preserved.
6. Serialization is performed locally.
7. Identical input produces identical structured output.

## Architecture

```text
Casper.DataForge/
├── src/Casper.DataForge.Core/
│   ├── DeterministicConverter.cs
│   └── Casper.DataForge.Core.csproj
├── src/Casper.DataForge.CrossPlatform/
│   ├── Assets/
│   ├── Data/
│   ├── Engine/
│   ├── App.axaml
│   ├── GraphWindow.axaml
│   ├── HistoryWindow.axaml
│   ├── KnowledgeBase.cs
│   ├── KnowledgeGraph.cs
│   ├── MainWindow.axaml
│   └── Casper.DataForge.CrossPlatform.csproj
├── tests/
│   ├── Casper.DataForge.Core.Smoke/
│   └── Casper.EngineClient.Smoke/
├── CHANGELOG.md
├── CONTRIBUTING.md
├── LICENSE
└── README.MD
```

- Repository root: original Windows WPF implementation
- `src/Casper.DataForge.CrossPlatform`: current Avalonia implementation
- `src/Casper.DataForge.Core`: shared deterministic segmentation, serialization, and direction detection
- `Data/LocalDatabase.cs`: versioned SQLite schema and local query persistence
- `KnowledgeGraph.cs`: evidence graph model
- `GraphWindow.axaml`: graph visualization
- `HistoryWindow.axaml`: local query history
- `Assets/KnowledgeBase/knowledge.seed.json`: curated bilingual technology taxonomy
- `Assets/KnowledgeBase/knowledge.schema.json`: JSON Schema contract for seed data
- `Assets/Casper.DataForge.png`: application identity asset

## Build

Requirements:

- .NET SDK `8.0.423`
- Windows, Linux, or macOS for development

```powershell
dotnet build .\src\Casper.DataForge.CrossPlatform\Casper.DataForge.CrossPlatform.csproj -c Release
```

Expected result:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

## Publish a Windows Self-Contained Package

```powershell
dotnet publish .\src\Casper.DataForge.CrossPlatform\Casper.DataForge.CrossPlatform.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o .\release\win-x64
```

The output is intentionally multi-file: the Casper native engine is launched from `Engine/bin/casper.exe` and must remain beside the application. The application may extract bundled native Avalonia and Skia components to a temporary location while running.

## Project Status

| Area | Status |
|---|---|
| Windows x64 self-contained multi-file package | Available |
| Deterministic JSON and JSONL output | Verified |
| Arabic and English input | Verified |
| RTL/LTR controls | Verified |
| Code and LaTeX segmentation | Verified |
| UTF-8 export | Verified |
| Local SQLite schema v2 | Verified |
| Knowledge-base seed validation | Verified |
| 3D-projection graph UI | Implemented |
| Linux release package | Planned |
| macOS x64 and ARM64 packages | Planned |
| Automated release workflow | Planned |
| Code signing | Not implemented |

## Security and Privacy

- Forge input is processed locally.
- Casper queries may access the engine's configured retrieval backend.
- No telemetry is intentionally implemented.
- No network access is required for conversion.
- No credentials or API keys are required.
- Saved output remains under the user's control.

The current Windows executable is not code-signed. Verify the SHA-256 digest before execution.

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md) before submitting an issue or pull request.

## Changelog

Release history is maintained in [CHANGELOG.md](CHANGELOG.md).

## Licensing

Casper.DataForge is source-available for noncommercial use under the PolyForm Noncommercial License 1.0.0. Commercial, corporate, production, internal-business, hosted-service, resale, integration, AI-training, model-training, or revenue-generating use requires prior written permission and a separate commercial license from:

```text
Gratech
Commercial Registration: 7050426415
Riyadh, Saudi Arabia
```

See [LICENSE](LICENSE) for the complete notice.

## Maintainer

Maintained through [Grar00t](https://github.com/Grar00t) under the Gratech commercial licensing identity. Technical claims are intended to be supported by source code, build output, release artifacts, and reproducible verification steps.
