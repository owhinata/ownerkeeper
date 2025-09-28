# OwnerKeeper 仕様書

## 1. ドキュメント情報
- ドキュメントID: OK-SPEC-001
- バージョン: 0.1
- 作成日: 2025-09-27
- 対象: ownerkeeper リソース所有権管理ライブラリ (.NET 8)
- 参照: docs/REQUIREMENTS.md (OK-REQ-001), docs/REQUIREMENTS_DIAGRAM.md

## 2. 目的とスコープ
- 本仕様書は OK-REQ-001 の要件群を満たすライブラリの設計指針・API仕様・内部コンポーネント構造を定義する。
- 仕様はカメラリソースを主対象とするが、将来的なハードウェア拡張（REQ-OV-003, REQ-FU-001）を考慮した抽象化を含む。

## 3. 全体アーキテクチャ
### 3.1 レイヤ構成
| レイヤ | 主要コンポーネント | 主な責務 | 関連要件 |
| --- | --- | --- | --- |
| APIレイヤ | `OwnerKeeperHost`, `IOwnerSession` | 初期化、インターフェイス払い出し、同期API提供、イベント購読 (REQ-IN-001, REQ-AI-001, REQ-OV-001) |
| コアサービス | `ResourceManager`, `OwnershipService`, `StateMachine`, `OperationScheduler` | 所有権管理、状態遷移、非同期実行、タイムアウト監視 (REQ-OW-001~004, REQ-ST-001~004, REQ-CT-002) |
| ハードウェア抽象化 | `IHardwareResource`, `CameraAdapter`, `ResourceTable` | カメラ等デバイスの抽象化・自動採番管理 (REQ-RM-001, REQ-RM-002, REQ-CF-003) |
| サポートサービス | `Logger`, `MetricsCollector`, `ConfigurationService`, `ErrorCatalog` | ログ、メトリクス、設定、エラーコード管理 (REQ-LG-001, REQ-MN-001, REQ-EC-003, REQ-CF-004) |

### 3.2 主要クラス図（テキスト）
- `OwnerKeeperHost` (Singleton)
  - メソッド: `Initialize(OwnerKeeperOptions options)`, `Shutdown()`, `CreateSession(string? userId = null, CancellationToken? token = null)`
  - `IDisposable` を実装し、`Dispose()` で `Shutdown()` を呼び出す。
  - 内部に `ResourceManager`, `MetricsCollector`, `Logger` への参照。
- `OwnerSession` (`IOwnerSession`, `IDisposable` 実装)
  - メソッド: `StartStreaming()`, `StopStreaming()`, `PauseStreaming()`, `ResumeStreaming()`, `UpdateConfiguration(CameraConfiguration config)`, `RequestStatus()`, `Reset()` 他。
  - イベント: `StartStreaming`, `StopStreaming`, `PauseStreaming`, `ResumeStreaming`, `UpdateConfiguration`, `Reset`, `StatusChanged`（各イベント名はAPIと同一; REQ-EV-001）。
- `ResourceManager`
  - `ConcurrentDictionary<ResourceId, ResourceDescriptor>` でリソーステーブルを保持 (REQ-RM-001)。
  - 所有権の獲得/解放を管理。`TryAcquire(ResourceId, OwnerSession)` 等。
- `StateMachine`
  - 状態遷移ルール（ST-1）を保持し、`Transition(ResourceId, CameraState, OperationType)` により状態を検証・更新 (REQ-ST-002~004)。
- `OperationScheduler`
  - 同期APIから渡された操作を非同期キューに投入し、結果をイベントで通知。`System.Threading.Channels` を利用予定。
- `CameraAdapter`
  - `IHardwareResource` を実装するカメラスタブ/実装。操作実行・設定変更・ストリーム制御を抽象化。

## 4. API仕様
### 4.1 エントリポイント (OwnerKeeperHost)
```csharp
public sealed class OwnerKeeperHost : IDisposable
{
    public static OwnerKeeperHost Instance { get; }
    public void Initialize(OwnerKeeperOptions options);                // REQ-IN-001
    public void Shutdown();                                            // REQ-IN-002
    public IOwnerSession CreateSession(string? userId = null, OwnerSessionOptions? options = null);
}
```
- `Initialize` は一度のみ許可。未初期化時の操作は `OwnerKeeperNotInitializedException` (ARG3002) を投げる (REQ-IN-003, REQ-ER-001)。
- `Shutdown` は所有権・リソースを同期的に開放し、再初期化を可能にする (REQ-IN-002)。
- `CreateSession` はユーザ識別子を受け取り、省略時に UUID を生成 (REQ-AI-001)。`OwnerSessionOptions` に `CancellationToken`, `TimeoutOverrides` を含む (REQ-CT-001, REQ-CT-002)。

