# 署名方針

ClipSave の MSIX 署名は現在一時停止中です。
`2026-02-19` 時点で、Azure Key Vault / OIDC を含む運用方式を再評価するまで、CI での署名は行いません。

## この文書の責務

この文書では、以下を扱います。

- 現在の署名停止方針
- 署名再開の判断基準
- 再開時に決めるべき項目

この文書では、以下は扱いません。

- ブランチ運用（`BranchStrategy.md`）
- 版数規約（`Versioning.md`）
- デプロイ手順（`Deployment.md`）

## 現在の運用（署名停止中）

- `dev-build.yml` / `release-build.yml` から署名ステップは削除済み。
- `MSIX_SIGNING_CERT_BASE64` / `MSIX_SIGNING_CERT_PASSWORD` は現行運用の必須要件ではない。
- `msix-signing-dev` / `msix-signing-release` Environment 承認は現行運用では不要。
- `dev-package-*` / `release-package-*` は未署名 `*.msixbundle` として扱う。
- `release-signing-evidence-*` は現行運用では生成しない。

## 署名再開の判断トリガー

以下のいずれかが成立したら、署名運用を再開する。

1. GitHub Releases 配布物をインストール可能な形式で恒常運用したい。
2. 署名未実施による運用制約が開発速度や配布品質に影響している。
3. 証明書管理方式（PFX / Key Vault + OIDC / Trusted Signing）の方針合意が得られた。

## 比較観点（再評価用）

| 方式 | 長所 | 注意点 |
|------|------|--------|
| GitHub Secrets に PFX 保持 | 早く導入できる | 秘密鍵の取り扱い責任が重い |
| Key Vault + OIDC | 秘密情報の常時保持を減らせる | Azure 構成とRBAC設計が必要 |
| Trusted Signing | 証明書配布を簡略化しやすい | 利用条件・料金・互換性確認が必要 |

## 再開時チェックリスト

1. 署名方式を決定し、責任範囲（証明書発行・保管・更新）を明文化する。
2. `Package.appxmanifest` の `Identity Publisher` と署名主体の整合を確認する。
3. `dev-build.yml` / `release-build.yml` に署名ステップを復帰し、検証ジョブを追加する。
4. 証明書ローテーション手順（期限前・漏洩時）をこの文書に追記する。
5. `Deployment.md` と `README.md` の配布説明を署名再開後の実態に合わせる。

## 関連ドキュメント

- [Deployment](Deployment.md) — CI/CD と配布実行手順
- [Versioning](Versioning.md) — 版数規約
- [BranchStrategy](BranchStrategy.md) — ブランチ構成と統合方向
