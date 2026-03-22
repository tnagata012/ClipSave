# リリースガイド

**このドキュメントの目的**: ClipSave の workflow、成果物、配布実行手順を定義します。
`docs/release` 領域では、このドキュメントを実行手順の正本とし、系列・版数・タグのルールは [ReleaseProcess](ReleaseProcess.md)、GitHub Release Notes の運用は [ReleaseNotes](ReleaseNotes.md) に分離します。

## このドキュメントの役割

- GitHub Actions workflow の役割と生成物を整理する
- Dev / RC / Archive / Store の各チャネルで何を配るかを揃える
- メジャー/マイナー、パッチ、`Release Finalize` 後処理の実行手順をまとめる
- ロールバック/取り下げ時の基本対応を明文化する

このドキュメントでは、ブランチ設計方針と版数・タグのルールは [ReleaseProcess](ReleaseProcess.md)、署名方針は [Signing](../distribution/Signing.md) を正本とします。
Partner Center / listing の実務手順は [StoreSubmission](../distribution/store/StoreSubmission.md)、GitHub Release Notes の具体運用は [ReleaseNotes](ReleaseNotes.md) に分離し、ここでは扱いません。

## ワークフロー一覧

| ワークフロー | トリガー | 用途 | 主な生成物 |
| ------------------------------------------------------------ | ------------------------------------------------------------ | ------------------------------------------------------------ | ------------------------------------------------------------ |
| [pr-check.yml](../../.github/workflows/pr-check.yml) | PR（`main`, `release/*`） | 品質ゲート | `TestResults/**/*.trx` |
| [deploy-pages.yml](../../.github/workflows/deploy-pages.yml) | `main` push（`site/**`） / 手動（`main` のみ） | GitHub Pages 公開 | GitHub Pages site artifact |
| [prepare-release.yml](../../.github/workflows/prepare-release.yml) | 手動（`X.Y`。内部で `X.Y.0` に展開） | `release/X.Y` 作成 + `Release Notes: X.Y` の自動確保 + `main` 側 bump ブランチ作成、任意で PR 作成 | `release/X.Y`, `chore/bump-main-to-*` |
| [prepare-patch-release.yml](../../.github/workflows/prepare-patch-release.yml) | 手動（`release/X.Y`） | patch init ブランチ作成、任意で PR 作成 | `chore/release-X.Y.(Z+1)-init` |
| [dev-build.yml](../../.github/workflows/dev-build.yml) | `main` push（`docs/**`, `*.md`, `site/**`, `.github/workflows/deploy-pages.yml` のみ変更時は除く） / 手動 | 開発成果物生成（未署名） | `dev-package-*`, `dev-latest`, `SHA256SUMS.txt`, GitHub 上に記録される Artifact Attestation |
| [rc-build.yml](../../.github/workflows/rc-build.yml) | `release/*` push（`docs/**`, `*.md`, `site/**`, `.github/workflows/deploy-pages.yml` のみ変更時は除く） / 手動 | 公開候補生成（未署名） | `rc-package-*`, `rc-X.Y-latest`, `SHA256SUMS.txt`, GitHub 上に記録される Artifact Attestation |
| [release-finalize.yml](../../.github/workflows/release-finalize.yml) | `X.Y.Z` タグ push / 手動 | version tag `X.Y.Z` と archive を整備し、必要時のみ Store package を生成する。Store 提出前の手動実行では `release/X.Y` の現在 HEAD へ tag を寄せ直せる | `release-archive-*`, GitHub Release `X.Y.Z`, `SHA256SUMS.txt`, GitHub 上に記録される Artifact Attestation, 任意で `store-package-*` |

補足:

- 配布対象は `*.msixbundle`（未署名）、Store 提出対象は `.msixupload`。
- Dev/RC/Archive 配布では `*.msixbundle` と `SHA256SUMS.txt` を公開し、同じ workflow 実行に対する GitHub Artifact Attestation を GitHub 上に記録する。
- GitHub Artifact Attestation は Release asset として添付せず、`gh attestation verify` で検証する。
- `.NET` SDK の解決はリポジトリ直下の `global.json` を単一の正本とし、workflow の `actions/setup-dotnet` は `global-json-file: global.json` を参照する。
- `pr-check.yml` は workflow lint を常時実行し、website-only PR（`site/**`, `.github/workflows/deploy-pages.yml`, `docs/presentation/LandingPage.md` のみ変更）のときはアプリ本体の restore / security / build / test / spec coverage を skip する。
- `deploy-pages.yml` は `workflow_dispatch` でも `main` 以外では失敗するため、手動実行は `main` を前提とする。
- `rc-X.Y-latest` の GitHub Release は候補版として常に `prerelease` 表示にする。
- `Prepare Release` は `release/X.Y` が既に存在していても、その branch を再利用して継続できる。`main` 側 bump ブランチだけが欠けている rerun では、その branch を再作成して PR 作成まで進める。

