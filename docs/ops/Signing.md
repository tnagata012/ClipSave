# 署名方針

ClipSave は運用コストと配布安全性のバランスを優先する。

- 一般ユーザー向けの安定版は `Store` を正本チャネルとする。
- `Store` 外の配布（GitHub Releases 直配布など）は、開発者向け/検証向けに限定する。
- 公開コード署名証明書（OV/EV 等）は、`Store` 外の恒常的な一般配布が必要になるまで導入しない。

## この文書の責務

この文書では、以下を扱う。

- チャネル別の署名要件
- OSS としての現実的な配布運用
- 公開証明書を導入する判断基準

この文書では、以下は扱わない。

- ブランチ運用（`BranchStrategy.md`）
- 版数規約（`Versioning.md`）
- デプロイ実行手順（`Deployment.md`）

## チャネル別ポリシー

| チャネル | 主な利用者 | 配布物 | 署名方針 |
|------|------|------|------|
| Dev | 開発者/協力者 | `dev-package-*`（`*.msixbundle`） | 未署名を許容 |
| Release | テスター/検証者 | `release-package-*`（`*.msixbundle`） | 未署名を許容 |
| Stable | 一般ユーザー | `store-package-*`（`.msixupload`）経由で Store 配布 | Store チャネルを利用 |

補足:

- `dev-build.yml` / `release-build.yml` には署名ステップを含めず、`SHA256SUMS.txt` を成果物に同梱し、GitHub Artifact Attestation を記録する。
- `MSIX_SIGNING_CERT_BASE64` / `MSIX_SIGNING_CERT_PASSWORD` は現行運用の必須要件ではない。
- `msix-signing-dev` / `msix-signing-release` Environment 承認は現行運用では不要。

## 運用の基本ルール

`Store` 外で未署名成果物を配布する場合は、最低限次を満たす。

1. 配布対象を明示する（開発者向け・検証目的であることを明記）。
2. バイナリの `SHA256` を併記する。
3. `gh attestation verify` で確認可能な provenance（GitHub Artifact Attestation）を提供する（運用では `scripts/verify-artifact.ps1` の利用を推奨）。
4. 取得元を公式チャネルに限定する（リポジトリ直下の Releases / Actions artifacts）。
5. 既知の制約（SmartScreen 警告、手動インストール手順）を案内する。

## 公開証明書導入の判断トリガー

次のいずれかが成立した場合、`Store` 外配布向け署名を再評価する。

1. GitHub Releases 等で一般ユーザー向けインストーラーを恒常運用したい。
2. 未署名運用によるサポート負荷（警告対応、導入失敗）が無視できない。
3. 企業導入などで署名済み配布が要件化された。

## 導入時の選定順序

`Store` 外配布向けに署名をする場合、次の順で検討する。

1. OSS 支援プログラム（例: SignPath Foundation）
2. クラウド署名基盤（例: Trusted Signing）
3. OV/EV 証明書 + CI 連携（鍵管理を含む）

`GitHub Secrets` に PFX を直接保持する方式は、短期回避策としてのみ扱う。

## 署名導入時チェックリスト

1. 配布チャネルごとの署名要件を明文化する（Dev/Release/Stable）。
2. 署名主体と `Package.appxmanifest` の `Identity Publisher` を一致させる。
3. CI に署名・検証・タイムスタンプ検証を組み込む。
4. 鍵管理とローテーション手順（期限前更新、漏えい時失効）を定義する。
5. `Deployment.md` と `README.md` の配布説明を更新する。

## 関連ドキュメント

- [Deployment](Deployment.md) — CI/CD と配布実行手順
- [Versioning](Versioning.md) — 版数規約
- [BranchStrategy](BranchStrategy.md) — ブランチ構成と統合方向
