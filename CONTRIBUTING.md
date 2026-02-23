# Contributing to ClipSave

ClipSave へのコントリビューションに興味を持っていただき、ありがとうございます。

## このドキュメントの役割

- コントリビューション手順の入口を示す。
- リポジトリ横断ルール（言語ポリシー、コミットメッセージ）を定義する。
- 実装規約の詳細は `docs/dev/CodingGuidelines.md` を参照する。

## Code of Conduct

このプロジェクトに参加するすべての人は、[行動規範](CODE_OF_CONDUCT.md) を確認し、敬意を持って接することが期待されます。

## Quick Start

1. 変更対象の Issue を確認（大きな変更は先に Issue で合意）。
2. `main` から短命ブランチを作成（`feature/...` / `fix/...` / `docs/...` / `chore/...`）。
3. 実装と必要ドキュメントを更新。
4. ローカルでテストを実行。
5. 英語のコミットメッセージでコミット。
6. PR を作成（原則 `main` 向け）。

## Issue ガイド

### Bug Report

バグを発見した場合は、GitHub Issues で報告してください。次の情報を含めてください。

- 問題の簡潔な説明
- 再現手順
- 期待される動作
- 実際の動作
- 環境情報（OS バージョン、.NET バージョン）
- スクリーンショット（該当する場合）

### Enhancement

機能要望や改善提案も Issues で受け付けています。次の情報を含めてください。

- 機能の説明
- ユースケース
- 既存の代替手段との比較

## Pull Request ガイド

- 実装規約: `docs/dev/CodingGuidelines.md`
- テスト方針: `docs/dev/TestingStrategy.md`
- リリース運用: `docs/ops/BranchStrategy.md` / `docs/ops/Versioning.md` / `docs/ops/Deployment.md` / `docs/ops/Signing.md` / `docs/ops/ReleaseNotes.md`
- レビュー担当: `.github/CODEOWNERS`（`@TNagata012`）

PR には少なくとも次を含めてください。

- 変更概要と背景
- テスト結果（実行コマンドと結果）
- 仕様影響がある場合は関連 SPEC-ID やドキュメント更新内容
- ユーザー影響がある変更は `CHANGELOG.md` の `[Unreleased]` を更新（詳細は `docs/ops/ReleaseNotes.md`）

## Language Policy

| 対象 | 言語 |
|------|------|
| コードベース（ソースコード、YAML、スクリプト、コメント、ログ、例外、クラッシュダンプ） | 英語 |
| Issue / Pull Request（タイトル・本文） | 英語 |
| コミットメッセージ | 英語 |
| `docs/` 配下ドキュメント | 日本語 |
| UI テキスト | 英語 / 日本語（ローカライズ） |

## Commit Message Format

```text
<type>: <subject>

<body>

<footer>
```

Types:

- `feat`: 新機能
- `fix`: バグ修正
- `docs`: ドキュメントのみの変更
- `style`: コードの動作に影響しない変更（フォーマット等）
- `refactor`: リファクタリング
- `test`: テストの追加・修正
- `chore`: ビルドプロセス、補助ツール等の変更

Example:

```text
feat: Add OCR support for clipboard images

- Implement OCR service using Windows.Media.Ocr
- Add OCR result display in notification
- Update settings UI to enable/disable OCR

Closes #123
```

## Development Setup

### Prerequisites

- .NET 10 SDK
- Visual Studio 2022 (推奨: `MSIX Packaging Tools` / Desktop Bridge が利用可能な構成)
- Windows 11

> 補足:
> `src/ClipSave/ClipSave.csproj` のビルド・実行・テストは .NET SDK 単体でも可能です。
> `src/ClipSave.Package/ClipSave.Package.wapproj` のビルドには Desktop Bridge 関連ターゲットが必要です。

### Build & Run

```bash
git clone https://github.com/tnagata012/ClipSave.git
cd ClipSave
dotnet restore src/ClipSave/ClipSave.csproj
dotnet build src/ClipSave/ClipSave.csproj
dotnet run --project src/ClipSave
```

### Run Tests

```powershell
.\scripts\run-tests.ps1 -Configuration Release
```

## 関連ドキュメント

- `docs/dev/CodingGuidelines.md`: コーディング規約（基準ドキュメント）
- `docs/dev/TestingStrategy.md`: テスト戦略
- `docs/dev/Architecture.md`: アーキテクチャ
- `docs/dev/Specification.md`: 仕様（SPEC-ID）
- `docs/ops/BranchStrategy.md`: ブランチ運用
- `docs/ops/Versioning.md`: バージョニング規約
- `docs/ops/Deployment.md`: CI/CD と配布手順
- `docs/ops/Signing.md`: 署名・証明書運用

## Questions?

質問がある場合は、Issue で気軽に尋ねてください。

メンテナー連絡先: `tnagata012@gmail.com`

---

**Thank you for contributing to ClipSave!**