## 実行前チェック

1. 実行対象ブランチ（`main` または `release/X.Y`）と対象版が意図どおりであることを確認する。
2. `./scripts/assert-version-policy.ps1` が成功することを確認する。
3. `./scripts/run-tests.ps1` と `./scripts/run-security-checks.ps1` が成功することを確認する。
4. チャネル別の署名方針（Dev/RC は未署名許容、Stable は Store 正本）を理解したうえで、検証対象を明確化する（詳細は [../distribution/Signing.md](../distribution/Signing.md)）。

## 成果物チャネル

| チャネル | 配布元 | 用途 |
| -------- | ------------------------------------------------------------ | ---------------------------------- |
| Dev | `dev-latest` / `dev-package-*` + `SHA256SUMS.txt` | 検証配布（未署名） |
| RC | `rc-X.Y-latest` / `rc-package-*` + `SHA256SUMS.txt` | 公開候補比較（未署名） |
| Archive | GitHub Release `X.Y.Z` / `release-archive-*` + `SHA256SUMS.txt` | 確定版の固定配布・再検証（未署名） |
| Store | `store-package-*`（`.msixupload`） | Partner Center 提出 |

補足:

- Dev/RC/Archive の各チャネルには GitHub Artifact Attestation が同じ workflow 実行に紐づいて記録されるが、attestation 自体は配布 asset に同梱しない。

## 実運用手順

前提となる系列・タグ・チャネルの意味、latest-only、Finalize と Store 公開の関係は [ReleaseProcess](ReleaseProcess.md) を参照してください。
この章では実行手順だけを扱います。

### Release Finalize 後の後処理

1. `Release Finalize` の成功を確認し、GitHub Release `X.Y.Z` に `*.msixbundle` と `SHA256SUMS.txt` が揃っていること、同じ workflow 実行に対する GitHub Artifact Attestation が `gh attestation verify` で検証可能であることを確認する。
2. Store 提出前に追加修正や archive の差し替えが必要になった場合は、`Release Finalize` を `release/X.Y` から手動再実行する。通常は既定値のままでよく、`X.Y.Z` は現在 HEAD へ付け直され、archive もその commit に揃え直される。
3. `Release Notes: X.Y` を更新しただけなら `Release Finalize` の再実行は不要。GitHub Release は issue 参照だけを持つ。
4. Store へ進める条件は [ReleaseProcess](ReleaseProcess.md) の「Store チャネルへの進行条件」を正本とし、条件を満たす場合のみ `Release Finalize` を `release/X.Y` から実行する。通常は既定値のまま進めてよい。
5. Partner Center へ提出したら、[StoreSubmission](../distribution/store/StoreSubmission.md) に従って `Store Submission Log` を GitHub Release に記録する。ここで `X.Y.Z` は固定化される。
6. 公開直後の短い監視期間を終えたら、通常の RC 更新は止めてよい。次の変更が必要になるまでは branch を静置する。
7. 現行系列判定や旧系列の扱いは [ReleaseProcess](ReleaseProcess.md) に従う。

### メジャー/マイナーリリース

1. `Prepare Release`（推奨、workflow 入力は `X.Y`。内部で `X.Y.0` に展開）または `create-release-branch.ps1`（`-Version X.Y`）で `release/X.Y` を作成する。
2. 必要に応じて `next_main_version` / `-NextMainVersion` を指定し、`main` を次の近接系列ではなく将来系列（例: `0.5.0`）へ進める。
3. `chore/bump-main-to-* -> main` の PR をレビューしてマージする。`Prepare Release` workflow は既定でこの PR を自動作成する。
4. `Prepare Release` workflow が `Release Notes: X.Y` Issue を自動作成または再利用する。`create-release-branch.ps1` だけを使う場合は手動で作成または更新する。
5. `Release Notes: Unreleased` から今回の系列で出す bullet を `Release Notes: X.Y` へ手動で移す。
6. `release/X.Y` の安定化を PR で反映する。
7. `rc-X.Y-latest` と複数の公開候補（`rc-package-*`）を比較し、確定対象コミットを決定する。
8. tag 前に `Release Notes: X.Y` Issue を見直し、今回の出荷内容として読めるよう公開向け文面を整える。
9. 確定版を決め、その commit に version tag `X.Y.Z` を作成する。
10. タグ push で `Release Finalize` が走り、GitHub Release `X.Y.Z` にアーカイブ成果物が保存されたことを確認する。
11. Store 提出前に追加修正が必要になった場合は、`Release Finalize` を `release/X.Y` から既定値のまま再実行する。`X.Y.Z` は現在 HEAD へ付け直され、archive も更新される。
12. `Release Notes: X.Y` を更新しただけなら `Release Finalize` の再実行は不要。GitHub Release は issue 参照だけを持つ。
13. Store へ進める条件は [ReleaseProcess](ReleaseProcess.md) の「Store チャネルへの進行条件」を参照し、条件を満たす場合のみ `Release Finalize` を `release/X.Y` から実行して Store package を作成する。通常は既定値のまま進めてよい。
14. Store 提出は [../distribution/store/StoreSubmission.md](../distribution/store/StoreSubmission.md) の手順に従って実行し、提出直後に `Store Submission Log` を記録する。

