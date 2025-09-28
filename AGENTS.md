# OwnerKeeper プロジェクトガイドライン

## 1. 参照ドキュメント
- 要件定義: [`docs/REQUIREMENTS.md`](docs/REQUIREMENTS.md)
- 要件分析 (SysML): [`docs/REQUIREMENTS_DIAGRAM.md`](docs/REQUIREMENTS_DIAGRAM.md)
- 仕様書: [`docs/SPECS.md`](docs/SPECS.md)
- 実装計画: [`docs/SCHEDULE.md`](docs/SCHEDULE.md)

各実装・レビュー時には上記ドキュメントを基準とし、対応する要件IDや仕様節を明記して作業すること。

## 2. コーディングルール
1. **フォーマッタ適用**: コミット前に CSharpier のみを実行する（`.githooks/pre-commit` により自動適用）。設定は `csharp/.csharpierrc` を使用し、`csharp/` 配下に変更がある場合のみ実行する。
2. **XML コメント**: クラス・構造体・インターフェイス・メソッド・プロパティなど公開メンバーには XML ドキュコメントを必ず付与する。コメント内で関連する要件ID（例: `REQ-ST-003`）や仕様セクションを示すこと。
3. **インラインコメント**: ロジックが文書化された要件や仕様に基づく場合、`// (REQ-XX-YYY)` や `// (SPECS §n.n)` のように引用して根拠を示す。必要最小限とし、処理意図の補足に限定する。
4. **C# バージョン**: プロジェクトの LangVersion は C# 10 を使用する。`Directory.Build.props` で統一設定とする。
5. **テスト同時進行**: 実装フェーズごとに MSTest を用いたテストを作成・更新し、仕様書・要件に対する根拠をテスト名やコメントで示す。
6. **コメント言語**: コード内の XML ドキュメント/インラインコメントは英語で記述する（要件ID・仕様参照は従来どおり併記）。
7. **コミットメッセージ**: サブジェクト1行 + 空行 + 本文。Conventional Commits（例: `feat`, `fix`, `chore`）を推奨し、可能な限り本文に要件IDや仕様の参照（`REQ-...`, `SPECS §...`）や `Refs:` を記載する。本文の改行幅は固定しない（読みやすさを優先）。

## 3. 作業フロー
- docs/SCHEDULE.md に沿ってフェーズ単位の「実装 → テスト → レビュー → リファクタリング → 再テスト」を順守する。
- 各フェーズの作業開始前に、該当要件IDを洗い出し AGENTS.md を参照した根拠付けを行うこと。
- レビュー対応後は必ず formatter を再適用し、テストを緑に戻してから次フェーズへ移る。

## 4. 補足
- C# プロジェクトは `csharp/OwnerKeeper`, テストは `csharp/OwnerKeeper.Tests` で管理し、`Directory.Build.props` を共通設定に使用する。
- MSTest 以外のテストフレームワークは使用しない。
- 追加ドキュメントや変更が発生した場合は、AGENTS.md を更新して参照関係を維持する。
- フォーマッタとスタイル設定は `csharp/` 配下に配置する（`csharp/.csharpierrc`, `csharp/.editorconfig`, `csharp/.config/dotnet-tools.json`）。
