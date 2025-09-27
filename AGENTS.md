# OwnerKeeper プロジェクトガイドライン

## 1. 参照ドキュメント
- 要件定義: [`docs/REQUIREMENTS.md`](docs/REQUIREMENTS.md)
- 要件分析 (SysML): [`docs/REQUIREMENTS_DIAGRAM.md`](docs/REQUIREMENTS_DIAGRAM.md)
- 仕様書: [`docs/SPECS.md`](docs/SPECS.md)
- 実装計画: [`docs/SCHEDULE.md`](docs/SCHEDULE.md)

各実装・レビュー時には上記ドキュメントを基準とし、対応する要件IDや仕様節を明記して作業すること。

## 2. コーディングルール
1. **フォーマッタ適用**: コミット前に CSharpier および `dotnet format` を実行する（`.githooks/pre-commit` により自動適用）。
2. **XML コメント**: クラス・構造体・インターフェイス・メソッド・プロパティなど公開メンバーには XML ドキュコメントを必ず付与する。コメント内で関連する要件ID（例: `REQ-ST-003`）や仕様セクションを示すこと。
3. **インラインコメント**: ロジックが文書化された要件や仕様に基づく場合、`// (REQ-XX-YYY)` や `// (SPECS §n.n)` のように引用して根拠を示す。必要最小限とし、処理意図の補足に限定する。
4. **C# バージョン**: プロジェクトの LangVersion は C# 10 を使用する。`Directory.Build.props` で統一設定とする。
5. **テスト同時進行**: 実装フェーズごとに MSTest を用いたテストを作成・更新し、仕様書・要件に対する根拠をテスト名やコメントで示す。

## 3. 作業フロー
- docs/SCHEDULE.md に沿ってフェーズ単位の「実装 → テスト → レビュー → リファクタリング → 再テスト」を順守する。
- 各フェーズの作業開始前に、該当要件IDを洗い出し AGENTS.md を参照した根拠付けを行うこと。
- レビュー対応後は必ず formatter を再適用し、テストを緑に戻してから次フェーズへ移る。

## 4. 補足
- C# プロジェクトは `csharp/OwnerKeeper`, テストは `csharp/OwnerKeeper.Tests` で管理し、`Directory.Build.props` を共通設定に使用する。
- MSTest 以外のテストフレームワークは使用しない。
- 追加ドキュメントや変更が発生した場合は、AGENTS.md を更新して参照関係を維持する。