### パッチリリース

対象は [ReleaseProcess](ReleaseProcess.md) で定義した現行サポート系列のみとする。

1. [ReleaseProcess](ReleaseProcess.md) の「パッチリリース」にある patch init 前提を確認したうえで、`Prepare Patch Release`（推奨）または `create-patch-release-branch.ps1` で patch init ブランチを作成する。
2. patch init PR（`chore/release-X.Y.(Z+1)-init -> release/X.Y`）をマージする。`Prepare Patch Release` workflow は既定でこの PR を自動作成する。
3. 不具合修正を `main` へマージする。
4. `release/X.Y` をベースにした `fix/*` backport ブランチで必要コミットを `cherry-pick -x` し、PR で `release/X.Y` へ反映する。
   - 競合時の解消方針は [ReleaseProcess](ReleaseProcess.md) の「backport 競合解消方針」に従う。
5. 候補ビルドから確定対象コミットを決定し、確定版を決める。
6. tag 前に `Release Notes: X.Y` Issue を見直し、今回の patch 版として読めるよう公開向け文面を整える。
7. その commit に version tag `X.Y.Z` を作成する。
8. タグ push で `Release Finalize` が走り、GitHub Release `X.Y.Z` にアーカイブ成果物が保存されたことを確認する。
9. Store 提出前に追加修正が必要になった場合は、`Release Finalize` を `release/X.Y` から既定値のまま再実行する。`X.Y.Z` は現在 HEAD へ付け直され、archive も更新される。
10. `Release Notes: X.Y` を更新しただけなら `Release Finalize` の再実行は不要。必要時のみ `release/X.Y` から手動再実行して GitHub Release を更新する。
11. Store へ進める条件は [ReleaseProcess](ReleaseProcess.md) の「Store チャネルへの進行条件」を参照し、条件を満たす場合のみ `Release Finalize` を `release/X.Y` から実行する。通常は既定値のまま進めてよい。
12. Store 提出へ進み、提出直後に `Store Submission Log` を記録する。

### Store 提出

このドキュメントで扱うのは `store-package-*` の生成までとする。
Partner Center への upload、listing CSV import、審査向け補足、submission ID 記録は [StoreSubmission](../distribution/store/StoreSubmission.md) を正本とする。提出直後に GitHub Release の `Store Submission Log` を更新すると、その時点で `X.Y.Z` は固定化される。

## ロールバック/取り下げ

### Dev / RC 配布物

1. 問題のある配布リンクを停止または更新する。
2. `main`（必要なら `release/X.Y`）へ復旧 PR を反映する。
3. 修正版を再ビルドして差し替える。
4. 確定版 GitHub Release `X.Y.Z` に問題がある場合は削除せず、取り下げ理由を本文に追記し、次版を作る。

### Store 提出後

1. Partner Center 側で該当提出の公開を停止/取り下げする。
2. 必要なら最新のサポート対象 `release/X.Y` で次のパッチ版を準備する。
3. 新版 `.msixupload` を再提出する。

## 補足

### なぜ Store 提出を手動運用にするか

| 理由 | 内容 |
| -------------- | ---------------------------- |
| 審査プロセス | Microsoft 側審査が必要 |
| メタデータ更新 | 説明文・画像更新に人手が必要 |
| リスク管理 | 段階的リリース判断が必要 |

### 便利コマンド

```powershell
.\scripts\show-version-report.ps1
.\scripts\assert-version-policy.ps1 -BranchName release/1.3
.\scripts\run-tests.ps1 -Configuration Release
.\scripts\run-security-checks.ps1 -Configuration Release
.\scripts\store-checklist.ps1 -Version X.Y.Z
.\scripts\verify-artifact.ps1 -BundlePath .\ClipSave.Package_X.Y.Z.W_AnyCPU.msixbundle -Channel dev -SourceRef refs/heads/main
```

## 関連ドキュメント

- [ReleaseProcess](ReleaseProcess.md) — 系列、タグ、チャネル、Finalize の全体像
- [StoreSubmission](../distribution/store/StoreSubmission.md) — Partner Center 提出、listing 運用、提出記録
- [ReleaseNotes](ReleaseNotes.md) — GitHub Release Notes 運用
- [Signing](../distribution/Signing.md) — 署名方針
- [ArtifactInstallation](../distribution/ArtifactInstallation.md) — 未署名アーティファクトの検証・導入手順
- [IconAssets](../presentation/IconAssets.md) — アイコン運用
- [GitHub Releases](https://github.com/tnagata012/ClipSave/releases) — 公開 release notes と配布アーカイブ
