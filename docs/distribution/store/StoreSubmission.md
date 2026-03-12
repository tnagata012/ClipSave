# Store 提出実務

Microsoft Store の Partner Center で行う提出実務を、再現可能な手順として定義する。

## この文書の責務

この文書では、以下を扱う。

- `store-package-*` 取得後の Partner Center 操作
- Store metadata / listing / 審査向け補足の入力方針
- listing CSV と画像素材の運用
- 提出記録と証跡の残し方

この文書では、以下は扱わない。

- latest-only や finalized version の判定（[../../release/ReleaseProcess.md](../../release/ReleaseProcess.md)）
- `Release Finalize` の実行や `store-package-*` 生成（[../../release/ReleaseGuide.md](../../release/ReleaseGuide.md)）

## 開始条件

この文書の手順を始める前に、次を満たしていること。

- 対象版が [../../release/ReleaseProcess.md](../../release/ReleaseProcess.md) の Store チャネル進行条件を満たしている。
- [../../release/ReleaseGuide.md](../../release/ReleaseGuide.md) に従って `Release Finalize` の Store package mode が成功している。
- workflow summary で `Checkout Ref=refs/tags/X.Y.Z`、`Commit SHA`、`Store Package Mode=built` を確認できる。
- `store-package-X.Y.Z` から `.msixupload` を取得できる。

## 提出手順

1. `store-package-X.Y.Z` から `.msixupload` を取得する。
2. Partner Center で package をアップロードし、`検証済み` になるまで待つ。
3. listing CSV を import し、スクリーンショットを含む素材の解決結果を確認する。
4. 価格と可用性 / プロパティ / 年齢区分 / 申請オプション（表示される項目のみ）を確認して提出する。
5. 提出直後に Partner Center submission ID、workflow summary の `Commit SHA`、GitHub Actions 実行 URL を保存する。
6. 認定/公開後、Partner Center submission ID と公開結果を GitHub Release 本文の `Store Submission Log` に追記する。

## 提出前チェックリスト

- `store-package-X.Y.Z` の `.msixupload` を取得済みであること。
- workflow summary で `Checkout Ref=refs/tags/X.Y.Z`、`Commit SHA`、`Store Package Mode=built` を確認済みであること。
- `Privacy policy URL` と `Support contact` が設定済みであること。
- listing CSV の必須項目（`Title`、`ShortDescription`、`ReleaseNotes`、`DesktopScreenshot1`）に空欄がないこと。
- package upload 後に `制限付き機能` セクションが表示された場合は、`runFullTrust` の利用理由を記入済みであること。表示されない場合は、同内容を `審査向け補足` に記入済みであること。

上記のいずれかを満たさない場合は提出しない。

## Partner Center 推奨設定

### 価格と可用性

| 項目 | 推奨値 |
|------|--------|
| 価格 | 無料 |
| 配布対象 | パブリック |
| 配布市場 | 全市場（既定） |
| 公開タイミング | できるだけ早く（初回のみ手動公開でも可） |
| Discoverability | Store で検出可能（通常公開） |

### プロパティ

| 項目 | 推奨値 |
|------|--------|
| Primary category | `Productivity` |
| Secondary category | `Utilities + tools`（任意） |
| Privacy policy URL | 有効な公開 URL を設定 |
| Website | `https://github.com/tnagata012/ClipSave` |
| Support contact | tnagata012@gmail.com |

### 年齢区分

| 項目 | 推奨値 |
|------|--------|
| IARC 回答 | ツールアプリ実態に合わせて回答 |
| 表現 | 暴力/性的表現/賭博/教育は該当なし |
| オンライン要素 | なし（実装上ネットワーク通信なし） |

### パッケージ

| 項目 | 推奨値 |
|------|--------|
| 提出ファイル | `.msixupload` |
| 生成方法 | `.github/workflows/release-finalize.yml` |
| workflow 入力 | `version=X.Y.Z`, `build_store_package=true` |
| 対象バージョン | `X.Y.Z.0`（workflow で設定） |
| 対象デバイス | `Windows.Desktop` を確認 |

### ストア登録情報（Listing）

| 項目 | 推奨値 |
|------|--------|
| 言語 | `en-US` と `ja-JP` の 2 つを維持 |
| Import フォルダ | `docs/distribution/store/listing/import/` |
| CSV | import フォルダ直下に `listingData.csv` を 1 つだけ置く |
| Listing 素材 | import フォルダの `assets/` 配下 |
| スクリーンショット | 各言語で `assets/screenshots/<lang>/...` を `DesktopScreenshot1` 以降へ設定 |
| 画像パス | import 対象フォルダから解決できる相対パス、または Partner Center で解決できる URL のみ |

