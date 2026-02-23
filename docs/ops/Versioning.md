# バージョニング

ClipSave の版数規約と判定ルールを定義します。

## この文書の責務

この文書では、以下を扱います。

- SemVer の運用規約（`X.Y.Z`）
- 版数属性のマッピング（`Version` / `InformationalVersion` / `FileVersion` / MSIX）
- ブランチごとの版数制約
- 版数更新のルール（メジャー/マイナー、パッチ、Dev）
- Git タグ運用（固定タグ / 移動タグ）

この文書では、以下は扱いません。

- ブランチ統合方向（`BranchStrategy.md`）
- ワークフロー実行手順（`Deployment.md`）
- 署名方針（チャネル別運用、`Signing.md`）

## 基本方針

1. `Directory.Build.props` の `Version`（`X.Y.Z`）を SSOT とする。
2. SemVer を MSIX/DLL の制約に合わせて射影する。
3. `PATCH` は「正式リリース回数」に対してのみ増やす。
4. バージョン更新は原則 PR で実施する。
5. 署名有無は版数規約に影響させない。
6. 正式版の固定タグは `X.Y.Z` 形式で統一する。

## SemVer 規約

| 要素 | 意味 | 例 |
|------|------|-----|
| `MAJOR` | 破壊的変更 | `1.9.5` → `2.0.0` |
| `MINOR` | 後方互換な機能追加 | `1.0.1` → `1.1.0` |
| `PATCH` | 後方互換な不具合修正 | `1.0.0` → `1.0.1` |

## 属性マッピング

| 属性 | Release | Dev | 用途 |
|------|---------|-----|------|
| `Directory.Build.props` (`Version`) | `X.Y.Z` | `X.Y.Z` | SSOT |
| `InformationalVersion`（CI 上書き） | `X.Y.Z+sha.<shortSha>` | `X.Y.Z-dev.<run>+sha.<shortSha>` | 追跡・判定 |
| `AssemblyVersion` | `X.Y.0.0` | `X.Y.0.0` | バインディング互換維持 |
| `FileVersion`（CI 注入） | `X.Y.Z.0` | `X.Y.Z.<run>` | DLL 判定補助 |
| MSIX Version | `X.Y.Z.0` | `X.Y.Z.<run>` | パッケージ版数 |

補足:

- `Package.appxmanifest` はリポジトリ上で `X.Y.Z.0` を保持する。
- Dev 版数（`<run>`）は CI で一時注入し、版数ファイルはコミットしない。
- `AssemblyVersion = X.Y.0.0` は PATCH ごとの不要な再バインドを避けるため。
- ローカル手動ビルドでは `Version=X.Y.Z` を使い、`InformationalVersion` の既定値は `$(Version).local`。
- workflow 実行時は CI が `InformationalVersion` / `FileVersion` / MSIX Version をチャネル別に上書きする。

## ブランチ別版数制約

| ブランチ | `Directory.Build.props` | `Package.appxmanifest` |
|---------|--------------------------|--------------------------|
| `main` | `X.Y.Z` | `X.Y.Z.0` |
| `release/X.Y` | `X.Y.Z`（`X.Y` がブランチ名と一致） | `X.Y.Z.0` |

## リリース判定ルール

| 場面 | 判定情報 | ルール |
|------|----------|--------|
| CI/CD | `InformationalVersion` + 実行ブランチ | `-` サフィックスなし、かつ `release/X.Y` 実行 |
| DLL 確認 | `FileVersion` | 4 番目（BUILD）が `0` なら Release |
| 配布物 | 配布チャネル | `release-X.Y-latest` または `release-build.yml` 由来成果物を Release とみなす |

補足:

- `dev-latest` と `release-X.Y-latest` は固定版タグではなく移動タグ（floating tag）として運用し、各 workflow 成功時に実行コミットへ更新する。

## Git タグ運用

| 種別 | 形式 | 更新可否 | 用途 |
|------|------|----------|------|
| Dev チャネルタグ | `dev-latest` | 可（移動） | `main` の最新検証成果物を指す |
| Release チャネルタグ | `release-X.Y-latest` | 可（移動） | `release/X.Y` の最新候補を指す |
| 正式版固定タグ | `X.Y.Z` | 不可（固定） | 正式版の採用コミットを不変で識別する |

運用ルール:

1. `dev-latest` / `release-X.Y-latest` は workflow により更新する移動タグとして扱う。
2. 正式版を確定したら採用コミットへ `X.Y.Z` タグを作成する。
3. `X.Y.Z` は作成後に移動・付け替えを行わない（同一版の作り直しは版を上げる）。
4. Store Publish workflow は `source_ref` 未指定時に `release/X.Y` 先頭を使うため、正式提出では `source_ref=X.Y.Z` を原則指定する（例外時は commit SHA）。

## 検証ルール（`assert-version-policy.ps1`）

共通チェック:

1. `Directory.Build.props` が `X.Y.Z` 形式
2. `Package.appxmanifest` が `X.Y.Z.0` 形式
3. 両者の `X.Y.Z` が一致

ブランチ別チェック:

- `main`: `X.Y.Z` 形式
- `release/X.Y`: `X.Y.Z` 形式かつ `X.Y` がブランチ名と一致

## 版数更新ルール

### 共通

- `main` / `release/X.Y` への直 push は行わない。
- 版数更新は作業ブランチから PR で反映する。
- 例外は `Prepare Release Branch` による初期作成コミットのみ。

### メジャー/マイナー開始

1. `Prepare Release Branch`（推奨）または `create-release-branch.ps1` で `release/X.Y` を作成する。
2. 同時に `chore/bump-main-to-*` を作成し、`main` 側の次系列へ進める。
3. 安定化中は `X.Y.0` を維持し、候補ビルドを繰り返す。
4. 採用コミット確定後に正式版固定タグ `X.Y.Z` を作成する。

### パッチリリース

- 前回正式版が `X.Y.Z` の場合、次回は `X.Y.(Z+1)`。
- 当該リリースサイクルで `PATCH` を更新する PR は 1 回のみ（patch init PR）。
- 以降の backport PR では `PATCH` を再度増やさない。
- 採用コミット確定後に正式版固定タグ `X.Y.Z` を作成する。

実行手段:

- `Prepare Patch Release` workflow（推奨）
- `.\scripts\create-patch-release-branch.ps1 -ReleaseBranch release/X.Y -Push`

### Dev Build

- CI が `InformationalVersion` / `FileVersion` / MSIX Version を一時注入する。
- リポジトリ上の版数ファイルは変更しない。

## Dev/Release Identity 運用ポリシー

Dev と Release は同一 Identity（`Identity Name` / `Publisher`）を採用する。

- 利点: 設定/データ引き継ぎとサポート手順を単純化できる。
- 注意: Dev（例: `1.1.0.42`）の後に Release（`1.1.0.0`）を入れるとダウングレード判定になるため、Dev をアンインストールしてから Release を入れる。

再検討トリガー:

- Dev/Release 切り替え頻度増加により摩擦が継続した場合
- 共存インストール要件が明確化した場合
- 配布チャネル分離が製品要件化した場合

## 関連ドキュメント

- [BranchStrategy](BranchStrategy.md) — ブランチ構成と統合方向
- [Deployment](Deployment.md) — 配布 Runbook
