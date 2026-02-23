# CHANGELOG 運用ガイド

`CHANGELOG.md` を一貫して更新するための運用ルールです。
Keep a Changelog に沿い、リポジトリ固有情報に依存しない共通ルールを定義します。

## 基本ルール

1. 正本はリポジトリ直下の `CHANGELOG.md`。
2. `CHANGELOG` はユーザー向け。ユーザー影響のある変更のみ記載する。
3. 変更はまず `[Unreleased]` に追記し、リリース時に `## [X.Y.Z] - YYYY-MM-DD` へ移す。
4. カテゴリは Keep a Changelog 準拠（`Added` / `Changed` / `Deprecated` / `Removed` / `Fixed` / `Security`）。
5. 新しい版を上に追加する（降順）。履歴は追記ベースで管理する。
6. 1 項目 1 変更を原則に、1〜3 行で簡潔に書く。
7. 日付は `YYYY-MM-DD`（ISO 8601）で統一する。
8. 比較リンク（`[Unreleased]` など）は任意で運用する。

## 記載スタイル

| 観点 | 推奨 |
|------|------|
| 視点 | ユーザー視点で「何がどう変わるか」を書く |
| 粒度 | 1 項目に複数変更を詰め込みすぎない |
| 文言 | 内部実装名だけで終わらせず、挙動の変化を明示する |

## 記載判断（書く/書かない）

| 判断 | 目安 | 例 |
|------|------|----|
| 書く | アップデート後にユーザーが気づく/影響を受ける | 新機能、仕様変更、不具合修正、削除、セキュリティ対応 |
| 書かない | ユーザー影響がない内部変更 | リファクタ、テスト追加、CI 改善、内部ログ整理 |

## カテゴリ定義

| カテゴリ | 用途 |
|---------|------|
| `Added` | 新機能 |
| `Changed` | 既存機能の仕様変更、挙動変更 |
| `Deprecated` | 将来削除予定の機能 |
| `Removed` | 削除した機能 |
| `Fixed` | 不具合修正 |
| `Security` | セキュリティ修正、緩和策 |

## 更新フロー

### PR 時

1. ユーザー影響がある変更なら `CHANGELOG.md` の `[Unreleased]` に追記する。
2. 該当カテゴリを 1 つ選び、ユーザー視点で 1〜3 行にまとめる。
3. ユーザー影響がない変更は PR テンプレの `Changelog` を `N/A` にする。

### リリース時

1. `Unreleased` の内容を整理する。
2. `## [X.Y.Z] - YYYY-MM-DD` セクションを作り、対象版に含める項目だけを移動する。
3. `Unreleased` から移した項目を削除する（未リリース項目は残す）。
4. 配布先のリリースノートへ同内容を反映する。
5. 比較リンクを運用する場合は、`[Unreleased]` と直近版リンクを更新する。

### パッチリリース時

- `Unreleased` からパッチ対象の修正のみを `X.Y.Z` へ移す。
- 次版向け・未リリース項目は `Unreleased` に残す（空にしない場合がある）。

## テンプレート

未使用カテゴリは削除して構いません。

### `Unreleased`

```markdown
## [Unreleased]

### Added
- 

### Changed
- 

### Deprecated
- 

### Removed
- 

### Fixed
- 

### Security
- 
```

### 版セクション

```markdown
## [X.Y.Z] - YYYY-MM-DD

### Added
- 

### Changed
- 

### Deprecated
- 

### Removed
- 

### Fixed
- 

### Security
- 
```

### 比較リンク（任意）

```markdown
[Unreleased]: https://github.com/<owner>/<repo>/compare/X.Y.Z...HEAD
[X.Y.Z]: https://github.com/<owner>/<repo>/compare/A.B.C...X.Y.Z
```
