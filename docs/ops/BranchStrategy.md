# ブランチ戦略

ClipSave のブランチ構成と統合方向を定義します。

## この文書の責務

この文書では、以下を扱います。

- ブランチ種別（`main` / `release/X.Y` / 作業ブランチ）
- 統合方向（`main` 先行、`release/X.Y` への backport）
- リリース系列の開始・保守・終了
- ブランチ命名の制約

この文書では、以下は扱いません。

- 版数規約と `PATCH` 更新手順の詳細（`Versioning.md`）
- ワークフロー実行手順（`Deployment.md`）
- 署名方針（チャネル別運用、`Signing.md`）

## 運用原則

1. 開発の主戦場は `main`（Trunk-first）とする。
2. 公開品質の安定化は `release/X.Y` で行う。
3. 修正の正本は常に `main` とし、release 側は必要分のみ追従する。
4. 作業ブランチは短命に保ち、PR で統合する。

## ブランチ種別

| ブランチ | 寿命 | 用途 |
|---------|------|------|
| `main` | 永続 | 次期開発の幹 |
| `release/X.Y` | 中期 | 公開安定化・パッチ保守 |
| `feature/*` | 短命 | 機能追加 |
| `fix/*` | 短命 | 不具合修正・backport 作業 |
| `docs/*` | 短命 | ドキュメント更新 |
| `chore/*` | 短命 | 運用・自動化・雑務 |

## 命名ルール

- 作業ブランチのプレフィックスは `feature/`, `fix/`, `docs/`, `chore/` のみ許可する。
- 長寿命ブランチは `main` と `release/X.Y` のみとする。
- `release/X.Y.Z` や `hotfix/*` のようなパッチ単位ブランチは作成しない。
- 新しいプレフィックスを導入する場合は Issue/PR で合意し、この文書を更新する。

例:

- `feature/save-image-hotkey`
- `fix/release-1.3-backport-042`
- `docs/update-branch-policy`
- `chore/bump-main-to-1.4.0`

## 統合ルール（必須）

1. `main` 向けの作業ブランチは `main` から作成し、PR で `main` に統合する。
2. `release/X.Y` はメジャー/マイナー開始時にのみ作成する。
3. `release/X.Y` で新機能開発をしない。
4. 不具合修正は `main` に先にマージし、backport は `release/X.Y` をベースにした `fix/*` ブランチへ必要コミットを `cherry-pick -x` し、PR で `release/X.Y` に統合する。
5. `release/X.Y` から `main` へマージしない。
6. `main` / `release/X.Y` への人手による直 push は禁止し、PR マージのみで反映する。
7. 緊急脆弱性修正も `hotfix/*` ではなく通常のパッチリリース手順（`main` 先行 + backport + PR）で扱う。
8. `PATCH` の更新タイミングは `Versioning.md` の規則に従う。

## リリース系列ライフサイクル

### Start

- 安定した `main` から `release/X.Y` を作成する。
- 同時に `main` は次開発系列（通常は次 `MINOR`）へ進める。

### Maintenance

- `release/X.Y` には公開品質に必要な変更のみを入れる。
- 取り込み元は `main` を正とし、release 側は必要分のみ backport する。

### Support window

- 標準は「最新 1 系列（直近 `release/X.Y`）」のみ同時保守する。
- 旧系列の保守は、次の条件を満たす場合のみ例外許可する。
  - 高緊急度のセキュリティ対応
  - Store 審査/公開上の要請
  - メンテナーの明示承認

### End of support

- サポート終了後の `release/X.Y` は凍結し、履歴のみ保持する。
- 凍結後は原則として追加コミットを行わない。

## cherry-pick 競合時の方針

1. `release/X.Y` から backport 用ブランチ（例: `fix/release-X.Y-backport-<id>`）を作成する。
2. 競合は release 系列の互換性を優先して解消する。
3. 競合解消内容と理由を PR に明記する。
4. release 側だけの場当たり修正を避け、必要なら `main` に先行調整を入れて再 backport する。

## GitHub での強制

- `main` / `release/*` は Branch protection または Ruleset で保護する。
- Ruleset 定義 JSON は `.github/rulesets/` に保存し、GitHub 側の Ruleset 変更時は同時に更新する。
- 現行のレビュー強制は `Code Owner review` を必須とし、汎用の承認件数（write 権限レビューアによる `At least 1 approving review`）は `0` とする。
- `CODEOWNERS` は `@TNagata012` を正本とし、オーナー以外の PR は `@TNagata012` の承認を必須とする。

## 関連ドキュメント

- [Versioning](Versioning.md) — 版数規約
- [Deployment](Deployment.md) — 配布 Runbook
- [Signing](Signing.md) — 署名方針（チャネル別運用）
