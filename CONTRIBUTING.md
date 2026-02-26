# Contributing to ClipSave

ClipSave へのコントリビューションに興味を持っていただき、ありがとうございます。

## このドキュメントの役割

このドキュメントは、コントリビューションの入口です。

- 最低限の手順（Issue / PR / コミット）
- リポジトリ横断ルール（言語ポリシー）
- 詳細ドキュメントへの導線

実装規約や運用の詳細は `docs/` 配下を参照してください。

## Code of Conduct

このプロジェクトに参加するすべての人は、[行動規範](CODE_OF_CONDUCT.md) を確認し、敬意を持って接することが期待されます。

## Quick Start

1. 変更対象の Issue を確認（大きな変更は先に Issue で合意）。
2. `main` から短命ブランチを作成（`feature/...` / `fix/...` / `docs/...` / `chore/...`）。
3. 実装と必要ドキュメントを更新。
4. ローカルでテストを実行。
5. 英語のコミットメッセージでコミット。
6. PR を作成（原則 `main` 向け）。

## Issue

バグ報告・改善提案は GitHub Issues を使用してください。
再現手順、期待結果、実際の結果、環境情報を含めてください。

## Pull Request

PR 本文は [`.github/pull_request_template.md`](.github/pull_request_template.md) に沿って記載してください。

- `Summary`: 変更内容
- `Why`: 変更理由
- `Checklist`:
  - `Quality` は各項目を満たす（不要な項目は理由を `Summary` または `Why` に明記）
  - `Related Issue` はどちらか 1 つをチェック
  - `Specs` はどちらか 1 つをチェック
  - `Changelog` はどちらか 1 つをチェック

レビュー担当は [`.github/CODEOWNERS`](.github/CODEOWNERS) を参照してください。

テスト実行の目安:

```powershell
./scripts/run-tests.ps1 -Configuration Release
# 仕様変更時のみ
./scripts/check-spec-coverage.ps1
```

## Language Policy

| 対象 | 言語 |
|------|------|
| コードベース（ソースコード、YAML、スクリプト、コメント、ログ、例外、クラッシュダンプ） | 英語 |
| Issue / Pull Request（タイトル・本文） | 英語 |
| コミットメッセージ | 英語 |
| `docs/` 配下ドキュメント | 日本語 |
| UI テキスト | 英語 / 日本語（ローカライズ） |

## Commit Message

形式:

```text
<type>: <subject>
```

主な `type`:
- `feat`
- `fix`
- `docs`
- `refactor`
- `test`
- `chore`

例:

```text
feat: Add OCR support for clipboard images
```

## Development Setup

開発環境構築と実行手順は [README.md](README.md) を参照してください。

## 詳細ドキュメント

- [コーディング規約](docs/dev/CodingGuidelines.md)
- [テスト戦略](docs/dev/TestingStrategy.md)
- [仕様](docs/dev/Specification.md)
- [アーキテクチャ](docs/dev/Architecture.md)
- [ブランチ戦略](docs/ops/BranchStrategy.md)
- [デプロイ手順](docs/ops/Deployment.md)
- [署名運用](docs/ops/Signing.md)
- [バージョニング規約](docs/ops/Versioning.md)
- [CHANGELOG 運用](docs/ops/ReleaseNotes.md)

## Questions?

質問がある場合は Issue で問い合わせてください。

メンテナー連絡先: `tnagata012@gmail.com`

---

**Thank you for contributing to ClipSave!**
