# Store サブミッション運用

Microsoft Store への提出を、再現可能かつ同じ手順で実行するための Runbook です。

## 運用方針

1. 提出物の生成は `Release Finalize` workflow の Store package mode のみを使用する。
2. Store package mode の必須入力は `version=X.Y.Z` と `build_store_package=true`。
3. workflow は `version` から `refs/tags/X.Y.Z` を自動解決し、確定タグが `release/X.Y` 系列の確定対象コミットを指すことを検証する。
4. Store package mode は確定版のうち、一般ユーザー向けに出す版に対してのみ実行する。
5. GitHub Release `X.Y.Z` の `prerelease` 表示やタイトルは Store 提出判定には使わない。
6. ClipSave は latest-only 運用とし、Store へ提出するのは常に repository 全体の最新 finalized version のみとする。`Release Finalize` の Store package mode はこの条件を自動検証し、旧 finalized 系列や同一系列内の古い確定タグでは失敗する。切替基準は Store 公開有無ではなく Finalize 完了であり、新しい系列を archive-only で Finalize した場合も旧系列は `frozen / unsupported` に移行する。

## 提出手順

1. 確定対象コミットに確定タグ `X.Y.Z` を付与する。
2. タグ push により `Release Finalize` が走り、GitHub Release `X.Y.Z` とアーカイブ成果物が作成されたことを確認する。
3. `scripts/assert-version-policy.ps1` を実行する。
4. `scripts/store-checklist.ps1 -Version X.Y.Z` を実行する。latest-only 判定を含め、対象版が repository 全体の最新 finalized version であることを確認する。branch HEAD が先に進んでいる場合も、提出対象 tag を明示して確認する。
5. `Release Finalize` を `version=X.Y.Z`, `build_store_package=true` で実行する。
6. workflow summary で `Checkout Ref=refs/tags/X.Y.Z`、`Commit SHA`、`Store Package Mode=built` を確認する。
7. `store-package-X.Y.Z` から `.msixupload` を取得する。
8. Partner Center で package をアップロードし、`検証済み` になってから listing CSV をインポートする。
9. 価格と可用性 / プロパティ / 年齢区分 / 申請オプション（表示される項目のみ）を確認して提出する。
10. 認定/公開後、Partner Center submission ID と公開結果を GitHub Release 本文の `Store Submission Log` に追記する。
11. 公開直後は短い監視期間を設け、必要なら同じ最新系列で Store 再提出を行う。
12. 新しい系列 `A.B.Z` を Finalize した時点で、それ以前の系列は旧系列となり、新系列をまだ Store へ出していない場合でも以後は通常の Store 再提出を行わない。

## 提出前チェックリスト

- 確定タグ `X.Y.Z` が作成・push 済みであること。
- `Release Finalize` のアーカイブ処理が成功し、GitHub Release `X.Y.Z` が作成済みであること。
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
| Import フォルダ | `docs/store/listing/import/` |
| CSV | import フォルダ直下に `listingData.csv` を 1 つだけ置く |
| Listing 素材 | import フォルダの `assets/` 配下 |
| スクリーンショット | 各言語で `import/assets/screenshots/<lang>/...` を `DesktopScreenshot1` 以降へ設定 |
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

Store listing の投入物と編集用ソースを分離する。Partner Center へ渡すのは `docs/store/listing/import/` で、CSV は 1 つだけ置く。言語別の値は `listingData.csv` の `en` / `ja` 列で管理する。

`docs/store/listing/design-source/` は編集用の元データ、`docs/store/listing/import/` は Partner Center にそのまま渡す投入物とする。Partner Center から export した CSV や `listingassets/...` URL を残す場合は、必要になった時点で `docs/store/listing/results/` を作成して保存する。

### import 手順メモ

1. `docs/store/listing/import/` を開く。
2. フォルダ直下に `listingData.csv` が 1 つだけあることを確認する。
3. CSV ヘッダーが Partner Center の英語 export と同じ構成 (`Field`,`ID`,`Type (Type)`,`default`,`en`,`ja`) であることを確認する。
4. CSV の画像フィールドが `import/assets/...` のように、ルートフォルダ名を含む相対パスになっていることを確認する。
5. Partner Center では `import` フォルダ自体を選んで import する。

## listingData 運用ルール

1. Partner Center の英語 export から取得した `listingData.csv` を原本として使い、ヘッダー名や列順を変えない。
2. `Field`,`ID`,`Type (Type)`,`default`,`en`,`ja` の構成を維持する。
3. `default` にカンマ、改行、ダブルクォートを含む場合は CSV ルールに従ってクォートする。
4. 文言や画像パスは `default` ではなく `en` / `ja` 列へ入力する。
5. import 用 CSV は `docs/store/listing/import/listingData.csv` に 1 つだけ置く。
6. listing 用画像は `docs/store/listing/import/assets/` 配下に置き、デザインソースは `docs/store/listing/design-source/` に分離する。
7. 画像フィールドには import 対象フォルダのルート名を含む相対パスのみを入れ、絶対パスや repo ルート基準のパス（例: `src/...`）は入れない。
8. Partner Center から export した CSV や `listingassets/...` URL を保存する場合は `docs/store/listing/results/` を作成し、入力値の CSV を上書きしない。
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

- [Deployment](../ops/Deployment.md)
- [Signing](../ops/Signing.md)
- [Versioning](../ops/Versioning.md)
- [ReleaseNotes](../ops/ReleaseNotes.md)

## 参考（Microsoft Learn）

- https://learn.microsoft.com/ja-jp/windows/apps/publish/publish-your-app/msix/create-app-submission
- https://learn.microsoft.com/ja-jp/windows/apps/publish/publish-your-app/msix/create-app-store-listing
- https://learn.microsoft.com/ja-jp/windows/apps/publish/publish-your-app/msix/app-properties
