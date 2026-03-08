# バージョニング

ClipSave の版数規約と判定ルールを定義します。

## この文書の責務

この文書では、以下を扱います。

- SemVer の運用規約（`X.Y.Z`）
- 版数属性のマッピング（`Version` / `InformationalVersion` / `FileVersion` / MSIX）
- ブランチごとの版数制約
- 版数更新のルール（メジャー/マイナー、パッチ、Dev）
- 確定版と Store 公開の切り分け
- Git タグ運用（確定タグ / 移動タグ）

この文書では、以下は扱いません。

- ブランチ統合方向（`BranchStrategy.md`）
- ワークフロー実行手順（`Deployment.md`）
- 署名方針（チャネル別運用、`Signing.md`）

## 基本方針

1. `Directory.Build.props` の `Version`（`X.Y.Z`）を SSOT とする。
2. SemVer を MSIX/DLL の制約に合わせて射影する。
3. `PATCH` は「確定版として数えるリリース回数」に対してのみ増やす。
4. バージョン更新は原則 PR で実施する。
5. 署名有無は版数規約に影響させない。
6. 確定タグは `X.Y.Z` 形式で統一する。
7. 確定版の決定と Store 公開は別工程として扱う。
8. 履歴の正本は確定タグ `X.Y.Z` と GitHub Release `X.Y.Z` とする。能動サポート対象は最新 finalized 系列 1 つのみとし、旧系列は新系列 Finalize 時点で `frozen / unsupported` へ移す。この切替は Store 公開有無ではなく Finalize 完了で判定する。`release/X.Y` は追跡と必要な workflow 再実行のため remote に残す。

## 用語

- 候補版: `rc-X.Y-latest` など、比較・検証中の版。
- 確定版: 候補版の中から「この版でいく」と決めた版。
- 確定タグ: 確定版に付ける固定タグ `X.Y.Z`。

## SemVer 規約

| 要素 | 意味 | 例 |
|------|------|-----|
| `MAJOR` | 破壊的変更 | `1.9.5` → `2.0.0` |
| `MINOR` | 後方互換な機能追加 | `1.0.1` → `1.1.0` |
| `PATCH` | 後方互換な不具合修正 | `1.0.0` → `1.0.1` |

## 属性マッピング

| 属性 | 非 Dev | Dev | 用途 |
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

## ビルド種別判定ルール

| 場面 | 判定情報 | ルール |
|------|----------|--------|
| CI/CD | `InformationalVersion` + 実行ブランチ | `-` サフィックスなし、かつ `release/X.Y` 実行 |
| DLL 確認 | `FileVersion` | 4 番目（BUILD）が `0` なら非 Dev |
| 配布物 | 配布チャネル | `rc-X.Y-latest` は最新候補、GitHub Release `X.Y.Z` / `release-finalize.yml` 由来成果物は確定版アーカイブとみなす |

補足:

- `dev-latest` と `rc-X.Y-latest` は確定タグではなく移動タグ（floating tag）として運用し、各 workflow 成功時に実行コミットへ更新する。
- 能動サポート対象は最新 finalized 系列のみとする。
- 旧系列 `release/X.Y` は新系列 Finalize 後も remote に残すが、`frozen / unsupported` とする。

## Git タグ運用

| 種別 | 形式 | 更新可否 | 用途 |
|------|------|----------|------|
| Dev チャネルタグ | `dev-latest` | 可（移動） | `main` の最新検証成果物を指す |
| RC チャネルタグ | `rc-X.Y-latest` | 可（移動） | `release/X.Y` の最新候補を指す |
| 確定タグ | `X.Y.Z` | 不可（固定） | 確定版のコミットとアーカイブを不変で識別する |

運用ルール:

1. `dev-latest` / `rc-X.Y-latest` は workflow により更新する移動タグとして扱う。
2. 確定版を決めたら、そのコミットに確定タグ `X.Y.Z` を作成する。
3. `X.Y.Z` は確定タグとして作成後に移動・付け替えを行わない（同一版の作り直しは版を上げる）。
4. Store 提出時は `Release Finalize` workflow を `version=X.Y.Z`, `build_store_package=true` で実行し、workflow が `refs/tags/X.Y.Z` を確定タグとして固定参照する。
5. `rc-X.Y-latest` の GitHub Release 表示は候補版として常に `prerelease` とし、確定タグ `X.Y.Z` とは意味を分ける。

