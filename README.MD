# Casper.DataForge

Deterministic desktop converter for structuring Arabic and English text as JSON or JSONL without generation or rewriting.

## Features

- Cross-platform Avalonia UI
- Dark dual-pane interface
- Automatic and manual RTL/LTR control
- Deterministic JSON and JSONL output
- Text, fenced code, and LaTeX segmentation
- Original text and character-position preservation
- Copy and UTF-8 export
- Local processing with System.Text.Json
- Custom application icon

## Download

Download `Casper.DataForge-v1.1.0-win-x64.zip` from GitHub Releases, extract all files, then run `Casper.DataForge.exe`.

## Requirements

- Windows 10 or later
- Windows x64
- No separate .NET installation required for the packaged release

## Build

```powershell
dotnet build .\src\Casper.DataForge.CrossPlatform\Casper.DataForge.CrossPlatform.csproj -c Release
```

## Project Layout

- Root: original Windows WPF implementation
- `src/Casper.DataForge.CrossPlatform`: Avalonia cross-platform implementation
- `Assets/Casper.DataForge.png`: application identity asset

## Release

Version 1.1.0 introduces the Avalonia implementation, application icon, simplified executable name, and self-contained Windows x64 package.

## License

Copyright (c) 2026 Sulaiman Alshammari. No open-source license has been assigned.
