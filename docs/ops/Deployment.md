# デプロイ

ClipSave の CI/CD と配布実行手順（Runbook）を定義します。

## この文書の責務

この文書では、以下を扱います。

- GitHub Actions ワークフローの役割
- 配布チャネルごとの成果物
- メジャー/マイナー、パッチの実行手順
- ロールバック/取り下げ手順

この文書では、以下は扱いません。

- ブランチ設計方針（`BranchStrategy.md`）
- 版数フォーマットや `PATCH` 規約の定義（`Versioning.md`）
- 署名方針（`Signing.md`）
- Store 提出の実務手順（`../store/Submission.md`）

## ワークフロー一覧

| ワークフロー | トリガー | 用途 | 主な生成物 |
|-------------|---------|------|-----------|
| [pr-check.yml](../../.github/workflows/pr-check.yml) | PR（`main`, `release/*`） | 品質ゲート | `TestResults/**/*.trx` |
| [deploy-pages.yml](../../.github/workflows/deploy-pages.yml) | `main` push（`site/**`, `.github/workflows/deploy-pages.yml`） / 手動 | GitHub Pages 公開 | GitHub Pages site artifact |
| [prepare-release-branch.yml](../../.github/workflows/prepare-release-branch.yml) | 手動（`X.Y.0`） | `release/X.Y` 作成 + main 側 bump ブランチ作成 | `release/X.Y`, `chore/bump-main-to-*` |
| [prepare-patch-release.yml](../../.github/workflows/prepare-patch-release.yml) | 手動（`release/X.Y`） | patch init ブランチ作成 | `chore/release-X.Y.(Z+1)-init` |
| [dev-build.yml](../../.github/workflows/dev-build.yml) | `main` push（`docs/**`, `*.md`, `site/**`, `.github/workflows/deploy-pages.yml` のみ変更時は除く） / 手動 | 開発成果物生成（未署名） | `dev-package-*`, `dev-latest`, `SHA256SUMS.txt`, `GitHub Artifact Attestation` |
| [rc-build.yml](../../.github/workflows/rc-build.yml) | `release/*` push（`site/**`, `.github/workflows/deploy-pages.yml`, `docs/presentation/LandingPage.md` のみ変更時は除く） / 手動 | 公開候補生成（未署名） | `rc-package-*`, `rc-X.Y-latest`, `SHA256SUMS.txt`, `GitHub Artifact Attestation` |
| [release-finalize.yml](../../.github/workflows/release-finalize.yml) | `X.Y.Z` タグ push / 手動 | 確定版アーカイブ生成（未署名）、GitHub Release メタデータ調整、任意の Store package 生成 | `release-archive-*`, GitHub Release `X.Y.Z`, `SHA256SUMS.txt`, `GitHub Artifact Attestation`, 任意で `store-package-*` |

補足:

- 配布対象は `*.msixbundle`（未署名）、Store 提出対象は `.msixupload`。
- Dev/RC 配布では `*.msixbundle` と `SHA256SUMS.txt` をセットで公開し、GitHub Artifact Attestation を記録する。
- `.NET` SDK の解決はリポジトリ直下の `global.json` を単一の正本とし、workflow の `actions/setup-dotnet` は `global-json-file: global.json` を参照する。
- `pr-check.yml` は workflow lint を常時実行し、website-only PR（`site/**`, `.github/workflows/deploy-pages.yml`, `docs/presentation/LandingPage.md` のみ変更）のときは restore / build / test を skip する。
- `dev-latest` と `rc-X.Y-latest` は確定タグではなく移動タグ（floating tag）として運用し、各 workflow 成功時に実行コミットへ更新する。
- `release/X.Y` ブランチの配布タグは `rc-X.Y-latest`（例: `release/1.3` -> `rc-1.3-latest`）。
- `rc-X.Y-latest` の GitHub Release は候補版として常に `prerelease` 表示にする。
- 確定タグは `X.Y.Z` で作成し、作成後は移動しない。
- `Release Finalize` は確定タグから不変アーティファクトを GitHub Release `X.Y.Z` に保存する。
- `Release Finalize` は既存の確定タグ `X.Y.Z` に対しても `workflow_dispatch` で後追い実行できる。
- `Release Finalize` を手動再実行すると、`prerelease` / タイトル / `Operator Notes` を調整できる。`Store Submission Log` は本文中で保持する。
- `Release Finalize` に `build_store_package=true` を指定した場合のみ `.msixupload` を生成する。
- `PATCH` 更新規約は `Versioning.md` を正本とする。
- Store 提出運用の詳細は `../store/Submission.md` を正本とする。

## 実行前チェック

