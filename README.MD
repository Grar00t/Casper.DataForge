# Casper.DataForge

Deterministic local WPF converter for Arabic and English text.

## Features

- Dark dual-pane WPF interface
- Automatic RTL and LTR input direction
- JSON and JSONL output
- Separates plain text, fenced code, and LaTeX
- Preserves original input without generation or rewriting
- Copy and UTF-8 save support
- Fully local using System.Text.Json

## Requirements

- Windows 10 or later
- x64 processor

## Download

Download the ready-to-run Windows x64 package from GitHub Releases.

## Build

```powershell
dotnet build -c Release
```

## Publish

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

## License

Copyright (c) 2026 Sulaiman Alshammari.
