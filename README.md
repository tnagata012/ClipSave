# ClipSave

[![Dev Build](https://github.com/tnagata012/ClipSave/actions/workflows/dev-build.yml/badge.svg?branch=main)](https://github.com/tnagata012/ClipSave/actions/workflows/dev-build.yml)
[![RC Build](https://github.com/tnagata012/ClipSave/actions/workflows/rc-build.yml/badge.svg)](https://github.com/tnagata012/ClipSave/actions/workflows/rc-build.yml)
[![Dev Channel](https://img.shields.io/badge/Dev%20Channel-dev--latest-2f6feb)](https://github.com/tnagata012/ClipSave/releases/tag/dev-latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> コピーしたものを `Ctrl+Shift+V` でそのまま使えるファイルに変える Windows 常駐アプリ

ClipSave は、スクリーンショット、表、JSON、Markdown、メモを何度も保存する人のための軽量ツールです。`Ctrl+Shift+V` を押すだけでクリップボード内容を自動判別し、表は CSV に、JSON は整形して、デスクトップまたは今開いているエクスプローラーのフォルダーへ直接保存します。保存ダイアログを何度も開く必要はありません。ネットワーク通信やテレメトリもありません。

こんな用途に向いています。

- スクリーンショットやコピー画像をすぐファイル化したい
- Web や Excel からコピーした表を CSV にしたい
- JSON や Markdown やメモを保存先選択なしで残したい

## クイックスタート

1. ClipSave をインストールして起動
2. 保存したい内容をコピー
3. `Ctrl+Shift+V` を押す

対応コンテンツ: 画像 / CSV / JSON / Markdown / テキスト（自動判別）
保存先: デスクトップまたはアクティブなエクスプローラーのフォルダー

自動起動はインストール直後は有効になります（Windows の「設定 > アプリ > スタートアップ」で変更可能）。

## 動作環境

- **OS**: Windows 11
- **Runtime**: .NET 10
- **CPU**: AnyCPU

## インストール

- **Stable（一般ユーザー向け）**: Microsoft Store（公開後）
- **開発（推奨）**: ソースから実行

```bash
git clone https://github.com/tnagata012/ClipSave.git
cd ClipSave
dotnet restore src/ClipSave/ClipSave.csproj
dotnet run --project src/ClipSave/ClipSave.csproj --configuration Release
```

- **検証（任意）**: Dev/RC artifacts（`dev-latest` / `rc-X.Y-latest`）または確定版 GitHub Release（`X.Y.Z`）の `*.msixbundle` を利用
  - `*.msixbundle` は未署名の検証アーティファクトです。
  - インストールする場合は、[検証アーティファクト導入手順](docs/ops/ArtifactInstallation.md) を参照してください。

## ドキュメント

- [使い方ガイド](docs/UsageGuide.md) - 基本操作と設定
- [製品コンセプト](docs/ProductConcept.md) - ビジョンと設計思想
- [ランディングページ運用](docs/presentation/LandingPage.md) - `site/` の更新方針と確認手順
- [Changelog](CHANGELOG.md) - 変更履歴

## 開発者向け

開発に参加したい方は [CONTRIBUTING.md](CONTRIBUTING.md) を参照してください。

| カテゴリ | ドキュメント |
|---------|-------------|
| 設計 | [仕様](docs/dev/Specification.md) ・ [アーキテクチャ](docs/dev/Architecture.md) ・ [コーディングガイドライン](docs/dev/CodingGuidelines.md) |
| テスト | [テスト戦略](docs/dev/TestingStrategy.md) |
| 運用 | [デプロイ](docs/ops/Deployment.md) ・ [Store サブミッション運用](docs/store/Submission.md) ・ [署名運用](docs/ops/Signing.md) ・ [バージョニング](docs/ops/Versioning.md) ・ [ブランチ戦略](docs/ops/BranchStrategy.md) ・ [CHANGELOG 運用](docs/ops/ReleaseNotes.md) ・ [検証アーティファクト導入](docs/ops/ArtifactInstallation.md) ・ [アイコン運用](docs/presentation/IconAssets.md) |

## セキュリティ

セキュリティポリシーと脆弱性の報告方法については [SECURITY.md](SECURITY.md) を参照してください。

## ライセンス

このプロジェクトは MIT ライセンスの下で公開されています。詳細は [LICENSE](LICENSE) を参照してください。

サードパーティライブラリのライセンスについては [NOTICES](NOTICES) を参照してください。

---

**Copyright (c) 2026 tnagata012**