1. `main` / `release/X.Y` への直 push を行わない運用であることを確認する。
2. 実行対象ブランチ（`main` または `release/X.Y`）が意図どおりであることを確認する。
3. `./scripts/assert-version-policy.ps1` が成功することを確認する。
4. `./scripts/run-tests.ps1` と `./scripts/run-security-checks.ps1` が成功することを確認する。
5. チャネル別の署名方針（Dev/RC は未署名許容、Stable は Store 正本）を理解したうえで、検証対象を明確化する（詳細は `Signing.md`）。

## 成果物チャネル

| チャネル | 配布元 | 用途 |
|---------|-------|------|
| Dev | `dev-latest` / `dev-package-*` + `SHA256SUMS.txt` + `GitHub Artifact Attestation` | 検証配布（未署名） |
| RC | `rc-X.Y-latest` / `rc-package-*` + `SHA256SUMS.txt` + `GitHub Artifact Attestation` | 公開候補比較（未署名） |
| Archive | GitHub Release `X.Y.Z` / `release-archive-*` + `SHA256SUMS.txt` + `GitHub Artifact Attestation` | 確定版の固定配布・再検証（未署名） |
| Store | `store-package-*`（`.msixupload`） | Partner Center 提出 |

## 実運用手順

### 確定版と Store 公開の関係

- 版の確定は `X.Y.Z` 確定タグの作成までを指す。
- Store 公開は確定版の後段にある任意工程であり、すべての確定版で必須ではない。
- `0.1.0` や `0.1.1` のような試験リリースは、確定タグ付与と `Release Finalize` のアーカイブ処理で止めてよい。
- `Release Finalize` 導入前に付けた既存の確定タグ（例: すでに存在する `0.1.1`）も、手動実行で後追いアーカイブ化してよい。
- 一般ユーザー向けに出す版だけ `Release Finalize` の Store package mode を実行する。対象は常に最新 finalized version のみとし、同一系列内の古い確定タグは archive-only とする。
- ClipSave は latest-only 運用とし、運用上のサポート対象は常に最新 finalized 系列だけとする。ここでいう切替基準は Store 公開有無ではなく Finalize 完了であり、新しい系列を archive-only で Finalize した場合も、その時点で旧系列は `frozen / unsupported` に移す。
- latest-only は運用ルールとして扱い、PR / RC / patch init を workflow で一律停止しない。自動で hard gate を置くのは `Release Finalize` の Store package mode だけとする。

### Release Finalize 後の後処理

1. `Release Finalize` の成功を確認し、GitHub Release `X.Y.Z` に `*.msixbundle`、`SHA256SUMS.txt`、GitHub Artifact Attestation が揃っていることを確認する。
2. GitHub Release の見せ方を調整する必要がある場合のみ、`Release Finalize` を手動再実行して `prerelease` / タイトル / `Operator Notes` を更新する。
3. その版が repository 全体の最新 finalized version であり、かつ一般ユーザー向けに出す場合のみ、`Release Finalize` を `build_store_package=true` で再実行し、以後は `../store/Submission.md` の Runbook に従う。Store へ出さない版は「Archive のみ」で完結してよい。
4. その系列が最新 finalized 系列である間だけ、`release/X.Y` を現行サポート系列として維持し、必要時に patch release、最新 finalized version に対する Store package 再生成、`Release Finalize` 再実行を行う。Store 公開の有無はこの判定に使わない。
5. 公開直後の短い監視期間を終えたら、通常の RC 更新は止めてよい。次の変更が必要になるまでは branch を静置する。
6. 新しい系列 `release/A.B` で最初の確定タグ `A.B.Z` を作成し Finalize した時点で、それ以前の `release/X.Y` は即時に `frozen / unsupported` とする。これは新系列をまだ Store へ出していない場合でも同じで、旧系列の通常 patch、RC 更新、Store 再提出は再開しない。
7. 旧系列の既存確定タグ `X.Y.Z` に対する `Release Finalize` 再実行は、アーカイブ成果物や GitHub Release メタデータの保守に限って例外的に許容する。Store package mode は使わず、旧系列サポートの再開ともみなさない。
8. 旧系列 branch は追跡と上記の限定的な workflow 再実行余地を保つため、remote に残す。

### メジャー/マイナーリリース

1. `Prepare Release Branch`（推奨）または `create-release-branch.ps1` で `release/X.Y` を作成する。
2. 必要に応じて `next_main_version` / `-NextMainVersion` を指定し、`main` を次の近接系列ではなく将来系列（例: `0.5.0`）へ進める。
3. `chore/bump-main-to-* -> main` の PR をレビューしてマージする。
4. `release/X.Y` の安定化を PR で反映する。
5. `rc-X.Y-latest` と複数の公開候補（`rc-package-*`）を比較し、確定対象コミットを決定する。
   - 既存の finalized 系列が別に存在していても、新系列 `release/X.Y` の最初の Finalize 前であれば、この安定化 PR と RC 比較は継続してよい。
