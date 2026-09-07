# Contributing to Casper.DataForge

## Development environment

- Windows, Linux, or macOS
- .NET SDK 8.0.423
- Avalonia 11.3.12

Build the primary application:

```bash
dotnet build src/Casper.DataForge.CrossPlatform/Casper.DataForge.CrossPlatform.csproj -c Release
```

Run all smoke checks:

```bash
dotnet run --project tests/Casper.DataForge.Core.Smoke/Casper.DataForge.Core.Smoke.csproj -c Release
dotnet run --project tests/Casper.EngineClient.Smoke/Casper.EngineClient.Smoke.csproj -c Release
dotnet run --project tests/Casper.Persistence.Smoke/Casper.Persistence.Smoke.csproj -c Release
```

The engine-client smoke check does not require the bundled native Casper executable.

On Windows, build the legacy WPF project as an additional compatibility check:

```powershell
dotnet build .\Casper.DataForge.csproj -c Release
```

## Engineering requirements

Changes must:

1. Preserve source text without generation or rewriting in the Forge converter.
2. Preserve segment order, offsets, and lengths.
3. Keep JSON and JSONL output deterministic.
4. Preserve raw characters such as `<`, `>`, `&&`, and `*`.
5. Keep Forge conversion local and network-independent.
6. Keep Casper networking explicitly separated from Forge conversion.
7. Preserve Arabic and English direction detection.
8. Validate native-engine output before graph construction or persistence.
9. Keep database writes off the UI event path when they can block.
10. Validate graph invariants before persistence or rendering.
11. Validate knowledge-base changes and keep node IDs stable.
12. Do not commit credentials, private data, generated build output, or unverified third-party binaries.

## Native Casper integration

A configured external engine can be selected with:

```text
CASPER_DATAFORGE_ENGINE=/absolute/path/to/casper
```

An external engine can be integrity-pinned with:

```text
CASPER_DATAFORGE_ENGINE_SHA256=<64-hex-sha256>
```

Bundled engine changes must update the adjacent `CASPER-EXE-MANIFEST.txt` and pass the Windows digest check in CI.

## Pull requests

Include:

- purpose and scope
- files changed
- build and smoke-test results
- before-and-after behavior
- platform tested

## Contribution license

By submitting a contribution, you represent that you have the right to submit it. You grant Gratech a perpetual, worldwide, irrevocable, royalty-free license to use, reproduce, modify, distribute, sublicense, and relicense the contribution, including under commercial terms.

The project remains source-available under the repository license. A contribution does not change the project license unless Gratech agrees in writing.

## Commercial use

Contributing does not grant a commercial license. Commercial and corporate use requires prior written permission from Gratech.
