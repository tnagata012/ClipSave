# リリースガイド

**このドキュメントの目的**: ClipSave の workflow、成果物、配布実行手順を定義します。
`docs/release` 領域では、このドキュメントを実行手順の正本とし、系列・版数・タグのルールは [ReleaseProcess](ReleaseProcess.md)、GitHub Release Notes の運用は [ReleaseNotes](ReleaseNotes.md) に分離します。

## このドキュメントの役割

- GitHub Actions workflow の役割と生成物を整理する
- Dev / RC / Archive / Store の各チャネルで何を配るかを揃える
- メジャー/マイナー、パッチ、`Release Finalize` 後処理の実行手順をまとめる
- ロールバック / 取り下げ時の基本対応を明文化する

このドキュメントでは、ブランチ設計方針と版数・タグのルールは [ReleaseProcess](ReleaseProcess.md)、署名方針は [Signing](../distribution/Signing.md) を正本とします。
Partner Center / listing の実務手順は [StoreSubmission](../distribution/store/StoreSubmission.md)、GitHub Release Notes の具体運用は [ReleaseNotes](ReleaseNotes.md) に分離し、ここでは扱いません。

## ワークフロー一覧

| ワークフロー | トリガー | 用途 | 主な生成物 |
| ---- | ---- | ---- | ---- |
| [pr-check.yml](../../.github/workflows/pr-check.yml) | PR（`main`, `release/*`） | 品質ゲート | `TestResults/**/*.trx` |
| [deploy-pages.yml](../../.github/workflows/deploy-pages.yml) | `main` push（`site/**`） / 手動（`main` のみ） | GitHub Pages 公開 | GitHub Pages site artifact |
| [prepare-release.yml](../../.github/workflows/prepare-release.yml) | 手動（`X.Y`） | `release/X.Y` 作成 + `Release Notes: X.Y` の自動確保 / 再利用 | `release/X.Y` |
| [dev-build.yml](../../.github/workflows/dev-build.yml) | `main` push / 手動 | 開発成果物生成（未署名） | `dev-package-*`, `dev-latest`, `SHA256SUMS.txt`, GitHub 上に記録される Artifact Attestation |
| [rc-build.yml](../../.github/workflows/rc-build.yml) | `release/*` push / 手動 | 公開候補生成（未署名） | `rc-package-*`, `rc-X.Y-latest`, `SHA256SUMS.txt`, GitHub 上に記録される Artifact Attestation |
| [release-finalize.yml](../../.github/workflows/release-finalize.yml) | 手動（`release/X.Y` を選択して実行） | 選択した `release/X.Y` と手動入力の `patch` から `version=X.Y.Z` を解決し、`rc-X.Y-latest` の最新成功候補を採用して archive / Store package を整備する | `release-archive-*`, GitHub Release `X.Y.Z`, `SHA256SUMS.txt`, 任意で `store-package-*`, GitHub 上に記録される Artifact Attestation |

補足:

- patch line `Z` は `Release Finalize` で決め、修正 PR は `release/X.Y` へ反映する。
- Dev 配布版の package version は `0.0.1.B`、RC は `X.Y.0.B`、Archive / Store は `X.Y.Z.B` を使う。
- RC の `B` は `release/X.Y` branch ごとのカウンターで、新しい release branch を切ると 1 から始まる。
- `main=0.0.1` を preview line として固定し、release branch の `X.Y.0` base version と finalized version line `X.Y.Z` を分けることで、Dev と RC / Archive / Store の役割を見分けやすくする。
- `AssemblyVersion` は配布 build 識別子ではなく互換性用に扱い、実際の配布物識別は `FileVersion` / package version / `InformationalVersion` で行う。
- `dev-latest` / `rc-X.Y-latest` は floating tag として、docs-only を含む各 branch の最新 commit に追随させる。
- `rc-X.Y-latest` の GitHub Release は候補版として常に `prerelease` 表示にする。
- `Release Finalize` は現行サポート系列で、同じ finalized version の rerun または新しい patch line の確定に使う。
- `Release Finalize` の build は手動指定せず、Store 提出前は `rc-X.Y-latest` の最新成功候補を自動採用する。
- `Release Finalize` の `patch` 入力は `X.Y.Z` の `Z` を指す。メジャー / マイナー系列の初回 finalize は `patch=0`、以後の patch release は対象の `Z` を入れる。

