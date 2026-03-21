# 検証アーティファクト導入手順

**このドキュメントの目的**: `dev-latest` / `rc-X.Y-latest` / 確定タグ `X.Y.Z` の `*.msixbundle` を検証し、導入する手順を定義します。
`dev-latest` / `rc-X.Y-latest` / 確定タグ `X.Y.Z` の `*.msixbundle` は、開発者向けの未署名成果物です。
本番配布（一般ユーザー向け）は Store チャネルを利用してください。

## 対象

- Dev チャネル: `dev-latest` / `dev-package-*`
- RC チャネル: `rc-X.Y-latest` / `rc-package-*`
- 確定版: GitHub Release `X.Y.Z` / `release-archive-*`
- `release/X.Y` ブランチの最新候補タグは `rc-X.Y-latest`（例: `release/1.3` → `rc-1.3-latest`）

## 事前準備

1. Windows の「設定 > プライバシーとセキュリティ > 開発者向け」で開発者モードを ON にする。
2. `gh attestation verify` を使う場合は GitHub CLI（`gh`）をインストールする。
3. `gh auth login` で GitHub CLI にログインする。
4. `Add-AppxPackage -AllowUnsigned` を実行する PowerShell は、管理者として起動する。

## 導入手順

1. 公式チャネル（GitHub Releases / Actions artifacts）から `*.msixbundle` と `SHA256SUMS.txt` を取得する。
   - 最新候補を試す場合は `dev-latest` / `rc-X.Y-latest` を使う。
   - 特定の確定版を試す場合は GitHub Release `X.Y.Z` を使う。
2. 管理者 PowerShell で `scripts/install-artifact.ps1` を実行する。

以下の例は管理者 PowerShell をリポジトリルートで実行し、成果物を `$artifactDir` に置く想定です。

```powershell
$artifactDir = ".\artifacts"
$bundle = @(Get-ChildItem (Join-Path $artifactDir "*.msixbundle"))
if ($bundle.Count -ne 1) { throw "Expected exactly one .msixbundle, found $($bundle.Count)" }
$bundlePath = $bundle[0].FullName

.\scripts\install-artifact.ps1 `
  -BundlePath $bundlePath `
  -ChecksumPath (Join-Path $artifactDir "SHA256SUMS.txt") `
  -Channel dev `
  -SourceRef refs/heads/main
```

RC を導入する場合は `-Channel rc -SourceRef refs/heads/release/X.Y`、確定版アーカイブを導入する場合は `-Channel archive -SourceRef refs/tags/X.Y.Z` を使います。既存の Preview package を置き換える必要がある場合は `-RemoveExisting` を追加します。

`install-artifact.ps1` は、`SHA256` と GitHub Artifact Attestation の検証、管理者権限チェック、既存 Preview package の削除、`Add-AppxPackage -AllowUnsigned` をまとめて処理します。

## チャネル切り替え時の注意

Dev / RC / Archive は `ClipSave.Preview` identity で配布され、Store 版とは別パッケージとして扱われます。Store 版と Preview 版は共存できますが、設定や LocalState は共有されません。

Dev（例: `1.1.0.42`）の後に RC/Archive（`1.1.0.0`）を導入すると Preview identity 内でダウングレード判定になるため、先に Dev をアンインストールしてください。

RC 候補同士、および RC 候補から同版 Archive への切り替えでも、同一 Preview identity / 同一 package version（`X.Y.Z.0`）のため上書き導入できないことがあります。別候補へ切り替える場合も、必要に応じて既存の Preview パッケージを削除してから導入してください。

確定タグ `X.Y.Z` に対応するアーカイブ版は Store 公開の有無と独立しているため、試験リリースも同じ手順で導入できます。
GitHub Release 側で `prerelease` 表示になっていても、導入手順や真正性確認の方法は変わりません。

```powershell
Get-AppxPackage | Where-Object { $_.Name -eq "ClipSave.Preview" } | Remove-AppxPackage
```

## 関連ドキュメント

- [UsageGuide](../UsageGuide.md) — 一般利用者向けの使い方
- [ReleaseProcess](../release/ReleaseProcess.md) — 系列、タグ、チャネルの全体像
- [ReleaseGuide](../release/ReleaseGuide.md) — 配布ガイド
- [Signing](Signing.md) — 署名方針
