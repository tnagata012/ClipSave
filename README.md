# ClipSave

[![Dev Build](https://github.com/tnagata012/ClipSave/actions/workflows/dev-build.yml/badge.svg?branch=main)](https://github.com/tnagata012/ClipSave/actions/workflows/dev-build.yml)
[![RC Build](https://github.com/tnagata012/ClipSave/actions/workflows/rc-build.yml/badge.svg)](https://github.com/tnagata012/ClipSave/actions/workflows/rc-build.yml)
[![Dev Channel](https://img.shields.io/badge/Dev%20Channel-dev--latest-2f6feb)](https://github.com/tnagata012/ClipSave/releases/tag/dev-latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> A lightweight Windows tray app that turns clipboard content into ready-to-use files with `Ctrl+Shift+V`

ClipSave is a lightweight tool for people who repeatedly save screenshots, tables, JSON, Markdown, and notes. Press `Ctrl+Shift+V` and the app automatically detects clipboard content, converts tables to CSV, formats JSON, and saves the result directly to your desktop or the folder open in Explorer. You do not need to open a Save dialog every time. There is no network communication and no telemetry.

ClipSave works well when you want to:

- Quickly save screenshots or copied images as files
- Convert copied web or Excel tables into CSV
- Save JSON, Markdown, or notes without choosing a destination every time

## Quick Start

1. Install and launch ClipSave
2. Copy the content you want to save
3. Press `Ctrl+Shift+V`

Supported content: images / CSV / JSON / Markdown / text (auto-detected)  
Save destination: the desktop or the active Explorer folder

Auto-start is enabled immediately after installation. You can change it in Windows under `Settings > Apps > Startup`.

## System Requirements

- **OS**: Windows 11
- **Runtime**: .NET 10
- **CPU**: AnyCPU

## Installation

- **Stable (general users)**: Microsoft Store (after publication)
- **Development (recommended)**: run from source

```bash
git clone https://github.com/tnagata012/ClipSave.git
cd ClipSave
dotnet restore src/ClipSave/ClipSave.csproj
dotnet run --project src/ClipSave/ClipSave.csproj --configuration Release
```

- **Validation (optional)**: use Dev/RC artifacts (`dev-latest` / `rc-X.Y-latest`) or a versioned GitHub Release (`X.Y.Z`) `*.msixbundle`
  - `*.msixbundle` files are unsigned validation artifacts.
  - If you want to install one, see [Validation Artifact Installation](docs/ops/ArtifactInstallation.md).

## Documentation

Project documentation is written in Japanese.

- [Usage Guide](docs/UsageGuide.md) - Basic operations and settings
- [Product Concept](docs/ProductConcept.md) - Vision and design principles
- [Landing Page Operations](docs/presentation/LandingPage.md) - Update policy and verification steps for `site/`
- [Changelog](CHANGELOG.md) - Change history

## For Contributors

If you want to contribute, see [CONTRIBUTING.md](CONTRIBUTING.md).

| Category | Documents |
|---------|-------------|
| Design | [Specification](docs/dev/Specification.md) ・ [Architecture](docs/dev/Architecture.md) ・ [Coding Guidelines](docs/dev/CodingGuidelines.md) |
| Testing | [Testing Strategy](docs/dev/TestingStrategy.md) |
| Operations | [Deployment](docs/ops/Deployment.md) ・ [Store Submission](docs/store/Submission.md) ・ [Signing](docs/ops/Signing.md) ・ [Versioning](docs/ops/Versioning.md) ・ [Branch Strategy](docs/ops/BranchStrategy.md) ・ [Release Notes Operations](docs/ops/ReleaseNotes.md) ・ [Validation Artifact Installation](docs/ops/ArtifactInstallation.md) ・ [Icon Asset Operations](docs/presentation/IconAssets.md) |

## Security

See [SECURITY.md](SECURITY.md) for the security policy and how to report vulnerabilities.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

For third-party library licenses, see [NOTICES](NOTICES).

---

**Copyright (c) 2026 tnagata012**
