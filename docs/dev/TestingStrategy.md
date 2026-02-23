# テスト戦略

## 目的

**このドキュメントの目的**: ClipSave のテスト運用を次の 2 点で統一し、テスト品質と再現性を高めます。

- どの層に、何を、どの粒度で書くかを揃える（迷いをなくす）
- ローカルと CI で同じ手順・同じ判定基準で実行する（再現性を上げる）

---

## テスト哲学（Integration-first / Cost-aware）

ClipSave では **Classical (Detroit-style) / Sociable Tests** を基本とし、**Integration テストを品質保証の中心**に置く。

- **品質保証の主戦場は Integration**
  実際の構成に近い組み合わせ（複数コンポーネント、I/O、OS 機能）でバグを捕まえることを重視する。
- **ただし Integration は高コスト**
  時間・不安定さ・環境差を伴うため、Integration は **Integration でしか担保できない境界**に集中投資する。
- **テストのためにプロダクションコードの構造を変えない**
  インターフェース抽出や `virtual` 付与はテスト目的では行わない。
- `InternalsVisibleTo` により `internal` へアクセスし、実オブジェクトを中心に検証する。
- private 実装へのリフレクション直接呼び出し（`BindingFlags.NonPublic` など）は原則行わない。
  必要な場合は最小の `internal` テストフックを設計し、振る舞いとして検証する。
- **モックは原則使わない**
  ただし **挙動に影響しない観測・通知系の依存**（例: `ILogger<T>`）は運用上の例外として許容する。
  ※例外を導入した場合は、理由を PR で明記しレビューで合意する。
- **UI テストは UI 固有仕様の検証に限定する**
  WPF の表示・バインディング・Dispatcher タイミングなど、Integration で代替できない仕様確認を担う。
  代表フローの Smoke は最低限ラインとして維持し、UI 全網羅は行わない。

---

## テスト層の責務

| 層           | プロジェクト                             | 主目的                                                               | 外部依存                              | SPEC-ID     |
| ----------- | ---------------------------------- | ----------------------------------------------------------------- | --------------------------------- | ----------- |
| Unit        | `tests/ClipSave.UnitTests/`        | 純ロジックや軽量契約の正確さ（速く・安定）                                             | 原則実オブジェクト（`ILogger<T>` のモックは例外許容） | 不要          |
| Integration | `tests/ClipSave.IntegrationTests/` | **品質の中心**：複数コンポーネント統合の挙動検証                                        | ファイルシステム・クリップボード等を必要に応じて実利用       | 推奨（仕様検証は必須） |
| UI          | `tests/ClipSave.UiTests/`          | **UI 固有仕様の検証**：WPF 画面/トレイ操作のうち Integration で代替不能な挙動を確認（Smoke を含む） | WPF Window/Dispatcher を実利用        | 推奨（仕様検証は必須） |

`src/ClipSave/ClipSave.csproj` は `InternalsVisibleTo` で上記 3 プロジェクトに `internal` を公開する。

---

## テスト層の選定ルール

### 基本方針

品質保証の主戦場は Integration だが、Integration は高コストである。
そのため **Integration でしか担保できない箇所に集中投資**し、純ロジックは Unit に落として重複を避ける。

Integration は特に以下に集中する。

- OS/外部リソース境界（ファイルシステム、クリップボード、ホットキー等）
- 複数コンポーネントの結線（イベント/通知/永続化の連鎖）
- 失敗時の影響が大きいフロー（保存、復元、設定反映など）

### 判定手順

新規テストは次の順で判定する。

1. **純ロジックのみで完結するか**
   該当するなら Unit を選ぶ（例: 変換、命名、パース）。
2. **複数コンポーネントや I/O 境界を跨ぐか**
   該当するなら Integration を第一選択にする。
3. **UI 実行基盤でしか再現できないか**
   該当する場合のみ UI を選ぶ。
   この条件を満たす仕様検証は、Smoke 以外でも UI テストで扱ってよい。

### 重複の扱い

- 同一仕様の重複は、重い層ほど減らす。
- Unit でロジックを密に、Integration で結線を薄く、UI で代替不能な仕様に絞って検証する。

---

## ディレクトリと配置規約

### Unit

- 本体 `src/ClipSave` の構造を基本的に鏡写しにする。
- テスト共通ヘルパーは `TestInfrastructure/` に限定する。
- クラス名は `<対象クラス名>Tests`。
- クラスには `[UnitTest]` を付与し、カテゴリ分類を統一する。
- UI 要素でも、ランタイム起動不要な静的契約検証（XAML 解析など）は Unit で扱ってよい。

### Integration

- 仕様カテゴリ（SPEC 群）ベースでフォルダを分ける。
- クラス名は `<対象コンポーネント名>IntegrationTests`。

### UI

- `src/ClipSave` 構造に準じて `Views/`・`Services/` へ配置する。
- クラス名は `<対象コンポーネント名>UiTests`。

---

## テスト作成ルール

### 命名

メソッド名は振る舞いを表す英語で記述する。

```text
<Target>_<ConditionOrAction>_<ExpectedResult>
```

例:

- `SaveButton_IsEnabledOnlyWhenUnsavedChangesExist`
- `HandleTrayIconClick_LeftClick_RaisesSettingsRequested`

### xUnit 属性の使い分け