## 確定タグと Store 公開

- `X.Y.Z` 確定タグは、その版を確定版として固定したことを示す正本であり、Store 公開の有無とは独立する。
- `Release Finalize` workflow は確定タグごとに不変の未署名アーティファクトを GitHub Release `X.Y.Z` として保存する。
- `Release Finalize` は `workflow_dispatch` により既存の確定タグ `X.Y.Z` に対しても後追い実行できる。
- GitHub Release の `prerelease` フラグやタイトルは表示メタデータであり、版数規約・タグの意味・Store package mode 実行可否には影響しない。
- `Release Finalize` の Store package mode は、確定版のうち一般ユーザー向けに昇格させる版に対してのみ実行する。対象は repository 全体の最新 finalized version に限る。
- ClipSave は latest-only 運用とし、運用上のサポート対象は常に最新 finalized 系列のみとする。
- ただし、新系列 `release/A.B` の最初の確定タグ `A.B.Z` を作る前の PR / RC 安定化は許可する。latest-only 制約は旧 finalized 系列の延命を防ぐためのものであり、新系列の事前準備までは禁止しない。
- 新しい系列の最初の確定タグ `A.B.Z` が作成され Finalize した時点で、それ以前の系列は Store 公開有無にかかわらずサポート対象から外れ、以後の patch/Store 提出は行わない。同一系列内でも、Store package mode を許可するのは最新 finalized version のみとする。
- 旧系列の既存確定タグ `X.Y.Z` に対する `Release Finalize` 再実行は、アーカイブ成果物や GitHub Release メタデータの保守に限って例外的に許容する。これは旧系列サポートの再開を意味しない。
- latest-only は運用原則として扱い、workflow の hard gate は `Release Finalize` の Store package mode に限定する。PR / RC / patch init は複雑化を避けて一律停止しない。
- 例: `0.1.0` は確定タグ付与までで止め、既存の `0.1.1` 確定タグには後から `Release Finalize` を手動実行してよい。

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
3. `main` の次系列は既定で次 `MINOR` へ進めるが、必要なら `next_main_version` / `-NextMainVersion` で将来系列（例: `0.5.0`）へ直接進めてよい。
4. 安定化中は `X.Y.0` を維持し、候補ビルドを繰り返す。
5. 確定対象コミットを決めたら、確定タグ `X.Y.Z` を作成する。

### パッチリリース

- patch release は最新の能動サポート系列でのみ行う。新しい系列が既に Finalize 済みの旧系列では開始しない。
- 前回確定版が `X.Y.Z` の場合、次回は `X.Y.(Z+1)`。
- 当該リリースサイクルで `PATCH` を更新する PR は 1 回のみ（patch init PR）。
- 以降の backport PR では `PATCH` を再度増やさない。
- patch init は、現行版 `X.Y.Z` の確定タグと GitHub Release `X.Y.Z` が存在し、`release/X.Y` HEAD がその確定コミットにある状態でのみ開始する。
- `Release Finalize` 導入前の既存タグなどで GitHub Release `X.Y.Z` が未作成なら、先に `Release Finalize` を手動実行して補完する。
- 確定対象コミットを決めたら、確定タグ `X.Y.Z` を作成する。

実行手段:

- `Prepare Patch Release` workflow（推奨）
- `.\scripts\create-patch-release-branch.ps1 -ReleaseBranch release/X.Y -Push`

### Dev Build

- CI が `InformationalVersion` / `FileVersion` / MSIX Version を一時注入する。
- リポジトリ上の版数ファイルは変更しない。

## Dev/RC Identity 運用ポリシー

Dev と RC/Archive は同一 Identity（`Identity Name` / `Publisher`）を採用する。

- 利点: 設定/データ引き継ぎとサポート手順を単純化できる。
- 注意: Dev（例: `1.1.0.42`）の後に RC/Archive（`1.1.0.0`）を入れるとダウングレード判定になるため、Dev をアンインストールしてから RC/Archive を入れる。

再検討トリガー:

- Dev/RC 切り替え頻度増加により摩擦が継続した場合
- 共存インストール要件が明確化した場合
- 配布チャネル分離が製品要件化した場合

## 関連ドキュメント

- [BranchStrategy](BranchStrategy.md) — ブランチ構成と統合方向
- [Deployment](Deployment.md) — 配布 Runbook
