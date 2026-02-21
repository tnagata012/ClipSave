# 検証アーティファクト導入手順

`dev-latest` / `release-latest` の `*.msixbundle` は、開発者向けの未署名成果物です。
本番配布（一般ユーザー向け）は Store チャネルを利用してください。

## 対象

- Dev チャネル: `dev-latest` / `dev-package-*`
- Release チャネル: `release-latest` / `release-package-*`

## 事前準備

1. Windows の「設定 > プライバシーとセキュリティ > 開発者向け」で開発者モードを ON にする。
2. `gh attestation verify` を使う場合は GitHub CLI（`gh`）をインストールする。
3. `gh auth login` で GitHub CLI にログインする。

## 導入手順

1. 公式チャネル（GitHub Releases / Actions artifacts）から `*.msixbundle`（1ファイル）と `SHA256SUMS.txt` を取得する。
2. `SHA256` と GitHub Artifact Attestation を検証する（推奨: `scripts/verify-artifact.ps1`）。
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

# Release チャネルを検証する場合は -Channel release を使い、
# 可能であれば -SourceRef refs/heads/release/X.Y も指定する。

# 2) インストール（未署名）
Add-AppxPackage -Path $bundlePath -AllowUnsigned
```

## チャネル切り替え時の注意

Dev（例: `1.1.0.42`）の後に Release（`1.1.0.0`）を導入するとダウングレード判定になるため、先に Dev をアンインストールしてください。

```powershell
Get-AppxPackage *ClipSave* | Remove-AppxPackage
```

## 関連ドキュメント

- [UsageGuide](../UsageGuide.md) - 一般利用者向けの使い方
- [Signing](Signing.md) - 署名方針
- [Deployment](Deployment.md) - 配布 Runbook
- [Versioning](Versioning.md) - 版数規約
