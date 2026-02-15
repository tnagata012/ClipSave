# Release Notes

## Unreleased

次回リリースに向けた開発中の機能や修正：

### Added
- （新機能を追記）

### Changed
- （既存機能の変更を追記）

### Fixed
- （不具合修正を追記）

---

## v1.0.0 - 初回リリース（予定）

### New Features

- **ホットキー保存**: `Ctrl+Shift+V` でクリップボードコンテンツを即座に保存
- **タスクトレイ常駐**: ウィンドウレスで静かに動作
- **保存先自動決定**: アクティブなデスクトップ／エクスプローラーに保存
- **多種コンテンツ対応**:
  - **画像**: PNG/JPG 形式で保存（透過は PNG で保持、JPG は白背景）
  - **テキスト**: プレーンテキストを UTF-8 TXT として保存
  - **Markdown**: 見出し・リスト等を検出して MD ファイルとして保存
  - **JSON**: 自動整形して JSON ファイルとして保存
  - **表データ**: タブ区切りを CSV 変換（BOM 付き UTF-8、Excel 互換）
- **コンテンツ自動判別**: 画像 → CSV → JSON → Markdown → テキスト の優先順位で判別
- **JPG 品質調整**: 1〜100 の範囲で品質設定
- **カスタマイズ設定**:
  - 保存形式・品質
  - ホットキー
  - 自動起動（Windows スタートアップ設定、既定 ON）
  - 通知設定
  - コンテンツ種別ごとの有効/無効
- **スタートアップ**:
  - 初回起動時に自動起動の変更導線をバルーン通知で案内
  - トレイ右クリックメニューから Windows のスタートアップ設定を直接開ける
- **バルーン通知**: 保存結果を通知
- **多重起動防止**: Mutex による確実な制御

### System Requirements

- **OS**: Windows 11
- **Runtime**: .NET 10

### Installation

- Microsoft Store（予定）
- GitHub Releases から MSIX パッケージをダウンロード

---

## Release Notes Template

新しいリリース時は以下のテンプレートを使用：

```markdown
## v{MAJOR}.{MINOR}.{PATCH} - {概要} (YYYY-MM-DD)

### New Features
- 追加された機能の説明

### Improvements
- 改善された項目の説明

### Bug Fixes
- 修正されたバグの説明
- Issue #XX を参照

### Breaking Changes (MAJOR 更新時のみ)
- 互換性のない変更の説明
- 移行手順

### Known Issues
- 既知の問題がある場合

### Deprecations
- 非推奨となった機能

### Upgrade Notes
- 特別なアップグレード手順が必要な場合
```

---

## Version History Format

各バージョンには以下の情報を含める：

1. **バージョン番号**: SemVer 形式
2. **リリース日**: YYYY-MM-DD 形式
3. **変更内容**: カテゴリ別に整理
4. **Issue/PR 参照**: 該当する場合

---

**Note**: このファイルは [Keep a Changelog](https://keepachangelog.com/) の原則に従って管理されています。
