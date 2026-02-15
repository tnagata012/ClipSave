# バージョニング

ClipSave のバージョニング規約を定義します。

## バージョン形式

SemVer 2.0.0 の `MAJOR.MINOR.PATCH` を使用します。

```text
MAJOR.MINOR.PATCH
```

| 要素 | 意味 | 例 |
|------|------|-----|
| `MAJOR` | 破壊的変更 | `1.9.5` -> `2.0.0` |
| `MINOR` | 後方互換な機能追加 | `1.0.1` -> `1.1.0` |
| `PATCH` | 後方互換な不具合修正 | `1.0.0` -> `1.0.1` |

## ファイルごとの表現

| ファイル | 形式 | 用途 |
|---------|------|------|
| `Directory.Build.props` | `X.Y.Z` | ソース上のバージョン（SSOT） |
| `Package.appxmanifest` | `X.Y.Z.0` | MSIX 用4桁バージョン |

補足:
- `Package.appxmanifest` は MSIX 制約により常に数値4桁。
- ソース管理上は常に Revision `0` を保持する。

## ブランチごとの規約

| ブランチ | `Directory.Build.props` | `Package.appxmanifest` |
|---------|--------------------------|--------------------------|
| `main` | `X.Y.Z` | `X.Y.Z.0` |
| `release/X.Y.x` | `X.Y.Z` | `X.Y.Z.0` |

追加ルール:
- `release/X.Y.x` では `X.Y` がブランチ名と一致している必要がある。

## ビルド成果物の見え方

MSIX は `Major.Minor.Build.Revision` 形式です。

| ビルド種別 | `Directory.Build.props` | 実際のパッケージ Version |
|-----------|--------------------------|-----------------------------|
| Dev Build | `1.4.0` | `1.4.0.<GITHUB_RUN_NUMBER>` |
| Release Build | `1.3.1` | `1.3.1.0` |

注意: `GITHUB_RUN_NUMBER` はリポジトリ単位のカウンターであり、リポジトリ移行やフォーク時にリセットされる可能性がある。Dev Build の一意性保証はこのカウンターに依存するため、移行時には Revision の衝突に注意すること。

確認例:

```powershell
Get-AppxPackage -Name "*ClipSave*" | Select-Object Version
# 1.3.1.0   <- Release
# 1.4.0.123 <- Dev
```

## 検証ルール（`validate-version.ps1`）

### 共通チェック

1. `Directory.Build.props` が `X.Y.Z` 形式か
2. `Package.appxmanifest` が `X.Y.Z.0` 形式か
3. 両者の `X.Y.Z` が一致しているか

### ブランチ別チェック

- `main`: `X.Y.Z` 形式であること
- `release/X.Y.x`: `X.Y.Z` 形式かつ `X.Y` がブランチ名と一致すること

運用メモ:
- スクリプトは実行ディレクトリに依存せず、`-ProjectRoot` で対象リポジトリを明示可能。

## バージョン更新タイミング

### メジャー/マイナーリリース開始時

`create-release-branch.ps1 -Version X.Y.0` で以下を更新:
- `release/X.Y.x` を `X.Y.0`
- `main` を `X.(Y+1).0`

### パッチリリース時

`release/X.Y.x` 上で手動更新:
- `Directory.Build.props`: `X.Y.Z`
- `Package.appxmanifest`: `X.Y.Z.0`

### Dev Build 実行時

- バージョンファイルは書き換えない
- `X.Y.Z.<GITHUB_RUN_NUMBER>` をビルド時に一時使用

## 運用チェックリスト

- `release/X.Y.x` の `X.Y` とファイル版数の `X.Y` が一致している
- `Package.appxmanifest` が数値4桁になっている
- `validate-version.ps1` が CI で成功している

## 関連ドキュメント

- [BranchStrategy](BranchStrategy.md)
- [Deployment](Deployment.md)
