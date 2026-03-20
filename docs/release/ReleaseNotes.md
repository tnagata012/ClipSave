# GitHub Release Notes 運用ガイド

**このドキュメントの目的**: ClipSave の公開向け変更履歴を `Release Notes` Issues と GitHub Release の参照リンクで一貫して管理するための運用ルールを定義します。

## なぜ `CHANGELOG.md` で運用しないか

1. `Release Notes: Unreleased` と `Release Notes: X.Y` に分けた方が、作業中ドラフトと release line ごとの公開ノートを分離しやすい。
2. GitHub Release 本文は配布アーカイブの案内に寄せつつ、`Release Finalize` が issue 本文を snapshot として自動反映する方が運用が軽い。
3. repo 内ファイルと GitHub Release 本文を二重更新すると、patch release や backport で転記漏れが起きやすい。
4. PR、release 準備、Store 前確認が同じ issue title を参照できる。

## 基本ルール

1. 作業中の一次ソースは `Release Notes: Unreleased` Issue とする。
2. release line ごとの公開ノートは GitHub Issue `Release Notes: X.Y` とする。
3. GitHub Release `X.Y.Z` 本文の `Release Notes` セクションは、`Release Notes: X.Y` への参照と、その時点の issue 本文 snapshot を自動で持つ。
4. `Release Notes: X.Y` はその系列の公開向けメモとして必要に応じて更新する。
5. patch release でも issue は増やさず、同じ `Release Notes: X.Y` を更新する。
6. ユーザー影響のある PR は、対応する release-notes issue を更新する。
7. 通常の `main` 向け PR は `Release Notes: Unreleased`、`release/X.Y` 向け PR は当該 `Release Notes: X.Y` を更新する。
8. ユーザー影響のない PR は、release-notes issue の更新を不要とする。

## 運用要素

### `Release Notes: Unreleased`

Issue title: `Release Notes: Unreleased`

1. `main` で今後出す user-facing changes をここに集める。
2. user-facing bullet を短く保ち、必要なら `Added / Changed / Fixed` の見出しを使う。
3. release 準備時に、今回出荷する bullet だけを `Release Notes: X.Y` へ手動で移す。
4. 同じ title の open issue を重複作成しない。
5. この Issue はクローズしない。常に 1 つだけ open のまま維持する。

### `Release Notes: X.Y`

1. `release/X.Y` を切ったら 1 系列につき 1 つ作る。
2. `Prepare Release` workflow を使う場合は最小構成の issue を自動作成または再利用し、`create-release-branch.ps1` だけを使う場合は手動で作成する。
3. `Release Notes: Unreleased` から、今回の系列で出荷する bullet を手動で移したものを初期内容にする。
4. patch 版を出すたびに同じ issue を更新する。
5. issue 本文は GitHub Release からリンクされる公開向けノートとして読みやすく保つ。
6. GitHub Release `X.Y.Z` はこの issue へのリンクを持つが、release 本文には tag 時点の snapshot も残す。
7. issue 自体は系列内で後から更新されるため、GitHub Release から辿る issue リンク先は常に系列の最新状態を指す。
8. 同じ title の open issue を重複作成しない。
9. 現行サポート系列である間は open のまま維持し、その系列が [ReleaseProcess](ReleaseProcess.md) の `frozen / unsupported` へ移った時点でクローズする。

### Pull Request

1. `main` 向けの user-facing PR は、`Release Notes: Unreleased` を更新する。
2. `release/X.Y` 向けの user-facing PR は、当該 `Release Notes: X.Y` を更新する。
3. `main` で user-facing change を入れ、その後 `release/X.Y` へ backport した場合は、release 側 PR で `Release Notes: X.Y` も更新する。
4. docs / CI / internal-only な PR は、release-notes issue を更新しない。
5. PR テンプレの `Release Notes` チェックは、更新済みか不要かだけを示す。

## 更新フロー

### release line 開始前

1. `main` に入る user-facing change は `Release Notes: Unreleased` を一次ソースにする。
2. 機能ごとの Issue は必須にしない。必要な議論があるときだけ補助的に使う。

### リリース開始時

1. `Prepare Release` または `create-release-branch.ps1` で `release/X.Y` を作る。
2. `Prepare Release` を使う場合は `Release Notes: X.Y` Issue が自動作成または再利用される。`create-release-branch.ps1` だけを使う場合は手動で作成または更新する。
3. `Release Notes: Unreleased` から今回の系列で出す bullet を手動で移し、残りは `Unreleased` に残す。

### release line 運用中

1. `main` 側の user-facing change は引き続き `Release Notes: Unreleased` を更新する。
2. `release/X.Y` 側の安定化 PR / backport PR は、必要に応じて `Release Notes: X.Y` を更新する。
3. `Release Notes: X.Y` は、その系列で次に出す版の候補を保持し、tag 時点の内容は GitHub Release 側に snapshot される。

### tag 前

1. `Release Notes: X.Y` Issue を見直し、今回の出荷内容として読めることを確認する。
2. 必要なら merged PR や関連 Issue を見て bullet を補い、ユーザー向け文面に磨く。

### Release Finalize と再実行

1. tag `X.Y.Z` を push したら `Release Finalize` が GitHub Release `X.Y.Z` を更新する。
2. workflow は GitHub Release 本文の `Release Notes` セクションに `Release Notes: X.Y` への参照と、その時点の issue 本文 snapshot を入れる。
3. issue が未作成でも release 自体は publish されるが、参照は検索リンク fallback になり、`store-checklist.ps1` は未整備として扱う。
4. 既存 archive が揃っている版への再実行は、archive を再生成せず metadata と本文参照を更新する。
5. workflow は `Release Notes` / `Release Archive (Unsigned)` / `Operator Notes` / `Store Submission Log` の 4 セクションを保持する。

## 記載スタイル

| 観点 | 推奨 |
| ---- | ---- |
| 視点 | ユーザー視点で「何がどう変わるか」を書く |
| 粒度 | 1 項目に複数変更を詰め込みすぎない |
| 文言 | 内部実装名だけで終わらせず、挙動の変化を明示する |
