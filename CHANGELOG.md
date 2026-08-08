# Changelog

All notable changes to Casper.DataForge are documented in this file.

The format follows Keep a Changelog principles. Versions follow Semantic Versioning where practical.

## [Unreleased]

### Added

- Shared `Casper.DataForge.Core` library used by WPF and Avalonia clients.
- Local SQLite persistence for query sessions, sources, and evidence graphs.
- Bilingual knowledge-base seed and JSON Schema contract for core technology domains.
- Evidence graph and local query-history windows.
- Bounded bilingual Chat history and animated 3D-projection graph view.
- Core and engine/database smoke validation.

### Changed

- Manual RTL/LTR direction is now preserved while editing; Auto mode remains automatic.
- Casper engine path selection now supports platform and architecture-specific binaries.
- Cancellation terminates the native Casper process safely.
- Documentation now distinguishes deterministic offline conversion from optional web retrieval.

### Planned

- Linux x64 release package
- macOS x64 release package
- macOS ARM64 release package
- Automated build and release validation
- Published checksum files
- Windows code signing

## [1.1.2] - 2026-08-05

### Security

- Removed the native self-extracting single-executable distribution.
- Replaced the direct EXE release with a conventional multi-file Windows package.
- Scanned the published directory using Microsoft Defender before packaging.
- No new Defender detection was recorded during the local pre-release scan.

### Distribution

- Windows x64 self-contained ZIP package.
- No separate .NET installation required.
- The executable must remain beside the included native libraries.
## [1.1.1] - 2026-08-05

### Added

- Direct-download Windows x64 executable
- Self-contained single-file publishing
- Native Avalonia and Skia libraries bundled for runtime extraction
- Published SHA-256 verification digest

### Changed

- Updated the project version to `1.1.1`
- Replaced the ZIP-only distribution model with a directly downloadable executable
- Improved repository documentation and release navigation

### Verified

- File: `Casper.DataForge.exe`
- Platform: Windows x64
- Distribution: Self-contained single executable
- Size: `46,935,048 bytes`
- SHA-256: `107F020B527EAE122A21EA6306FFA6E941FE6C0F2A654F26E966DDE87BA43D44`
- Separate .NET installation: Not required

## [1.1.0] - 2026-08-05

### Added

- Cross-platform Avalonia implementation under `src/Casper.DataForge.CrossPlatform`
- Dark dual-pane desktop interface
- Automatic and manual RTL/LTR controls
- JSON and JSONL output modes
- Text, fenced-code, and LaTeX segmentation
- Copy and UTF-8 file export
- Custom Casper.DataForge application identity asset
- Self-contained Windows x64 ZIP package

### Changed

- Simplified the packaged executable name to `Casper.DataForge.exe`
- Updated project documentation for the Avalonia implementation

### Verified

- Release build completed with zero warnings and zero errors
- Windows package published as `Casper.DataForge-v1.1.0-win-x64.zip`
- SHA-256: `CFB5E634BE572451D11B473793F9F4DA2522B7C5D3BD513CEA7D5FCAB38BF485`

## [1.0.0] - 2026-08-05

### Added

- Initial Windows WPF implementation
- Deterministic local conversion with `System.Text.Json`
- Arabic and English text input
- JSON and JSONL output
- Text, fenced-code, and LaTeX segmentation
- Original-content preservation
- Copy and UTF-8 save operations

[Unreleased]: https://github.com/Grar00t/Casper.DataForge/compare/v1.1.2...HEAD
[1.1.1]: https://github.com/Grar00t/Casper.DataForge/releases/tag/v1.1.1
[1.1.0]: https://github.com/Grar00t/Casper.DataForge/releases/tag/v1.1.0
[1.0.0]: https://github.com/Grar00t/Casper.DataForge/releases/tag/v1.0.0

