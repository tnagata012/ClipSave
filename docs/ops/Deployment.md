# デプロイ

ClipSave の CI/CD と配布実行手順（Runbook）を定義します。

## この文書の責務

この文書では、以下を扱います。

- GitHub Actions ワークフローの役割
- 配布チャネルごとの成果物
- メジャー/マイナー、パッチ、Store 提出の実行手順
- ロールバック/取り下げ手順

この文書では、以下は扱いません。

- ブランチ設計方針（`BranchStrategy.md`）
- 版数フォーマットや `PATCH` 規約の定義（`Versioning.md`）
- 署名方針（`Signing.md`）

## ワークフロー一覧

| ワークフロー | トリガー | 用途 | 主な生成物 |
|-------------|---------|------|-----------|
| [pr-check.yml](../../.github/workflows/pr-check.yml) | PR（`main`, `release/*`） | 品質ゲート | `TestResults/**/*.trx` |
| [prepare-release-branch.yml](../../.github/workflows/prepare-release-branch.yml) | 手動（`X.Y.0`） | `release/X.Y` 作成 + main 側 bump ブランチ作成 | `release/X.Y`, `chore/bump-main-to-*` |
| [prepare-patch-release.yml](../../.github/workflows/prepare-patch-release.yml) | 手動（`release/X.Y`） | patch init ブランチ作成 | `chore/release-X.Y.(Z+1)-init` |
| [dev-build.yml](../../.github/workflows/dev-build.yml) | `main` push / 手動 | 開発成果物生成（未署名） | `dev-package-*`, `dev-latest`, `SHA256SUMS.txt`, `GitHub Artifact Attestation` |
| [release-build.yml](../../.github/workflows/release-build.yml) | `release/*` push / 手動 | 公開候補生成（未署名） | `release-package-*`, `release-latest`, `SHA256SUMS.txt`, `GitHub Artifact Attestation` |
| [store-publish.yml](../../.github/workflows/store-publish.yml) | 手動（`X.Y.Z`） | Store 提出物生成 | `store-package-*`（`.msixupload`） |

補足:

- 配布対象は `*.msixbundle`（未署名）、Store 提出対象は `.msixupload`。
- Dev/Release 配布では `*.msixbundle` と `SHA256SUMS.txt` をセットで公開し、GitHub Artifact Attestation を記録する。
- `PATCH` 更新規約は `Versioning.md` を正本とする。

## 実行前チェック

1. `main` / `release/X.Y` への直 push を行わない運用であることを確認する。
2. 実行対象ブランチ（`main` または `release/X.Y`）が意図どおりであることを確認する。
3. `./scripts/assert-version-policy.ps1` が成功することを確認する。
4. `./scripts/run-tests.ps1` と `./scripts/run-security-checks.ps1` が成功することを確認する。
5. チャネル別の署名方針（Dev/Release は未署名許容、Stable は Store 正本）を理解したうえで、検証対象を明確化する（詳細は `Signing.md`）。

## 成果物チャネル

| チャネル | 配布元 | 用途 |
|---------|-------|------|
| Dev | `dev-latest` / `dev-package-*` + `SHA256SUMS.txt` + `GitHub Artifact Attestation` | 検証配布（未署名） |
| Release | `release-latest` / `release-package-*` + `SHA256SUMS.txt` + `GitHub Artifact Attestation` | 公開候補比較（未署名） |
| Store | `store-package-*`（`.msixupload`） | Partner Center 提出 |

## Store Publish の版数解決ルール

- 入力 `version=X.Y.Z` から対象ブランチ `release/X.Y` を解決する。
- 解決した `release/X.Y` を checkout し、その時点のソースから再ビルドする。
- 入力 `X.Y.Z` と `Directory.Build.props` が一致しない場合は失敗する。

## 実運用手順

### メジャー/マイナーリリース

1. `Prepare Release Branch`（推奨）または `create-release-branch.ps1` で `release/X.Y` を作成する。
2. `chore/bump-main-to-* -> main` の PR をレビューしてマージする。
3. `release/X.Y` の安定化を PR で反映する。
4. 複数の公開候補（`release-package-*`）から採用コミットを決定する。
5. 採用版を `release-latest` に反映して候補比較に使う（現行は未署名）。

### パッチリリース

1. `Prepare Patch Release`（推奨）または `create-patch-release-branch.ps1` で patch init ブランチを作成する。
2. patch init PR（`chore/release-X.Y.(Z+1)-init -> release/X.Y`）をマージする。
3. 不具合修正を `main` へマージする。
4. `release/X.Y` をベースにした `fix/*` backport ブランチで必要コミットを `cherry-pick -x` し、PR で `release/X.Y` へ反映する。
5. 候補ビルドから採用コミットを決定し、Store 提出対象版を確定する。

### Store 提出

1. `./scripts/store-checklist.ps1` を実行する。
2. `Store Publish`（`version=X.Y.Z`）または `./scripts/build-store-package.ps1` で `.msixupload` を生成する。
3. Partner Center に提出する。

## ロールバック/取り下げ

### Dev / Release 配布物

1. 問題のある配布リンクを停止または更新する。
2. `main`（必要なら `release/X.Y`）へ復旧 PR を反映する。
3. 修正版を再ビルドして差し替える。

### Store 提出後

1. Partner Center 側で該当提出の公開を停止/取り下げする。
2. `release/X.Y` で次のパッチ版を準備する。
3. 新版 `.msixupload` を再提出する。

## 補足

### なぜ Store 提出を手動運用にするか

| 理由 | 内容 |
|------|------|
| 審査プロセス | Microsoft 側審査が必要 |
| メタデータ更新 | 説明文・画像更新に人手が必要 |
| リスク管理 | 段階的リリース判断が必要 |

### 便利コマンド

```powershell
.\scripts\show-version-report.ps1
.\scripts\assert-version-policy.ps1 -BranchName release/1.3
.\scripts\run-tests.ps1 -Configuration Release
.\scripts\run-security-checks.ps1 -Configuration Release
.\scripts\store-checklist.ps1
.\scripts\verify-artifact.ps1 -BundlePath .\ClipSave.Package_X.Y.Z.W_AnyCPU.msixbundle -Channel dev -SourceRef refs/heads/main
```

## 関連ドキュメント

- [BranchStrategy](BranchStrategy.md) — ブランチ構成と統合方向
- [Versioning](Versioning.md) — 版数規約
- [Signing](Signing.md) — 署名方針（チャネル別運用）
- [IconAssets](../presentation/IconAssets.md) — アイコン運用
- [RELEASE_NOTES](../../RELEASE_NOTES.md) — 変更履歴