### 4.2 セッション API (`IOwnerSession`)
```csharp
public interface IOwnerSession : IDisposable
{
    string SessionId { get; }                    // UUID or supplied ID, REQ-AI-001
    ResourceId ResourceId { get; }

    OperationTicket StartStreaming();            // REQ-SN-001
    OperationTicket StopStreaming();             // REQ-ST-003
    OperationTicket PauseStreaming();            // REQ-ST-003
    OperationTicket ResumeStreaming();           // REQ-ST-003
    OperationTicket UpdateConfiguration(CameraConfiguration configuration);  // REQ-CF-001~003
    OperationTicket RequestStatus();             // 状態ポーリング
    OperationTicket Reset();                     // REQ-EC-004
    CameraState GetCurrentState();               // 現在状態の同期取得, REQ-ST-003

    event StartStreamingEventHandler StartStreaming;          // REQ-EV-001/002
    event StopStreamingEventHandler StopStreaming;
    event PauseStreamingEventHandler PauseStreaming;
    event ResumeStreamingEventHandler ResumeStreaming;
    event UpdateConfigurationEventHandler UpdateConfiguration;
    event ResetEventHandler Reset;
    event StatusChangedEventHandler StatusChanged;
}
```
- `OperationTicket` は操作ID (`Guid`)、発行時刻、`OperationTicketStatus`、必要に応じて `ErrorCode` を保持し、イベントとの相関キーとして利用する (REQ-MN-001)。
- ランタイムエラーを非同期実行前に検知した場合は `OperationTicketStatus.FailedImmediately` と `ErrorCode` を設定して返却し、非同期処理は開始しない (REQ-ER-002, REQ-RC-002)。
- 非同期実行に進む操作は `OperationTicketStatus.Accepted` として返却され、後続処理は `OperationScheduler` が実行し結果イベントを発火する (REQ-OV-001, REQ-ER-003)。
- `GetCurrentState()` は現在の `CameraState` を同期的に返却し、監視やUI更新で即時に利用できる。内部的には `StateMachine` のスレッドセーフな状態参照を利用する。

```csharp
public readonly struct OperationTicket
{
    public Guid OperationId { get; }
    public OperationTicketStatus Status { get; }
    public ErrorCode? ErrorCode { get; }
    public DateTime CreatedAtUtc { get; }
}

public enum OperationTicketStatus
{
    Accepted,
    FailedImmediately
}
```

### 4.3 イベントデリゲートと引数
```csharp
public delegate void StartStreamingEventHandler(object sender, OperationCompletedEventArgs args);
public delegate void StopStreamingEventHandler(object sender, OperationCompletedEventArgs args);
...

public sealed class OperationCompletedEventArgs : EventArgs
{
    public Guid OperationId { get; }
    public bool IsSuccess { get; }
    public CameraState State { get; }                 // REQ-EV-002
    public CameraMetadata Metadata { get; }           // 成功時のみ有効、REQ-CF-001
    public ErrorCode? ErrorCode { get; }              // 失敗時、REQ-EV-003
    public DateTime TimestampUtc { get; }
}

public sealed class CameraMetadata
{
    public CameraResolution Resolution { get; }
    public PixelFormat PixelFormat { get; }
    public FrameRate FrameRate { get; }
}
```
- `IsSuccess == false` の際は `ErrorCode` が必ず設定される。
- `OperationTicketStatus.FailedImmediately` の操作ではイベントを原則発火せず、呼び出し側は返却されたチケットとログで失敗を把握する（必要に応じて監視向けに専用イベントを追加可能）。
- イベントは `Task.Run(() => handler(...))` でスレッドプールにディスパッチ (REQ-EV-004)。
- 各イベントディスパッチは `try/catch` でハンドラ例外を捕捉し、`Logger` に `Error` レベルで出力したうえで処理を継続する。再スローは行わず、複数ハンドラの場合でも全ハンドラ呼び出しを試みる (REQ-LG-001, REQ-ER-003)。

### 4.4 オペレーションタイムアウト・キャンセル
- `OwnerSessionOptions.DefaultCancellationToken` を保持。操作呼び出し時に明示的 `CancellationToken` が指定された場合はそのトークンを優先 (REQ-CT-001)。
- タイムアウト値は `OperationTimeouts` (Start: 5s, Stop: 5s, Pause/Resume: 3s, UpdateConfiguration: 4s, Reset: 10s) をデフォルトとし、`OwnerKeeperOptions` で調整可能 (REQ-CT-002, REQ-CF-004)。