### 申請オプション

| 項目 | 推奨値 |
|------|--------|
| 発行オプション | 既定（認定後に公開） |
| 審査向け補足 | `runFullTrust` 利用理由、グローバルホットキーとローカル保存用途、通信なしを明記 |

`申請オプション` の確認と記入は、package upload 後に行う。

`審査向け補足` テンプレート（日本語）:

```text
本アプリはグローバルホットキーとトレイ常駐を実現するために runFullTrust を使用します。
処理対象はユーザーがコピーしたローカルのクリップボード内容のみで、外部通信やテレメトリ送信は行いません。
```

## listing 素材配置

Store listing の投入物と編集用ソースを分離する。Partner Center へ渡すのは `docs/distribution/store/listing/import/` で、CSV は 1 つだけ置く。言語別の値は `listingData.csv` の `en` / `ja` 列で管理する。

`docs/distribution/store/listing/design-source/` は編集用の元データ、`docs/distribution/store/listing/import/` は Partner Center にそのまま渡す投入物とする。Partner Center から export した CSV や `listingassets/...` URL を残す場合は、必要になった時点で `docs/distribution/store/listing/results/` を作成して保存する。

### import 手順メモ

1. `docs/distribution/store/listing/import/` を開く。
2. フォルダ直下に `listingData.csv` が 1 つだけあることを確認する。
3. CSV ヘッダーが Partner Center の英語 export と同じ構成 (`Field`,`ID`,`Type (Type)`,`default`,`en`,`ja`) であることを確認する。
4. CSV の画像フィールドが `assets/...` のように、import 対象フォルダ直下から解決できる相対パスになっていることを確認する。
5. Partner Center では `import` フォルダ自体を選んで import する。

## listingData 運用ルール

1. Partner Center の英語 export から取得した `listingData.csv` を原本として使い、ヘッダー名や列順を変えない。
2. `Field`,`ID`,`Type (Type)`,`default`,`en`,`ja` の構成を維持する。
3. `default` にカンマ、改行、ダブルクォートを含む場合は CSV ルールに従ってクォートする。
4. 文言や画像パスは `default` ではなく `en` / `ja` 列へ入力する。
5. import 用 CSV は `docs/distribution/store/listing/import/listingData.csv` に 1 つだけ置く。
6. listing 用画像は `docs/distribution/store/listing/import/assets/` 配下に置き、デザインソースは `docs/distribution/store/listing/design-source/` に分離する。
7. 画像フィールドには import 対象フォルダ直下から解決できる相対パスのみを入れ、絶対パスや repo ルート基準のパス（例: `src/...`）は入れない。
8. Partner Center から export した CSV や `listingassets/...` URL を保存する場合は `docs/distribution/store/listing/results/` を作成し、入力値の CSV を上書きしない。
9. ファイル名は `clipsave-store-<用途>-<番号 or 内容>.ext` を基本とし、タイムスタンプ由来の名前を残さない。
10. `ReleaseNotes` は提出版と整合する内容に更新する。
11. 必須項目（`Title`、`ShortDescription`、`ReleaseNotes`、`DesktopScreenshot1`）は各言語列で空欄にしない。

## 証跡と提出記録

提出ごとに次を保存する。

1. GitHub Actions 実行 URL（`Release Finalize` の Store package mode run）。
2. workflow summary の `Commit SHA` と `InformationalVersion`。
3. Partner Center submission ID。

GitHub Release 本文には次の 1 行を追記する。

```text
StoreSubmission: date=YYYY-MM-DD(JST) | version=X.Y.Z | tag=X.Y.Z | commit=<40sha> | submission_id=<PartnerCenterID> | workflow_run=<GitHubActionsRunURL> | status=<Published/Rejected/Withdrawn> | note=<optional>
```

## 関連ドキュメント

- [ReleaseGuide](../../release/ReleaseGuide.md)
- [Signing](../Signing.md)
- [ReleaseProcess](../../release/ReleaseProcess.md)
- [ReleaseNotes](../../release/ReleaseNotes.md)
- [store/listing](listing/)

## 参考（Microsoft Learn）

- https://learn.microsoft.com/ja-jp/windows/apps/publish/publish-your-app/msix/create-app-submission
- https://learn.microsoft.com/ja-jp/windows/apps/publish/publish-your-app/msix/create-app-store-listing
- https://learn.microsoft.com/ja-jp/windows/apps/publish/publish-your-app/msix/app-properties
