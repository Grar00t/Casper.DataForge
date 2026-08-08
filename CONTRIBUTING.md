# Contributing to Casper.DataForge

Thank you for considering a contribution.

## Before You Start

- Open an issue before substantial changes.
- Keep changes focused and reviewable.
- Do not commit generated output, build artifacts, credentials, personal data, or third-party material without permission.
- Preserve deterministic behavior and original input content.

## Development Environment

- Windows, Linux, or macOS
- .NET SDK 8.0.423
- Avalonia 11.3.12

Build the cross-platform implementation:

```powershell
dotnet build .\src\Casper.DataForge.CrossPlatform\Casper.DataForge.CrossPlatform.csproj -c Release
```

Run the deterministic Core checks:

```powershell
dotnet run --project .\tests\Casper.DataForge.Core.Smoke -c Release
```

Run the Casper engine and SQLite integration smoke check on a machine with the bundled engine:

```powershell
dotnet run --project .\tests\Casper.EngineClient.Smoke -c Release
```

A release-quality change must produce:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

## Engineering Requirements

Contributions must:

1. Preserve source text without generation or rewriting.
2. Preserve segment order, offsets, and lengths.
3. Keep JSON and JSONL output deterministic.
4. Preserve raw characters such as `<`, `>`, `&&`, and `*` in saved output.
5. Keep processing local unless a separately reviewed feature explicitly requires otherwise.
6. Maintain Arabic and English RTL/LTR behavior.
7. Include a concise validation procedure in the pull request.
8. Validate knowledge-base changes against `Assets/KnowledgeBase/knowledge.schema.json` and keep node IDs stable.
9. Keep UI animations bounded and non-blocking; never run engine or database work on the render thread.

## Pull Requests

Include:

- Purpose and scope
- Files changed
- Actual build output
- Manual test steps
- Before-and-after behavior
- Platform tested

## Contribution License

By submitting a contribution, you represent that you have the right to submit it. You grant Gratech a perpetual, worldwide, irrevocable, royalty-free license to use, reproduce, modify, distribute, sublicense, and relicense the contribution, including under commercial terms.

The project remains source-available under the repository license. A contribution does not change the project license unless Gratech agrees in writing.

## Commercial Use

Contributing does not grant a commercial license. Commercial and corporate use requires prior written permission from Gratech.