## 5. リソース管理仕様
### 5.1 リソーステーブル
- `ResourceId` は `struct ResourceId { ushort Value; ResourceKind Kind; }` とし、`ResourceManager` 初期化時に `ResourceKind.Camera` のスタブを登録。
- テーブル項目 `ResourceDescriptor`:
  - `ResourceId Id`
  - `IHardwareResource Adapter`
  - `CameraState State`
  - `OwnerSession? CurrentOwner`
  - `SemaphoreSlim Lock`
- 所有権獲得は `SemaphoreSlim.Wait(0)` で即時判定し、失敗時は `OWN2001` を返却 (REQ-RC-001)。
- `CurrentOwner` には `SessionId` と `CancellationTokenSource` を保持し、`Dispose` または `GC` 時に解除 (REQ-OW-004)。
- `CameraAdapter` は `ArrayPool<byte>` を `FrameBufferPool` として保持し、フレーム取得時には `Rent`/`Return` を利用してバッファ再利用を行う。`OperationScheduler` からの完了通知時にバッファを返却し、メタデータにはバッファ寿命を明示しない運用とする (REQ-MM-001, REQ-MM-002)。

### 5.2 状態遷移実装
- `StateMachine` は ST-1 のテーブルを `Dictionary<CameraState, List<StateTransitionRule>>` で保持。
- 遷移可否判定に失敗した場合は、すべて「即時失敗チケット」で返却する。
  - 不正遷移は `OperationTicketStatus.FailedImmediately` として `ARG3001` を返却し、例外は投げない（Runtimeでユーザが防ぎようがないため）。
  - 所有権競合などのランタイム要因も同様に `FailedImmediately` で返却する (REQ-ER-002, REQ-RC-002)。
  - 非同期処理中に発生したランタイム要因（例: ハードウェア異常）はイベントで通知する (REQ-ER-003)。
- 状態変更時は `StatusChanged` イベントを発火し、監視メトリクスを更新 (REQ-MN-001, REQ-EV-001)。

### 5.3 スレッドセーフティ設計
- `ResourceManager` は所有権獲得/解放、リソース参照に `ReaderWriterLockSlim` を用いて読み取りの並列性と書き込み排他を両立させる (REQ-TH-001)。
- `ResourceDescriptor.Lock` は `SemaphoreSlim` により個別リソースの占有を即時判定し、取得に失敗した場合は `OperationTicketStatus.FailedImmediately` を返却する。
- `StateMachine` は `ConcurrentDictionary<ResourceId, CameraState>` を利用し、状態更新は `ResourceManager` の書き込みロック下で行うことで一貫性を保持する。
- イベント発火やログ出力はロック解放後に実施し、デッドロックを回避する。

## 6. エラー処理仕様
### 6.1 エラーコード定義
- `ErrorCode` は `record struct ErrorCode(string Prefix, int Code);` で実装。
- `ErrorCatalog` で表 ERR-1 をベースにコード→メッセージ→推奨アクションを管理。
- 競合 (`OWN2001`) 等のランタイム同期エラーは `OperationTicketStatus.FailedImmediately` と `ErrorCode` を設定したチケットを返却し、監視のために Logger および必要に応じてイベントへ転送する (REQ-ER-002, REQ-RC-002)。
- 未初期化 (`ARG3002`) 等の誤用に分類されるエラーは例外として送出する (REQ-ER-001)。

### 6.2 リカバリパス
- `Reset()` 操作は `Error` 状態のカメラを `Ready` へ戻すための内部再初期化を実施 (REQ-EC-004)。
- `Shutdown()` 時にはすべてのリソースを `Uninitialized` に戻し、全イベント購読者へ `StatusChanged` (State=Uninitialized) を送出。

## 7. 設定・構成仕様
- `OwnerKeeperOptions`
  - `int CameraCount`
  - `CameraConfiguration DefaultConfiguration`
  - `OperationTimeouts Timeouts`
  - `bool AutoRegisterMetrics`
- `CameraConfiguration`
  - `CameraResolution Resolution`
  - `PixelFormat PixelFormat`
  - `FrameRate FrameRate`
  - 変更要求は `ConfigurationValidator` によって検証し、無効な値は `ARG3001`
- Streaming中の設定変更は `CameraAdapter` に委譲し、成功/失敗は `UpdateConfiguration` イベントにより通知 (REQ-CF-003)。
- 画像データ転送が必要な場合は `FrameBufferHandle` を発行し、ハンドルが `ArrayPool<byte>` のバッファ寿命を管理する。イベント引数から直接バイト配列を参照せず、使用後は `FrameBufferHandle.Dispose()` が `Return` を呼ぶ設計とする (REQ-MM-001~003)。

