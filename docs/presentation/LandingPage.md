# ランディングページ運用

ClipSave のランディングページ（`site/`）の更新方針と確認手順を定義します。

## 対象ファイル

| ファイル | 役割 |
|---------|------|
| `site/index.html` | コンテンツ、導線、セクション構成 |
| `site/css/style.css` | レイアウト、配色、レスポンシブ |
| `site/js/main.js` | スクロール演出、モバイルメニュー制御 |
| `site/assets/ClipSave.ico` | サイトの favicon / ヘッダー用アイコン |

## セクション構成

| 順序 | セクション | ID | 背景 |
|------|-----------|-----|------|
| 1 | Hero | — | primary + グロー演出 |
| 2 | 機能 | `#features` | secondary |
| 3 | 使い方 | `#how-it-works` | primary |
| 4 | 詳細 | `#details` | secondary |
| 5 | FAQ | `#faq` | primary |
| 6 | ダウンロード CTA | `#download` | primary |

## 更新フロー

1. 仕様を確認（`docs/UsageGuide.md` / `docs/dev/Specification.md`）
2. `site/` 配下を変更
3. ローカル確認を実施
4. PR 作成（文言修正とデザイン変更は分離）
5. `main` へマージ後、GitHub Pages への自動公開を確認

## ローカル確認手順

```powershell
python -m http.server 4173 --directory site
```

- ブラウザで `http://localhost:4173` を開く
- 確認後は `Ctrl + C` でサーバー停止

## 公開運用

- 公開 URL は `https://tnagata012.github.io/ClipSave/`
- GitHub Pages の公開元は `GitHub Actions`
- `main` に対する `site/**` の変更は `Deploy Pages` workflow（`.github/workflows/deploy-pages.yml`）で自動公開する
- `Deploy Pages` の手動実行は再公開や workflow 変更反映の用途に限定し、`main` ブランチからのみ実行する
- `site/**` と `docs/presentation/LandingPage.md` だけの変更ではアプリ本体の `Dev Build` / `RC Build` は起動しない
- `PR Check` は必須チェックを維持したまま、website-only PR のときは restore / build / test を skip する

## 運用ルール

### 文言整合

- 訴求文は実装仕様と一致させる（保存条件・保存先を優先確認）
- 仕様判断に迷う場合は `docs/dev/Specification.md` を正とする
- 既定ホットキー表記（`Ctrl+Shift+V`）を変更した場合は、`docs/UsageGuide.md` と同時に更新する

### リンク管理

- `GitHub` / `Releases` / `LICENSE` へのリンクは変更時に必ず動作確認する
- 外部リンクには `target="_blank"` と `rel="noopener noreferrer"` を付与する
- フッターの著作年やメンテナー表記は `README.md` / `LICENSE` / `CONTRIBUTING.md` と整合させる

### アクセシビリティ

- `<main>` ランドマーク、スキップリンク、`aria-label` を維持する
- `prefers-reduced-motion: reduce` でアニメーション無効化済み
- フォーカスインジケータ（`focus-visible`）を削除しない

### OGP / メタ情報

- `og:title`・`og:description`・`twitter:card` を `<head>` に定義済み
- タイトルや説明文を変更した場合は OGP メタも同時に更新する

## 変更時チェック

| 区分 | チェック内容 |
|------|--------------|
| 表示 | PC（幅 `>= 900px`）で崩れがない |
| 表示 | モバイル（幅 `<= 640px`）で崩れがない |
| 操作 | モバイルメニューが開閉できる（Escape / 外側タップで閉じる） |
| 操作 | アンカーリンク（`#features` など）が動作する |
| 操作 | Back to top ボタンがスクロール後に表示される |
| 操作 | FAQ の `<details>` が開閉できる |
| 導線 | ダウンロード導線（Hero・ナビ・CTA）が機能する |
| a11y | スキップリンクが Tab で表示される |
| a11y | ナビのアクティブハイライトがスクロールに追従する |

## 関連ドキュメント

- [使い方ガイド](../UsageGuide.md)
- [仕様](../dev/Specification.md)
