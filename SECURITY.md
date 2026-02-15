# Security Policy

## Reporting a Vulnerability

セキュリティ脆弱性を発見した場合は、[GitHub Security Advisory](https://github.com/taka-rl/ClipSave/security/advisories) で非公開報告してください。

Security Advisory が利用できない場合の連絡先: `tnagata012@gmail.com`

**報告に含めるべき情報:**
- 脆弱性の種類と再現手順
- 影響範囲（対象バージョン、条件）
- 可能であればPoC（概念実証コード）

**お願い:** 公開Issueでの報告は避け、修正版リリースまで非公開にご協力ください。

## Security Features

ClipSave は、セキュリティとプライバシーを重視して設計されています。

**主要なセキュリティ対策:**
- ✅ **ネットワーク通信なし** - すべてローカルで処理、テレメトリなし
- ✅ **最小権限** - 管理者権限不要、ユーザーフォルダのみアクセス
- ✅ **データ保護** - クリップボード画像を解析・保持せず、保存処理に必要な範囲でのみ使用
- ✅ **原子的書き込み** - ファイル破損を防ぐ安全な保存処理
- ✅ **多重起動防止** - グローバルミューテックスで制御
- ✅ **プライバシー優先** - 画像内容、保存履歴、使用統計を一切収集しない

**依存関係:**
- Microsoft公式パッケージ（Extensions.*, CommunityToolkit.Mvvm）
- Serilog（ログ）

## Best Practices

**ユーザー向け:**
- 公式ソース（Microsoft Store / GitHub Releases）からのみインストール
- 常に最新バージョンを使用
- 機密情報を含むスクリーンショットは慎重に管理

**開発者向け:**
- オープンソースでコード監査可能
- セキュリティ問題を発見した場合は、本ドキュメントの手順で報告
