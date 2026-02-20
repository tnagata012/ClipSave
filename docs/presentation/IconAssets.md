# アイコン運用

ClipSave のアイコンを Windows 11 でぼやけず一貫表示するための実装方針と運用手順を定義します。

## 責務分離

- WPF（ウィンドウ / Alt+Tab / トレイ）
  - `src/ClipSave/Assets/ClipSave.ico`
- MSIX（スタート / 検索 / タスクバー）
  - `src/ClipSave.Package/Images/Square44x44Logo*`
  - `src/ClipSave.Package/Images/Square150x150Logo*`
  - `src/ClipSave.Package/Images/Wide310x150Logo*`
  - `src/ClipSave.Package/Images/SplashScreen*`
  - `src/ClipSave.Package/Images/StoreLogo.png`

## ソース資産

- デザイン原本（PowerPoint）は `assets/ClipSave.icon-design-source.pptx`
- 生成元（ICO）は `assets/icon/ClipSave.master.ico`
- 画像差し替え時は以下の順で更新する
  1. `assets/ClipSave.icon-design-source.pptx` を編集
  2. `.ico` を出力して `assets/icon/ClipSave.master.ico` を上書き
  3. `.\scripts\sync-icon-assets.ps1` を実行

## 生成コマンド

```powershell
.\scripts\sync-icon-assets.ps1
```

生成される主なファイル:

- `src/ClipSave/Assets/ClipSave.ico`（16/20/24/32/40/48/64/256）
- `src/ClipSave.Package/Images/Square44x44Logo.png`
- `src/ClipSave.Package/Images/Square44x44Logo.scale-200.png`
- `src/ClipSave.Package/Images/Square44x44Logo.targetsize-*_altform-unplated.png`
- `src/ClipSave.Package/Images/Square150x150Logo.png`
- `src/ClipSave.Package/Images/Square150x150Logo.scale-200.png`
- `src/ClipSave.Package/Images/Wide310x150Logo.png`
- `src/ClipSave.Package/Images/Wide310x150Logo.scale-200.png`
- `src/ClipSave.Package/Images/SplashScreen.png`
- `src/ClipSave.Package/Images/SplashScreen.scale-200.png`
- `src/ClipSave.Package/Images/StoreLogo.png`

## site 反映（手動）

- `site/assets/ClipSave.ico` は `sync-icon-assets.ps1` の生成対象にしない
- サイト用アイコン更新時は手動でコピーする

```powershell
Copy-Item -Force src/ClipSave/Assets/ClipSave.ico site/assets/ClipSave.ico
```

## targetsize 方針（Square44x44Logo）

優先供給サイズ:

- 16, 20, 24, 30, 32, 36, 40, 44, 48, 64, 96, 256

命名:

- `Square44x44Logo.targetsize-<size>_altform-unplated.png`

## デザイン注意点（小サイズ）

- 16/20/24 は単純縮小で破綻しやすいので、必要なら手動で描き分ける
- 1px グリッドを基準にし、半透明エッジを過剰に使わない
- light/dark 両テーマと 100/125/150/200% DPI で最終確認する
