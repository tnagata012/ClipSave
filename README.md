# ClipSave

[![Dev Build](https://github.com/tnagata012/ClipSave/actions/workflows/dev-build.yml/badge.svg?branch=main)](https://github.com/tnagata012/ClipSave/actions/workflows/dev-build.yml)
[![Release Build](https://github.com/tnagata012/ClipSave/actions/workflows/release-build.yml/badge.svg)](https://github.com/tnagata012/ClipSave/actions/workflows/release-build.yml)
[![GitHub prerelease](https://img.shields.io/github/v/release/tnagata012/ClipSave?include_prereleases&label=dev%20prerelease)](https://github.com/tnagata012/ClipSave/releases/tag/dev-latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> ホットキー一発でクリップボード内容をファイル保存できる Windows 常駐アプリ

ClipSave は、コピーした内容を `Ctrl+Shift+V` で即保存する軽量ツールです。コンテンツをファイルとして保存します。ネットワーク通信やテレメトリはありません。

## クイックスタート

1. ClipSave をインストールして起動
2. 保存したい内容をコピー
3. `Ctrl+Shift+V` を押す

対応コンテンツ: 画像 / CSV / JSON / Markdown / テキスト（自動判別）

自動起動はインストール直後は有効になります（Windows の「設定 > アプリ > スタートアップ」で変更可能）。

## 動作環境

- **OS**: Windows 11
- **Runtime**: .NET 10
- **CPU**: AnyCPU

## インストール

- **Stable（一般ユーザー向け）**: Microsoft Store（公開後）
- **Dev/Release artifacts**: `Releases`（`dev-latest` / `release-latest`）の `*.msixbundle` は未署名の検証アーティファクトです。インストールする場合は、[検証アーティファクト導入手順](docs/ops/ArtifactInstallation.md) を参照してください
- **開発・検証（推奨）**: ソースから実行

```bash
git clone https://github.com/tnagata012/ClipSave.git
cd ClipSave
dotnet restore src/ClipSave/ClipSave.csproj
dotnet run --project src/ClipSave/ClipSave.csproj --configuration Release
```

## ドキュメント

- [使い方ガイド](docs/UsageGuide.md) - 基本操作と設定
- [製品コンセプト](docs/ProductConcept.md) - ビジョンと設計思想
- [ランディングページ運用](docs/presentation/LandingPage.md) - `site/` の更新方針と確認手順
- [リリースノート](RELEASE_NOTES.md) - 変更履歴

## 開発者向け

開発に参加したい方は [CONTRIBUTING.md](CONTRIBUTING.md) を参照してください。

| カテゴリ | ドキュメント |
|---------|-------------|
| 設計 | [仕様](docs/dev/Specification.md) ・ [アーキテクチャ](docs/dev/Architecture.md) ・ [コーディングガイドライン](docs/dev/CodingGuidelines.md) |
| テスト | [テスト戦略](docs/dev/TestingStrategy.md) |
| 運用 | [デプロイ](docs/ops/Deployment.md) ・ [署名運用](docs/ops/Signing.md) ・ [バージョニング](docs/ops/Versioning.md) ・ [ブランチ戦略](docs/ops/BranchStrategy.md) ・ [検証アーティファクト導入](docs/ops/ArtifactInstallation.md) ・ [アイコン運用](docs/presentation/IconAssets.md) |

## セキュリティ

セキュリティポリシーと脆弱性の報告方法については [SECURITY.md](SECURITY.md) を参照してください。

## ライセンス

このプロジェクトは MIT ライセンスの下で公開されています。詳細は [LICENSE](LICENSE) を参照してください。

サードパーティライブラリのライセンスについては [NOTICES](NOTICES) を参照してください。

---

**Copyright (c) 2026 TNagata012**
