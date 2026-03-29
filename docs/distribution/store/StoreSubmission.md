# Store 提出実務

**このドキュメントの目的**: Microsoft Store の Partner Center で行う提出実務を、再現可能な手順として定義します。

## このドキュメントの役割

このドキュメントでは、以下を扱う。

- `store-package-*` 取得後の Partner Center 操作
- Store metadata / listing / 審査向け補足の入力方針
- listing CSV と画像素材の運用
- 提出記録と証跡の残し方
- `Store Submission Log` を使った version tag 固定化

このドキュメントでは、以下は扱わない。

- latest-only や finalized version の判定（[../../release/ReleaseProcess.md](../../release/ReleaseProcess.md)）
- `Release Finalize` の実行や `store-package-*` 生成（[../../release/ReleaseGuide.md](../../release/ReleaseGuide.md)）

## 開始条件

このドキュメントの手順を始める前に、次を満たしていること。

- 対象版が [../../release/ReleaseProcess.md](../../release/ReleaseProcess.md) の Store チャネル進行条件を満たしている。
- [../../release/ReleaseGuide.md](../../release/ReleaseGuide.md) に従って `Release Finalize` の Store package mode が成功している。
- workflow summary で `Checkout Ref=refs/tags/X.Y.Z`、`Commit SHA`、`Package Version=X.Y.Z.B`、`Store Package Mode=built` を確認できる。
- 同じ run の `store-package-X.Y.Z` artifact から `.msixupload` を取得でき、workflow の Store identity 検証も通っている。GitHub Release `X.Y.Z` assets は使わない。

## 提出手順

1. `store-package-X.Y.Z` から `.msixupload` を取得する。
2. Partner Center で package をアップロードし、`検証済み` になるまで待つ。
3. listing CSV を import し、スクリーンショットを含む素材の解決結果を確認する。
4. 価格と可用性 / プロパティ / 年齢区分 / 申請オプション（表示される項目のみ）を確認して提出する。
5. 提出直後に Partner Center submission ID、workflow summary の `Commit SHA`、GitHub Actions 実行 URL を保存する。
6. 提出直後に GitHub Release 本文の `Store Submission Log` へ 1 行追記する。これをもって `X.Y.Z` は固定扱いになる。
7. 認定/公開後、同じ行の `status` / `note` を更新する。

## 提出前チェックリスト

- `store-package-X.Y.Z` artifact から `.msixupload` を取得済みであること。GitHub Release assets は使わない。
- workflow summary で `Checkout Ref=refs/tags/X.Y.Z`、`Commit SHA`、`Package Version=X.Y.Z.B`、`Store Package Mode=built` を確認済みであること。
- `Privacy policy URL` に公開済みの [../../../PRIVACY.md](../../../PRIVACY.md) 相当ページを設定し、`Support contact` が設定済みであること。
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
| Privacy policy URL | repo 直下の [../../../PRIVACY.md](../../../PRIVACY.md) を公開した URL を設定 |
| Website | `https://github.com/tnagata012/ClipSave` |
| Support contact | tnagata012@gmail.com |

Store policy 上、Desktop Bridge / Win32 製品は privacy policy が必須になるため、Partner Center では `PRIVACY.md` を公開した URL を正本として使う。

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
| workflow 実行 | GitHub Actions で `release/X.Y` を選択して実行 |
| workflow 入力 | `patch` に対象の `Z` を入れる（初回 `X.Y.0` リリースは `patch=0`）。通常は `build_store_package=true` / `create_version_tag=true` のまま実行 |
| 対象バージョン | `X.Y.Z.B`（`B > 0`。`B` は対象 `release/X.Y` branch の RC カウンター） |
| 対象デバイス | `Windows.Desktop` を確認 |

### ストア登録情報（Listing）

| 項目 | 推奨値 |
|------|--------|
| 言語 | `en-US` と `ja-JP` の 2 つを維持 |
| Import フォルダ | `docs/distribution/store/listing/import/` |
| CSV | import フォルダ直下に `listingData.csv` を 1 つだけ置く |
| Listing 素材 | import フォルダの `assets/` 配下 |
| スクリーンショット | 各言語で `import/assets/screenshots/<lang>/...` を `DesktopScreenshot1` 以降へ設定 |
| 画像パス | `Import folder` 使用時は root フォルダ名を含むパス（例: `import/assets/...`）、または Partner Center で解決できる URL のみ |

### 申請オプション

