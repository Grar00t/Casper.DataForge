# Changelog

All notable changes to Casper.DataForge are documented in this file.

The format follows Keep a Changelog principles, and versions follow Semantic Versioning where practical.

## [Unreleased]

### Planned

- Linux x64 release package
- macOS x64 release package
- macOS ARM64 release package
- Automated release validation and checksums

## [1.1.0] - 2026-08-05

### Added

- Cross-platform Avalonia implementation under `src/Casper.DataForge.CrossPlatform`
- Dark dual-pane desktop interface
- Automatic and manual RTL/LTR controls
- JSON and JSONL output modes
- Text, fenced-code, and LaTeX segmentation
- Copy and UTF-8 file export
- Custom Casper.DataForge application identity asset
- Self-contained Windows x64 release package

### Changed

- Simplified packaged executable name to `Casper.DataForge.exe`
- Updated project documentation for the Avalonia implementation

### Verified

- Release build completed with zero warnings and zero errors
- Windows x64 package published as `Casper.DataForge-v1.1.0-win-x64.zip`
- Release asset SHA-256: `cfb5e634be572451d11b473793f9f4da2522b7c5d3bd513cea7d5fcab38bf485`

## [1.0.0] - 2026-08-05

### Added

- Initial Windows WPF implementation
- Deterministic local conversion with `System.Text.Json`
- Arabic and English text input
- JSON and JSONL output
- Text, code, and LaTeX segmentation
- Original-content preservation
- Copy and UTF-8 save operations

[Unreleased]: https://github.com/Grar00t/Casper.DataForge/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/Grar00t/Casper.DataForge/releases/tag/v1.1.0
[1.0.0]: https://github.com/Grar00t/Casper.DataForge/releases/tag/v1.0.0
