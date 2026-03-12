# リリースガイド

  ClipSave の CI/CD と配布実行手順を定義します。

  ## この文書の責務

  この文書では、以下を扱います。

  - GitHub Actions workflow の役割
  - 配布チャネルごとの成果物
  - メジャー/マイナー、パッチの実行手順
  - ロールバック/取り下げ手順

  対象読者:

  - リリース担当
  - 配布運用担当
  - GitHub Actions の保守担当

  この文書では、以下は扱いません。

  - ブランチ設計方針、版数、タグのルール（[ReleaseProcess](ReleaseProcess.md)）
  - 署名方針（[../distribution/Signing.md](../distribution/Signing.md)）
  - Partner Center / listing の実務手順（[../distribution/store/StoreSubmission.md](../distribution/store/StoreSubmission.md)）
  - CHANGELOG の具体的な記法（[ReleaseNotes](ReleaseNotes.md)）

  ## ワークフロー一覧

  | ワークフロー                                                 | トリガー                                                     | 用途                                                         | 主な生成物                                                   |
  | ------------------------------------------------------------ | ------------------------------------------------------------ | ------------------------------------------------------------ | ------------------------------------------------------------ |
  | [pr-check.yml](../../.github/workflows/pr-check.yml)         | PR（`main`, `release/*`）                                    | 品質ゲート                                                   | `TestResults/**/*.trx`                                       |
  | [deploy-pages.yml](../../.github/workflows/deploy-pages.yml) | `main` push（`site/**`, `.github/workflows/deploy-pages.yml`） / 手動（`main` のみ） | GitHub Pages 公開                                            | GitHub Pages site artifact                                   |
  | [prepare-release-branch.yml](../../.github/workflows/prepare-release-branch.yml) | 手動（`X.Y.0`）                                              | `release/X.Y` 作成 + `main` 側 bump ブランチ作成、任意で PR 作成 | `release/X.Y`, `chore/bump-main-to-*`                        |
  | [prepare-patch-release.yml](../../.github/workflows/prepare-patch-release.yml) | 手動（`release/X.Y`）                                        | patch init ブランチ作成、任意で PR 作成                      | `chore/release-X.Y.(Z+1)-init`                               |
  | [dev-build.yml](../../.github/workflows/dev-build.yml)       | `main` push（`docs/**`, `*.md`, `site/**`, `.github/workflows/deploy-pages.yml` のみ変更時は除く） / 手動 | 開発成果物生成（未署名）                                     | `dev-package-*`, `dev-latest`, `SHA256SUMS.txt`, GitHub 上に記録される Artifact Attestation |
  | [rc-build.yml](../../.github/workflows/rc-build.yml)         | `release/*` push（`site/**`, `.github/workflows/deploy-pages.yml`, `docs/presentation/LandingPage.md` のみ変更時は除く） / 手動 | 公開候補生成（未署名）                                       | `rc-package-*`, `rc-X.Y-latest`, `SHA256SUMS.txt`, GitHub 上に記録される Artifact Attestation |
  | [release-finalize.yml](../../.github/workflows/release-finalize.yml) | `X.Y.Z` タグ push / 手動                                     | 確定タグを固定参照してアーカイブ再生成、GitHub Release メタデータ調整、任意の Store package 生成 | `release-archive-*`, GitHub Release `X.Y.Z`, `SHA256SUMS.txt`, GitHub 上に記録される Artifact Attestation, 任意で `store-package-*` |

  補足:

  - 配布対象は `*.msixbundle`（未署名）、Store 提出対象は `.msixupload`。
  - Dev/RC/Archive 配布では `*.msixbundle` と `SHA256SUMS.txt` を公開し、同じ workflow 実行に対する GitHub Artifact Attestation を GitHub 上に記録する。
  - GitHub Artifact Attestation は Release asset として添付せず、`gh attestation verify` で検証する。
  - `.NET` SDK の解決はリポジトリ直下の `global.json` を単一の正本とし、workflow の `actions/setup-dotnet` は `global-json-file: global.json` を参照する。
  - `pr-check.yml` は workflow lint を常時実行し、website-only PR（`site/**`, `.github/workflows/deploy-pages.yml`, `docs/presentation/LandingPage.md` のみ変更）のときは restore / build / test を skip する。
  - `deploy-pages.yml` は `workflow_dispatch` でも `main` 以外では失敗するため、手動実行は `main` を前提とする。
  - `dev-latest` と `rc-X.Y-latest` は確定タグではなく移動タグ（floating tag）として運用し、各 workflow 成功時に実行コミットへ更新する。
  - `release/X.Y` ブランチの配布タグは `rc-X.Y-latest`（例: `release/1.3` → `rc-1.3-latest`）。
  - `rc-X.Y-latest` の GitHub Release は候補版として常に `prerelease` 表示にする。
  - 確定タグは `X.Y.Z` で作成し、作成後は移動しない。
  - `Release Finalize` は確定タグを固定参照してアーカイブ成果物を再生成し、GitHub Release `X.Y.Z` を更新する。
  - `Release Finalize` は既存の確定タグ `X.Y.Z` に対しても `workflow_dispatch` で後追い実行できる。
  - `Release Finalize` を手動再実行すると、アーカイブ assets を再生成したうえで `prerelease` / タイトル / `Operator Notes` を調整できる。`Store Submission Log` は本文中で保持する。
  - `Release Finalize` に `build_store_package=true` を指定した場合のみ `.msixupload` を生成する。
  - `PATCH` 更新規約は [ReleaseProcess](ReleaseProcess.md) を正本とする。
  - `store-package-*` 生成後の Partner Center 実務は [../distribution/store/StoreSubmission.md](../distribution/store/StoreSubmission.md) を正本とする。

  ## 実行前チェック

  1. `main` / `release/X.Y` への直 push を行わない運用であることを確認する。
  2. 実行対象ブランチ（`main` または `release/X.Y`）が意図どおりであることを確認する。
  3. `./scripts/assert-version-policy.ps1` が成功することを確認する。
  4. `./scripts/run-tests.ps1` と `./scripts/run-security-checks.ps1` が成功することを確認する。
  5. チャネル別の署名方針（Dev/RC は未署名許容、Stable は Store 正本）を理解したうえで、検証対象を明確化する（詳細は [../distribution/Signing.md](../distribution/Signing.md)）。

  ## 成果物チャネル

  | チャネル | 配布元                                                       | 用途                               |
  | -------- | ------------------------------------------------------------ | ---------------------------------- |
  | Dev      | `dev-latest` / `dev-package-*` + `SHA256SUMS.txt`            | 検証配布（未署名）                 |
  | RC       | `rc-X.Y-latest` / `rc-package-*` + `SHA256SUMS.txt`          | 公開候補比較（未署名）             |
  | Archive  | GitHub Release `X.Y.Z` / `release-archive-*` + `SHA256SUMS.txt` | 確定版の固定配布・再検証（未署名） |
  | Store    | `store-package-*`（`.msixupload`）                           | Partner Center 提出                |

  補足:

  - Dev/RC/Archive の各チャネルには GitHub Artifact Attestation が同じ workflow 実行に紐づいて記録されるが、attestation 自体は配布 asset に同梱しない。

  ## 実運用手順

  前提となる系列・タグ・チャネルの意味、latest-only、Finalize と Store 公開の関係は [ReleaseProcess](ReleaseProcess.md) を参照してください。
  この章では実行手順だけを扱います。

  主要な実行経路だけを抜き出すと、全体像は次のとおりです。

  ```mermaid
  flowchart TD
    start[リリース開始] --> kind{開始種別}
    kind -->|新系列| prepRelease[Prepare Release Branch]
    kind -->|パッチ| prepPatch[Prepare Patch Release]
    prepRelease --> stabilize[release/X.Y を安定化し RC を比較]
    prepPatch --> backport[main 修正を backport して候補を比較]
    stabilize --> tag[確定タグを作成]
    backport --> tag
    tag --> finalize[Release Finalize]
    finalize --> archive[GitHub Release X.Y.Z / release-archive-*]
    finalize --> storeCheck{一般ユーザー向け<br/>かつ latest finalized?}
    storeCheck -->|yes| store[store-package-* を生成して Store Submission]
    storeCheck -->|no| done[Archive-only で完了]
  ```

  ### Release Finalize 後の後処理

  1. `Release Finalize` の成功を確認し、GitHub Release `X.Y.Z` に `*.msixbundle` と `SHA256SUMS.txt` が揃っていること、同じ workflow 実行に対する GitHub Artifact Attestation が `gh attestation verify` で検証可能であることを確認する。
  2. GitHub Release の見せ方を調整する必要がある場合のみ、`Release Finalize` を手動再実行する。手動再実行は metadata だけでなくアーカイブ assets も確定タグから再生成するため、`prerelease` / タイトル / `Operator Notes` の調整と asset refresh を同時に行う操作として扱う。
  3. その版が repository 全体の最新 finalized version であり、かつ一般ユーザー向けに出す場合のみ、`Release Finalize` を `build_store_package=true` で再実行し、以後は [../distribution/store/StoreSubmission.md](../distribution/store/StoreSubmission.md) のガイドに従う。Store へ出さない版は「Archive のみ」で完結してよい。
  4. 公開直後の短い監視期間を終えたら、通常の RC 更新は止めてよい。次の変更が必要になるまでは branch を静置する。
  5. 現行系列判定や旧系列の扱いは [ReleaseProcess](ReleaseProcess.md) に従う。

  ### メジャー/マイナーリリース

  1. `Prepare Release Branch`（推奨）または `create-release-branch.ps1` で `release/X.Y` を作成する。
  2. 必要に応じて `next_main_version` / `-NextMainVersion` を指定し、`main` を次の近接系列ではなく将来系列（例: `0.5.0`）へ進める。
  3. `chore/bump-main-to-* -> main` の PR をレビューしてマージする。`Prepare Release Branch` workflow は既定でこの PR を自動作成する。
  4. `release/X.Y` の安定化を PR で反映する。
  5. `rc-X.Y-latest` と複数の公開候補（`rc-package-*`）を比較し、確定対象コミットを決定する。
  6. tag 前に最後にマージする `release/X.Y` 側 PR（通常は最終安定化 PR または RC 用 PR）で、`CHANGELOG.md` の今回出荷分を `[Unreleased]` から `## [X.Y.Z] - YYYY-MM-DD` へ移す。
  7. 確定版を決め、その commit に確定タグ `X.Y.Z` を作成する。
  8. タグ push で `Release Finalize` が走り、GitHub Release `X.Y.Z` にアーカイブ成果物が保存されたことを確認する。
  9. GitHub Release の見せ方を調整したい場合は、`Release Finalize` を手動再実行して `prerelease` / タイトル / `Operator Notes` を更新する。再実行時はアーカイブ assets も確定タグから再生成される。
  10. その版が repository 全体の最新 finalized version であり、一般ユーザー向けに出す場合のみ、`Release Finalize` を `build_store_package=true` で実行して Store package を作成する。
  11. Store 提出は [../distribution/store/StoreSubmission.md](../distribution/store/StoreSubmission.md) の手順に従って実行する。

  ### パッチリリース

  対象は [ReleaseProcess](ReleaseProcess.md) で定義した現行サポート系列のみとする。

  1. `Prepare Patch Release`（推奨）または `create-patch-release-branch.ps1` で patch init ブランチを作成する。
     - この時点で現行版 `X.Y.Z` の確定タグと GitHub Release `X.Y.Z` が存在し、`release/X.Y` HEAD がその確定コミットにあることを確認する。
     - 既存タグに GitHub Release が無い場合は、先に `Release Finalize` を `version=X.Y.Z`, `build_store_package=false` で手動実行する。
  2. patch init PR（`chore/release-X.Y.(Z+1)-init -> release/X.Y`）をマージする。`Prepare Patch Release` workflow は既定でこの PR を自動作成する。
  3. 不具合修正を `main` へマージする。
  4. `release/X.Y` をベースにした `fix/*` backport ブランチで必要コミットを `cherry-pick -x` し、PR で `release/X.Y` へ反映する。
     - 競合時の解消方針は [ReleaseProcess](ReleaseProcess.md) の「backport 競合解消方針」に従う。
  5. 候補ビルドから確定対象コミットを決定し、確定版を決める。
  6. tag 前に最後にマージする `release/X.Y` 側 PR（通常は patch init PR または最終 backport PR）で、`CHANGELOG.md` の今回出荷分だけを `[Unreleased]` から `## [X.Y.Z] - YYYY-MM-DD` へ移す。
  7. その commit に確定タグ `X.Y.Z` を作成する。
  8. タグ push で `Release Finalize` が走り、GitHub Release `X.Y.Z` にアーカイブ成果物が保存されたことを確認する。
  9. 必要なら `Release Finalize` を手動再実行して GitHub Release の表示メタデータを更新する。再実行時はアーカイブ assets も確定タグから再生成される。
  10. その版が repository 全体の最新 finalized version である場合のみ、`Release Finalize` を `build_store_package=true` で実行する。
  11. Store 提出へ進む。

  ### Store 提出

  release 文書側で扱うのは `store-package-*` の生成までとする。
  Partner Center への upload、listing CSV import、審査向け補足、submission ID 記録は [StoreSubmission](../distribution/store/StoreSubmission.md) を正本とする。

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

  | 理由           | 内容                         |
  | -------------- | ---------------------------- |
  | 審査プロセス   | Microsoft 側審査が必要       |
  | メタデータ更新 | 説明文・画像更新に人手が必要 |
  | リスク管理     | 段階的リリース判断が必要     |

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
  - [ReleaseNotes](ReleaseNotes.md) — `CHANGELOG.md` 運用
  - [Signing](../distribution/Signing.md) — 署名方針
  - [ArtifactInstallation](../distribution/ArtifactInstallation.md) — 未署名アーティファクトの検証・導入手順
  - [IconAssets](../presentation/IconAssets.md) — アイコン運用
  - [CHANGELOG](../../CHANGELOG.md) — 変更履歴