## 実行前チェック

1. 実行対象ブランチ（`main` または `release/X.Y`）と対象版が意図どおりであることを確認する。
2. `./scripts/assert-version-policy.ps1` が成功することを確認する。
3. `./scripts/run-tests.ps1` と `./scripts/run-security-checks.ps1` が成功することを確認する。
4. チャネル別の署名方針（Dev / RC は未署名許容、Stable は Store 正本）を理解したうえで、検証対象を明確化する。

## 成果物チャネル

| チャネル | 配布元 | 用途 |
| ---- | ---- | ---- |
| Dev | `dev-latest` / `dev-package-*` + `SHA256SUMS.txt` | 検証配布（未署名、package version=`0.0.1.B`） |
| RC | `rc-X.Y-latest` / `rc-package-*` + `SHA256SUMS.txt` | 公開候補比較（未署名、package version=`X.Y.0.B`） |
| Archive | GitHub Release `X.Y.Z` / `release-archive-*` + `SHA256SUMS.txt` | 確定 build の固定配布 / 再検証（未署名、package version=`X.Y.Z.B`） |
| Store | `store-package-*`（`.msixupload`） | Partner Center 提出 |

## 実運用手順

前提となる系列・タグ・チャネルの意味、latest-only、Finalize と Store 公開の関係は [ReleaseProcess](ReleaseProcess.md) を参照してください。
この章では実行手順だけを扱います。

### Release Finalize 後の後処理

1. `Release Finalize` の成功を確認し、GitHub Release `X.Y.Z` に `*.msixbundle` と `SHA256SUMS.txt` が揃っていること、同じ workflow 実行に対する GitHub Artifact Attestation が `gh attestation verify` で検証可能であることを確認する。
2. Store 提出対象の `.msixupload` は GitHub Release assets ではなく、同じ run の `store-package-*` artifact から取得する。
3. Store 提出前に追加修正や archive の差し替えが必要になった場合は、同じ finalized version `X.Y.Z` に対して `Release Finalize` を `release/X.Y` から手動再実行し、対象の `patch=Z` を指定する。最新成功 RC 候補は自動採用される。
4. `Release Notes: X.Y` を更新しただけなら `Release Finalize` の再実行は不要。GitHub Release は issue 参照だけを持つ。
5. Store へ進める条件は [ReleaseProcess](ReleaseProcess.md) の「Store チャネルへの進行条件」を正本とし、条件を満たす場合のみ `Release Finalize` で Store package を作成する。
6. Partner Center へ提出したら、[StoreSubmission](../distribution/store/StoreSubmission.md) に従って `Store Submission Log` を GitHub Release に記録する。ここで採用 commit が固定化される。

### メジャー / マイナーリリース

1. `Prepare Release` または `create-release-branch.ps1` で `release/X.Y` を作成する。
2. `main` の repository version は `0.0.1` のまま維持する。
3. `Prepare Release` が `Release Notes: X.Y` Issue を自動作成または再利用する。手元スクリプトだけを使う場合は手動で作成または更新する。
4. `Release Notes: Unreleased` から今回の系列で出す bullet を `Release Notes: X.Y` へ手動で移す。
5. `release/X.Y` の安定化を PR で反映する。
6. `release/X.Y` の repository version は `X.Y.0` とする。
7. `rc-X.Y-latest` と必要な RC 成果物を確認し、採用したい候補が latest RC として見えていることを確認する。
8. `Release Notes: X.Y` Issue を見直し、今回の出荷内容として読めるよう公開向け文面を整える。
9. `Release Finalize` を `release/X.Y` から実行し、初回リリースでは `patch=0` を指定して GitHub Release `X.Y.0` と archive / Store package を揃える。
10. Store 提出前に別候補を採用し直す必要が出た場合は、release branch を更新して RC Build を通し、同じ patch line `X.Y.Z` に対して `Release Finalize` を再実行する。
11. Store 提出は [../distribution/store/StoreSubmission.md](../distribution/store/StoreSubmission.md) の手順に従って実行し、提出直後に `Store Submission Log` を記録する。