| 項目 | 推奨値 |
|------|--------|
| 発行オプション | 既定（認定後に公開） |
| 審査向け補足 | `runFullTrust` 利用理由、グローバルホットキーとローカル保存用途、通信なしを明記 |

`申請オプション` の確認と記入は、package upload 後に行う。

Partner Center の `制限付き機能` セクションで `runFullTrust` の説明欄が表示された場合は、次の `text` を使う。

```text
ClipSave は、Windows のタスクトレイに常駐するデスクトップユーティリティとして動作し、ユーザーが任意のアプリ使用中でもグローバルホットキー Ctrl+Shift+V で保存処理を実行できる必要があるため、runFullTrust を使用します。

本アプリは、ユーザーが明示的にコピーしたローカルのクリップボード内容のみを対象に、画像・表データ・JSON・Markdown・テキストを判別し、デスクトップまたは現在開いている File Explorer のフォルダーへ保存します。保存先の判定、トレイ常駐、グローバルホットキー登録は Windows デスクトップ API を用いてローカルで実行します。

外部通信、クラウド送信、アカウント連携、テレメトリ送信は行いません。クリップボード内容はユーザーの操作に応じて端末内でのみ処理されます。
```

## listing 素材配置

Store listing の投入物と編集用ソースを分離する。Partner Center へ渡すのは `docs/distribution/store/listing/import/` で、CSV は 1 つだけ置く。言語別の値は `listingData.csv` の `en` / `ja` 列で管理する。

`docs/distribution/store/listing/design-source/` は編集用の元データ、`docs/distribution/store/listing/import/` は Partner Center にそのまま渡す投入物とする。Partner Center から export した CSV や `listingassets/...` URL を残す場合は、必要になった時点で `docs/distribution/store/listing/results/` を作成して保存する。

### import 手順メモ

1. `docs/distribution/store/listing/import/` を開く。
2. フォルダ直下に `listingData.csv` が 1 つだけあることを確認する。
3. CSV ヘッダーが Partner Center の英語 export と同じ構成 (`Field`,`ID`,`Type (Type)`,`default`,`en`,`ja`) であることを確認する。
4. CSV の画像フィールドが `import/assets/...` のように、root フォルダ名を含むパスになっていることを確認する。
5. Partner Center では `import` フォルダ自体を選んで import する。

## listingData 運用ルール

1. Partner Center の英語 export から取得した `listingData.csv` を原本として使い、ヘッダー名や列順を変えない。
2. `Field`,`ID`,`Type (Type)`,`default`,`en`,`ja` の構成を維持する。
3. `default` にカンマ、改行、ダブルクォートを含む場合は CSV ルールに従ってクォートする。
4. 文言や画像パスは `default` ではなく `en` / `ja` 列へ入力する。
5. import 用 CSV は `docs/distribution/store/listing/import/listingData.csv` に 1 つだけ置く。
6. listing 用画像は `docs/distribution/store/listing/import/assets/` 配下に置き、デザインソースは `docs/distribution/store/listing/design-source/` に分離する。
7. 画像フィールドには `import/assets/...` のように root フォルダ名を含むパスのみを入れ、絶対パスや repo ルート基準のパス（例: `src/...`）は入れない。
8. Partner Center から export した CSV や `listingassets/...` URL を保存する場合は `docs/distribution/store/listing/results/` を作成し、入力値の CSV を上書きしない。
9. ファイル名は `clipsave-store-<用途>-<番号 or 内容>.ext` を基本とし、タイムスタンプ由来の名前を残さない。
10. `ReleaseNotes` は issue `Release Notes: X.Y` の公開ノートと矛盾しない内容に更新する。
11. 必須項目（`Title`、`ShortDescription`、`ReleaseNotes`、`DesktopScreenshot1`）は各言語列で空欄にしない。

## 証跡と提出記録

提出ごとに次を保存する。

1. GitHub Actions 実行 URL（`Release Finalize` の Store package mode run）。
2. workflow summary の `Commit SHA` と `InformationalVersion`。
3. Partner Center submission ID。

GitHub Release 本文には次の 1 行を追記する。

```text
StoreSubmission: date=YYYY-MM-DD(JST) | version=X.Y.Z | tag=X.Y.Z | commit=<40sha> | submission_id=<PartnerCenterID> | workflow_run=<GitHubActionsRunURL> | status=<Submitted/InCertification/Published/Rejected/Withdrawn> | note=<optional>
```

`StoreSubmission:` 行が GitHub Release に記録された時点で、その version tag は固定扱いになる。提出直後は `status=Submitted` で記録し、その後の認定結果に合わせて更新する。

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