| 属性                          | 用途                                     |
| --------------------------- | -------------------------------------- |
| `[UnitTest]`                | Unit テストクラスのカテゴリ分類（クラス属性）           |
| `[Fact]`                    | 通常のテスト                                 |
| `[StaFact]`                 | WPF・NotifyIcon・Clipboard など STA 必須のテスト |
| `[Theory]` + `[InlineData]` | 入力違いの同一ロジック検証                          |

### SPEC トレース（Integration/UI：仕様検証は必須）

- クラスに `[IntegrationTest]` または `[UiTest]` を付与する。
- **仕様検証テストには**メソッドに `[Spec("SPEC-xxx-yyy")]` を付与する。
- 補助テスト（セットアップ/後始末/耐障害性/回帰補助など）は `[Spec]` を省略してよい。
- 1 テストで複数 SPEC を検証する場合は `[Spec]` を複数付与してよい。

例:

```csharp
[UiTest]
public class SettingsWindowUiTests
{
    [StaFact]
    [Spec("SPEC-040-007")]
    public void SaveButton_IsEnabledOnlyWhenUnsavedChangesExist() { ... }
}
```

### テスト文言

- コメント、`Should(..., "reason")`、テスト用例外メッセージは英語で統一する。
- 仕様検証に必要なデータ（ローカライズ文字列など）は日本語を許容する。

### 契約テストの書き方（壊れにくさ優先）

- ソースコード文字列への単純一致（`File.ReadAllText(...).Should().Contain("...")`）で仕様を保証しない。
- 契約確認は、可能な限り振る舞い検証または構造化データの検証（例: `XDocument` で `App.xaml` / `app.manifest` / `*.csproj` を解析）で行う。
- 実装詳細（private フィールド名・private メソッド名）に依存した検証は避ける。

---

## 安定運用ルール

### 並列実行（現行方針）

- **当面は Unit / Integration / UI すべて並列無効**とする（再現性優先）。
- `xunit.runner.json` 基本設定:

  - `parallelizeAssembly: false`
  - `parallelizeTestCollections: false`

理由:

- STA 制約（WPF/Clipboard/NotifyIcon）
- グローバルホットキーやファイル I/O の競合回避
- CI 再現性の向上

### リソースクリーンアップ

- 一時ディレクトリは `ClipSave_{TestType}_{Guid}` 形式で作成する。
- テスト終了時に必ず削除する（best effort で可）。
- UI テストでは `IDisposable` なコンテキストで Window の生成と破棄を管理する。

### UI テストの共通インフラ

- `WpfTestHost`: `Application` 初期化と `Dispatcher` キュー消化
- `LogicalTreeSearch`: 論理ツリーからコントロール探索
- `TestMetadataAttributes`: `[UiTest]` と `[Spec]` の Trait 定義

### UI Smoke の最低限ライン

Smoke は「起動」「主要画面表示」「主要操作（1〜2件）」「終了」の成立を確認する最小セットとする。

---

## 実行パターン

### 推奨: スクリプト実行

`run-tests.ps1` は `tests/` 配下の `.csproj` を再帰走査し、テストプロジェクトを自動判定して順次 `dotnet test` を実行する。

```powershell
./scripts/run-tests.ps1 -Configuration Debug -Verbosity normal
./scripts/run-tests.ps1 -Configuration Release -NoBuild
```

### フィルタ実行

```powershell
# SPEC 単位
dotnet test tests/ClipSave.IntegrationTests/ClipSave.IntegrationTests.csproj --filter "SpecId=SPEC-021-003"

# カテゴリ単位
dotnet test tests/ClipSave.IntegrationTests/ClipSave.IntegrationTests.csproj --filter "Category=Integration"
dotnet test tests/ClipSave.UiTests/ClipSave.UiTests.csproj --filter "Category=UI"
```

### 仕様トレース整合チェック

`Specification.md` とテストの `Spec` 属性の対応を機械的に確認する。

```powershell
.\scripts\check-spec-coverage.ps1
```

用途:
- 仕様 ID の振り直し・名称変更時に、テスト側の参照漏れを検出する
- 孤立した `Spec` 属性（仕様に存在しない ID）を早期に検出する
- 未カバー SPEC や孤立した SPEC がある場合は失敗し、CI でブロックする

---

## CI 連携

GitHub Actions では、すべてのビルド系ワークフローでテストを実行する。

| Workflow            | 起動条件                       | テスト手順                                                                 |
| ------------------- | -------------------------- | --------------------------------------------------------------------- |
| `pr-check.yml`      | PR to `main` / `release/*` | `run-tests.ps1`（Debug） + `LocalizationResourceCompletenessTests` 先行実行 + `check-spec-coverage.ps1` |
| `dev-build.yml`     | Push to `main`             | `run-tests.ps1`（Release）                                              |
| `release-build.yml` | Push to `release/*`        | `run-tests.ps1`（Release）                                              |
| `store-publish.yml` | Manual dispatch            | `run-tests.ps1`（Release）                                              |

---

## PR 前チェックリスト

- 変更内容に対して適切な層（Unit / Integration / UI）へテストを追加した。
- 仕様検証を目的とする Integration/UI テストへ `Spec` 属性を付与した。
- 一時リソースの生成・破棄が対になっている。
- `./scripts/run-tests.ps1` がローカルで成功する。
- `Specification.md` または `Spec` 属性を変更した場合、`./scripts/check-spec-coverage.ps1` が成功する。
- 新規ヘルパーを追加した場合、`TestInfrastructure/` に集約した。

---

## 関連ドキュメント

- [仕様](Specification.md) - 詳細仕様（SPEC-ID）
- [アーキテクチャ](Architecture.md) - 技術的な実装詳細