6. tag 前に最後にマージする `release/X.Y` 側 PR（通常は最終安定化 PR または RC 用 PR）で、`CHANGELOG.md` の今回出荷分を `[Unreleased]` から `## [X.Y.Z] - YYYY-MM-DD` へ移す。
7. 確定版を決め、その commit に確定タグ `X.Y.Z` を作成する。
8. タグ push で `Release Finalize` が走り、GitHub Release `X.Y.Z` にアーカイブ成果物が保存されたことを確認する。
9. GitHub Release の見せ方を調整したい場合は、`Release Finalize` を手動再実行して `prerelease` / タイトル / `Operator Notes` を更新する。
10. その版が repository 全体の最新 finalized version であり、一般ユーザー向けに出す場合のみ、`Release Finalize` を `build_store_package=true` で実行して Store package を作成する。
11. Store 提出は `../store/Submission.md` の手順に従って実行する。

### パッチリリース

対象は最新の能動サポート系列のみとする。新しい系列が既に Finalize 済みの旧系列では patch release を行わない。
この latest-only 判定は運用で担保し、patch init や RC の workflow では一律ブロックしない。一般配布用の Store package を出す段階で最終確認する。

1. `Prepare Patch Release`（推奨）または `create-patch-release-branch.ps1` で patch init ブランチを作成する。
   - この時点で現行版 `X.Y.Z` の確定タグと GitHub Release `X.Y.Z` が存在し、`release/X.Y` HEAD がその確定コミットにあることを確認する。
   - 既存タグに GitHub Release が無い場合は、先に `Release Finalize` を `version=X.Y.Z`, `build_store_package=false` で手動実行する。
2. patch init PR（`chore/release-X.Y.(Z+1)-init -> release/X.Y`）をマージする。
3. 不具合修正を `main` へマージする。
4. `release/X.Y` をベースにした `fix/*` backport ブランチで必要コミットを `cherry-pick -x` し、PR で `release/X.Y` へ反映する。
5. 候補ビルドから確定対象コミットを決定し、確定版を決める。
6. tag 前に最後にマージする `release/X.Y` 側 PR（通常は patch init PR または最終 backport PR）で、`CHANGELOG.md` の今回出荷分だけを `[Unreleased]` から `## [X.Y.Z] - YYYY-MM-DD` へ移す。
7. その commit に確定タグ `X.Y.Z` を作成する。
8. タグ push で `Release Finalize` が走り、GitHub Release `X.Y.Z` にアーカイブ成果物が保存されたことを確認する。
9. 必要なら `Release Finalize` を手動再実行して GitHub Release の表示メタデータを更新する。
10. その版が repository 全体の最新 finalized version である場合のみ、`Release Finalize` を `build_store_package=true` で実行する。
11. Store 提出へ進む。

### Store 提出

Store 提出は [StoreSubmission](../store/Submission.md) を正本とし、本書では記載しない。

### 段階的リリース例

1. `0.1.0`: `release/0.1` を作成し、候補比較後に確定タグ `0.1.0` を付ける。`Release Finalize` のアーカイブ処理で成果物を残し、Store package mode は実行しない。
2. `0.1.1`: `release/0.1` 上に既存の確定タグ `0.1.1` があるなら、`Release Finalize` を手動実行してアーカイブを補完する。ここでも Store package mode は実行しない。
3. `0.5.0`: `main` を `0.5.0` 系列へ進めて `release/0.5` を作成し、確定後に確定タグ `0.5.0` を付ける。`Release Finalize` 完了後に `build_store_package=true` で Store package を生成する。

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
.\scripts\store-checklist.ps1 -Version X.Y.Z
.\scripts\verify-artifact.ps1 -BundlePath .\ClipSave.Package_X.Y.Z.W_AnyCPU.msixbundle -Channel dev -SourceRef refs/heads/main
```

## 関連ドキュメント

- [StoreSubmission](../store/Submission.md) — Store 提出の実務手順と listing 運用
- [BranchStrategy](BranchStrategy.md) — ブランチ構成と統合方向
- [Versioning](Versioning.md) — 版数規約
- [ReleaseNotes](ReleaseNotes.md) — CHANGELOG 運用
- [Signing](Signing.md) — 署名方針（チャネル別運用）
- [IconAssets](../presentation/IconAssets.md) — アイコン運用
- [CHANGELOG](../../CHANGELOG.md) — 変更履歴