## 8. ログとメトリクス
- `Logger` は `Console.WriteLine` をラップし、`LogLevel` に応じて出力 (REQ-LG-001, REQ-LG-002)。
  - `Info`: API要求受付 (`REQ-LG-001`)
  - `Warning`: 再試行可能なエラー
  - `Error`: イベント失敗、所有権競合
- `MetricsCollector`
  - Counter: `ownerkeeper_operations_total{type}`, `ownerkeeper_operation_failures_total{type, error}`
  - Histogram: `ownerkeeper_operation_latency_ms{type}`
  - テスト容易性のため、メモリ上に最新値を公開（OperationsTotal/OperationFailures/LastLatencyMs）。
- ヘルスチェックAPI `OwnerKeeperHost.GetHealthSnapshot()` を追加予定 (REQ-MN-002)。

## 9. セキュリティ
- セッション生成時に `SessionId` と `ResourceId` を紐付け、操作時に一致確認 (REQ-SE-001)。
- ログ／イベントに機密情報を出力しない。将来の暗号化ポイントとして `IHardwareResource` インターフェイスに `SecureChannel` プロパティを確保 (REQ-SE-002, REQ-SE-003)。

## 10. テスト・スタブ戦略
- `CameraStub` は `IHardwareResource` を実装し、動作モード（成功、失敗、遅延）を変更可能 (REQ-TS-001~003)。
- `IHardwareResourceFactory` を DI で注入し、実機⇔スタブ切替を行う。
- 単体テストでは `ResourceManager`, `StateMachine` をモック化し、相互作用を検証。

## 11. 依存関係と制約
- .NET 8 標準ライブラリのみ使用 (REQ-IM-001)。
- `System.Threading.Channels`, `System.Buffers.ArrayPool`, `System.Diagnostics.Metrics` など標準APIで要件を満たす。

### 11.1 バージョニング戦略
- アセンブリバージョンは Semantic Versioning に準拠し、`Major.Minor.Patch` を `OwnerKeeperHost.LibraryVersion` で公開する (REQ-VR-001)。
- 公開APIの互換性を破壊する変更はメジャーバージョンをインクリメントし、マイナーバージョンでは後方互換 API を `Obsolete` 属性付きで維持する (REQ-VR-002)。
- 旧バージョン向けの互換レイヤーとして `CompatibilityMode` オプションを提供し、サポート対象バージョンを `OwnerKeeperOptions.CompatibleVersions` で列挙できるようにする (REQ-VR-003)。

### 11.2 デバッグモード
- `OwnerKeeperOptions.EnableDebugMode` を追加し、`true` の場合は詳細ログ（イベントペイロード、状態遷移）と追加メトリクスを出力する (REQ-MN-003)。
- デバッグモードでは `OperationScheduler` の内部キュー長を周期的にダンプし、`MetricsCollector` へ `debug_` プレフィックス付きメトリクスを送信する。

### 11.3 ファイナライザ実装
- `OwnerKeeperHost` は `IDisposable` に加え保険としてファイナライザを実装し、`Dispose(false)` で残存セッションを `Dispose` して所有権を解放する (REQ-IN-005)。
- `OwnerSession` はセーフハンドル風の `ReleaseHandle()` を持ち、GC 時に所有権を解放できるよう `CriticalFinalizerObject` を利用する案を検討する。

## 12. トレーサビリティ概要
| 仕様セクション | 対応要件ID |
| --- | --- |
| 3. 全体アーキテクチャ | REQ-OV-001, REQ-OV-003, REQ-ST-001 |
| 4. API仕様 | REQ-SN-001~003, REQ-AI-001~003, REQ-EV-001~004, REQ-CT-001 |
| 5. リソース管理 | REQ-RM-001~002, REQ-OW-001~004, REQ-RC-001 |
| 6. エラー処理仕様 | REQ-ER-001~003, REQ-EC-001~004 |
| 7. 設定・構成仕様 | REQ-CF-001~004 |
| 8. ログとメトリクス | REQ-LG-001~002, REQ-MN-001~003, REQ-PF-002 |
| 9. セキュリティ | REQ-SE-001~003 |
| 10. テスト戦略 | REQ-TS-001~003 |
| 11. 依存制約 | REQ-IM-001 |
| 11.1 バージョニング戦略 | REQ-VR-001~003 |
| 11.2 デバッグモード | REQ-MN-003 |
| 11.3 ファイナライザ実装 | REQ-IN-005 |
| 12. トレーサビリティ | 参照マップ |

## 13. 今後の検討課題
- OperationScheduler の実装詳細（キューサイズ、バックオフ戦略）。
- メトリクスの外部エクスポート方式（Prometheus 互換等）。
- ハードウェア固有エラーの追加定義とカタログ管理ワークフロー。
- セッションの有効期限・自動失効ポリシー（REQ-OW-004 拡張案）。
