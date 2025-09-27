# OwnerKeeper 実装スケジュール

## 前提
- 開発ルート: `csharp/OwnerKeeper`（本体）、`csharp/OwnerKeeper.Tests`（MSTest）、共通設定は `csharp/Directory.Build.props` に配置する。
- 各ステップは「実装 → MSTest による検証 → レビュー → 必要ならリファクタリング → 再テスト」のサイクルで進める。
- ステップは独立した Pull Request/コミット単位を想定し、原則としてひとつのステップが終わってから次に進む。

## フェーズ0: プロジェクトセットアップ
1. **ソリューション雛形作成**
   - `dotnet new sln`、`dotnet new classlib`（OwnerKeeper）、`dotnet new mstest`（OwnerKeeper.Tests）。
   - `Directory.Build.props` に共通ターゲットフレームワーク (.NET 8)、nullable、treat warnings as errors 等を設定。
2. **テスト基盤整備**
   - MSTest 用の `GlobalUsings.cs` など整備。
   - CI を想定した `dotnet test` コマンド確認（ローカルで実行）。
3. **ベース構造確認**
   - サンプルクラスとテストを配置し、ビルド/テストが通ることを確認。

*レビュー後、リファクタリング反映 → 再テスト → 次フェーズへ。*

## フェーズ1: 基礎ドメイン構築
目的: 仕様書 3.2 および 4.2 の基礎型を定義する。
- 実装: `ResourceId`, `CameraState`, `CameraMetadata`, `OperationTicket`, `ErrorCode`, `CameraConfiguration` 等のモデルとバリデーション補助。
- テスト: 値オブジェクトの不変条件、`OperationTicketStatus` の挙動、`ErrorCode` 区分を MSTest で検証。

## フェーズ2: リソース管理の基盤
目的: 仕様書 5.1 / REQ-RM-001~002 の実装。
- 実装: `ResourceDescriptor`, `ResourceManager`（Acquire/Release、ReaderWriterLockSlim、SemaphoreSlim）
- テスト: 競合時の `OperationTicketStatus.FailedImmediately` 返却、所有権解放タイミング、マルチスレッドシミュレーション（Task 並列）

## フェーズ3: 状態遷移エンジン
目的: 仕様書 5.2 / ST-1 遷移テーブル。
- 実装: `StateTransitionRule`, `StateMachine`、遷移検証＆エラーコードマッピング。
- テスト: 正常遷移、誤用による例外、ランタイム失敗時の即時失敗チケット判定。

## フェーズ4: OperationScheduler とイベントディスパッチ
目的: 仕様書 3.2 / 4.3 / 5.3 / 8.
- 実装: `OperationScheduler`（Channels ベース）、イベントディスパッチ (`Task.Run` + try/catch)、Logger 連携の下地。
- テスト: キュー投入→イベント完了、例外発生時のログ記録、キャンセル/タイムアウトハンドリングのユニットテスト。

## フェーズ5: OwnerSession API
目的: 仕様書 4.2, 4.4 の実体化。
- 実装: `OwnerSession`（`StartStreaming` 等の同期 API、OperationTicket 発行、`GetCurrentState`）。
- テスト: メソッド呼び出しの即時失敗/受理フロー、状態問い合わせ、CancellationToken 適用。

## フェーズ6: OwnerKeeperHost と初期化/終了管理
目的: 仕様書 4.1, 11.3 / REQ-IN-001~005。
- 実装: `OwnerKeeperHost` シングルトン、初期化フロー、`Shutdown`/`Dispose`、ファイナライザ対応。
- テスト: 初期化二重呼び出し防止、未初期化操作の例外、`Shutdown` 後の状態、Dispose/GC のテスト（必要に応じて WeakReference を利用）。

## フェーズ7: ハードウェア抽象化層とスタブ
目的: 仕様書 3.2 / 7 / TS。
- 実装: `IHardwareResource`, `CameraAdapter`, `CameraStub`, `IHardwareResourceFactory`。
- テスト: スタブを利用した Start/Stop/UpdateConfiguration の E2E テスト、ArrayPool バッファが返却されることの確認。

## フェーズ8: ログ・メトリクス・デバッグモード
目的: 仕様書 8, 11.2。
- 実装: `Logger` ラッパー、`MetricsCollector`, デバッグモードでの追加出力。
- テスト: ログ出力が適切に行われるか（テストダブル使用）、メトリクス更新の確認、デバッグモード ON/OFF 切り替え。

## フェーズ9: 統合テスト & ドキュメント更新
- 実装: OwnerKeeperHost + OwnerSession + CameraStub を用いた統合シナリオ（正常系・競合・エラー・リカバリ）。
- テスト: MSTest で統合テストを追加。必要に応じて Playbooks（ドキュメント）があれば更新。
- ドキュメント: README/仕様書/要件との整合性を最終確認し必要に応じて更新。

## フェーズ10: リファクタリング & リリース準備
- コードクリーンアップ、`Directory.Build.props` の最終調整、公開 API の XML ドキュコメント整備。
- バージョニング (`OwnerKeeperHost.LibraryVersion`) の設定とリリースノート草案作成。

---
各フェーズ完了後の流れ:
1. 実装 → MSTest による検証
2. 結果共有 → レビュー
3. レビュー指摘への対応（リファクタリング含む）
4. 再テスト → 次フェーズ移行

上記サイクルを繰り返し、常にテストがグリーンな状態を維持したまま段階的に機能を広げる。
