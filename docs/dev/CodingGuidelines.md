# コーディングガイドライン

ClipSave の実装規約（コード記述、コメント、ログ、例外境界、テスト接続）を定義します。

## 適用範囲

- コード実装規約の基準ドキュメントは本ドキュメントとする。
- リポジトリ横断の言語ポリシー（Issue/PR、コミットメッセージ、docs を含む）の基準ドキュメントは `CONTRIBUTING.md` とする。
- `CONTRIBUTING.md` は入口として概要のみを記載し、実装規約の詳細は本ドキュメントに集約する。
- 規約を変更した場合は、関連ドキュメント（`Architecture.md` / `TestingStrategy.md` / `CONTRIBUTING.md`）との整合を確認する。

## コーディング標準

- **.NET Conventions**: [.NET コーディング規則](https://learn.microsoft.com/ja-jp/dotnet/csharp/fundamentals/coding-style/coding-conventions)に従う。
- **命名規則**: PascalCase（型、メソッド、プロパティ）、camelCase（ローカル変数、引数）を使用する。
- **責務分離**: 層の責務は `Architecture.md` に従い、テスト容易性のためだけに本体設計を歪めない。
- **テスト接続**: テスト追加方針と粒度判定は `TestingStrategy.md` を参照する。

## コメント方針

- コメントは `what` の言い換えを避け、`why`（制約・設計意図・回避策）を優先する。
- 自明な処理説明コメントは追加しない。
- 公開 API の XML doc は必要最小限とし、シグネチャから読み取れる説明は重複しない。
- 将来変更で陳腐化しやすい値や手順は、コメントではなく定数名・テスト名で表現する。

## ロギング方針

- 文字列連結ではなく構造化ログ（`{Placeholder}`）を使用する。
- `Information` は状態遷移・主要イベント、`Debug` は診断情報、`Warning` は回復可能異常、`Error/Critical` は失敗境界に限定する。
- 同一事象で重複ログを出さない（境界で一度だけ記録する）。
- 失敗時ログは「何が失敗したか」「どの入力で失敗したか」を含める。
- 保存処理の一次ログ境界は `SavePipeline` とし、下位サービスの成功ログは原則 `Debug` に留める。
- 詳細ログはローテーションし、保持上限は 7 ファイルとする。

## 例外境界ポリシー

`catch` を配置してよいのは次の 2 箇所に限定する。

1. 境界層: `App` / `AppLifecycleCoordinator` のイベントハンドラ、`SavePipeline.ExecuteAsync()` のように「例外をユーザー向け結果へ変換する」入口。
2. 回復可能箇所: リトライ、フォールバック、クリーンアップ失敗の抑止など、処理継続に意味がある箇所。

- それ以外のサービス層は原則として例外を上位へ伝播し、`catch` して `log + throw` だけのコードは作らない。
- 後始末目的の制御は `try/catch` より `try/finally` を優先し、元の例外を上書きしない。
- 握りつぶしが必要な処理（通知表示、クラッシュ時の二次処理、クリーンアップ）は失敗時の挙動（無視/警告ログ）を明示する。

## 関連ドキュメント

- [Contributing](../../CONTRIBUTING.md) - 入口手順と言語ポリシー
- [アーキテクチャ](Architecture.md) - システムアーキテクチャ
- [テスト戦略](TestingStrategy.md) - テスト方針
- [仕様](Specification.md) - 詳細仕様（SPEC-ID）