### パッチリリース

対象は [ReleaseProcess](ReleaseProcess.md) で定義した現行サポート系列のみとする。

1. 不具合修正を `main` へ PR で反映する。
2. `release/X.Y` をベースにした `fix/*` backport ブランチで必要コミットを `cherry-pick -x` し、PR で `release/X.Y` へ反映する。
3. `release/X.Y` の repository version は `X.Y.0` とする。
4. `rc-X.Y-latest` と必要な RC 成果物を確認し、採用したい候補が latest RC として見えていることを確認する。
5. `Release Finalize` 前に `Release Notes: X.Y` Issue を見直し、今回の patch 版として読めるよう公開向け文面を整える。
6. `Release Finalize` を `release/X.Y` から実行し、operator が採番した対象 `patch=Z` を指定する。
7. GitHub Release `X.Y.Z` に archive 成果物が保存され、workflow summary で source package version が `X.Y.0.B`、final package version が `X.Y.Z.B` になっていることを確認する。
8. Store 提出前に追加修正が必要になった場合は、release branch を更新して RC Build を通し、同じ patch line `X.Y.Z` に対して `Release Finalize` を再実行する。
9. Store へ進める条件を満たす場合のみ、Store package を生成して提出する。

### Store 提出

このドキュメントで扱うのは `store-package-*` の生成までとする。
Partner Center への upload、listing CSV import、審査向け補足、submission ID 記録は [StoreSubmission](../distribution/store/StoreSubmission.md) を正本とする。

## ロールバック / 取り下げ

### Dev / RC 配布物

1. 問題のある配布リンクを停止または更新する。
2. `main`（必要なら `release/X.Y`）へ復旧 PR を反映する。
3. 修正版を再ビルドして差し替える。
4. GitHub Release `X.Y.Z` に紐づく archive build に問題がある場合は削除せず、現行サポート系列かつ Store 提出前であれば release branch を更新して RC Build を通し、同じ `patch=Z` で `Release Finalize` を再実行して latest RC 候補へ差し替える。条件を外れる場合は新しい patch line を finalize する。

### Store 提出後

1. Partner Center 側で該当提出の公開を停止 / 取り下げする。
2. 必要なら最新のサポート対象 `release/X.Y` で次の patch line を準備する。
3. 新版 `.msixupload` を再提出する。

## 便利コマンド

```powershell
.\scripts\show-version-report.ps1
.\scripts\assert-version-policy.ps1 -BranchName release/1.3
.\scripts\run-tests.ps1 -Configuration Release
.\scripts\run-security-checks.ps1 -Configuration Release
.\scripts\store-checklist.ps1 -Version X.Y.Z
.\scripts\verify-artifact.ps1 -BundlePath .\ClipSave.Package_X.Y.Z.B_AnyCPU.msixbundle -Channel archive -SourceRef refs/tags/X.Y.Z
```

## 関連ドキュメント

- [ReleaseProcess](ReleaseProcess.md) — 系列、タグ、チャネル、Finalize の全体像
- [StoreSubmission](../distribution/store/StoreSubmission.md) — Partner Center 提出、listing 運用、提出記録
- [ReleaseNotes](ReleaseNotes.md) — GitHub Release Notes 運用
- [Signing](../distribution/Signing.md) — 署名方針
- [ArtifactInstallation](../distribution/ArtifactInstallation.md) — 未署名アーティファクトの検証・導入手順
- [IconAssets](../presentation/IconAssets.md) — アイコン運用
- [GitHub Releases](https://github.com/tnagata012/ClipSave/releases) — 公開 release notes と配布アーカイブ
