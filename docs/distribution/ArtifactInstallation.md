# 検証アーティファクト導入手順

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

## 導入手順

1. 公式チャネル（GitHub Releases / Actions artifacts）から `*.msixbundle` と `SHA256SUMS.txt` を取得する。
   - 最新候補を試す場合は `dev-latest` / `rc-X.Y-latest` を使う。
   - 特定の確定版を試す場合は GitHub Release `X.Y.Z` を使う。
2. `SHA256` と GitHub Artifact Attestation を検証する（attestation の別途ダウンロードは不要。推奨: `scripts/verify-artifact.ps1`）。
3. `Add-AppxPackage -AllowUnsigned` で導入する。

以下の例はリポジトリルートで実行し、成果物を `$artifactDir` に置く想定です。

```powershell
$artifactDir = ".\artifacts"
$bundle = @(Get-ChildItem (Join-Path $artifactDir "*.msixbundle"))
if ($bundle.Count -ne 1) { throw "Expected exactly one .msixbundle, found $($bundle.Count)" }
$bundlePath = $bundle[0].FullName

# 1) 検証（推奨）
# この例は Dev チャネル（main 由来）を想定
.\scripts\verify-artifact.ps1 `
  -BundlePath $bundlePath `
  -ChecksumPath (Join-Path $artifactDir "SHA256SUMS.txt") `
  -Channel dev `
  -SourceRef refs/heads/main

# RC を検証する場合は -Channel rc を使い、
# 可能であれば -SourceRef refs/heads/release/X.Y も指定する。
# 確定版アーカイブを検証する場合は -Channel archive を使い、
# 可能であれば -SourceRef refs/tags/X.Y.Z も指定する。

# 2) インストール（未署名）
Add-AppxPackage -Path $bundlePath -AllowUnsigned
```

## チャネル切り替え時の注意

Dev（例: `1.1.0.42`）の後に RC/Archive（`1.1.0.0`）を導入するとダウングレード判定になるため、先に Dev をアンインストールしてください。

確定タグ `X.Y.Z` に対応するアーカイブ版は Store 公開の有無と独立しているため、試験リリースも同じ手順で導入できます。
GitHub Release 側で `prerelease` 表示になっていても、導入手順や真正性確認の方法は変わりません。

```powershell
Get-AppxPackage *ClipSave* | Remove-AppxPackage
```

## 関連ドキュメント

- [UsageGuide](../UsageGuide.md) — 一般利用者向けの使い方
- [ReleaseProcess](../release/ReleaseProcess.md) — 系列、タグ、チャネルの全体像
- [ReleaseGuide](../release/ReleaseGuide.md) — 配布ガイド
- [Signing](Signing.md) — 署名方針
