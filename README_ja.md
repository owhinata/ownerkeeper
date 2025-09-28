# OwnerKeeper (日本語)

OwnerKeeper は .NET 8 向けのライブラリで、カメラ等のハードウェアに類する
リソースの「所有権」と「ライフサイクル」を厳密に管理します。同期 API は
即時に `OperationTicket` を返し、非同期実行や完了はイベントで通知します。
競合や不正遷移は例外ではなく「即時失敗チケット」で返す方針です。

- 単独所有（Single Ownership）。競合は即時失敗 `OWN2001`
- 状態機械に基づく操作。不正遷移は即時失敗 `ARG3001`
- チャネルベースの `OperationScheduler` + イベントディスパッチ
- `OwnerSession` による型付きイベント（Start/Stop/Pause/Resume/Update/Reset）
- ハードウェア抽象化（`IHardwareResource`）とデフォルトスタブ
- 任意のメトリクス（件数/失敗/レイテンシ）と簡易ロギング

詳細は `RELEASE_NOTES.md`、`docs/`、`AGENTS.md` を参照してください。

## 要件
- .NET 8 SDK

## クイックスタート
```csharp
using System;
using OwnerKeeper;
using OwnerKeeper.API;
using OwnerKeeper.Domain;

// 1) 初期化（多重呼び出しは冪等）
OwnerKeeperHost.Instance.Initialize(new OwnerKeeperOptions
{
    CameraCount = 1,
    AutoRegisterMetrics = true,
    DebugMode = false,
});

// 2) セッション作成と型付きイベント購読
var session = OwnerKeeperHost.Instance.CreateSession("user-1");
session.StartStreamingCompleted += (s, e) =>
{
    Console.WriteLine($"Start success={e.IsSuccess} state={e.State} error={e.ErrorCode}");
};

// 3) 同期APIで要求→チケット受領（非同期処理は内部スケジューラ）
var ticket = session.StartStreaming();
Console.WriteLine($"Ticket={ticket.OperationId} status={ticket.Status}");

// ... 後で停止
session.StopStreaming();
OwnerKeeperHost.Instance.Shutdown();
```

## 失敗セマンティクス（Immediate Failure）
同期 API は以下いずれかの `OperationTicket` を返します。
- `Accepted`: 非同期実行を受理。完了イベントが発火
- `FailedImmediately`: 非同期実行は行わない。失敗理由は `ErrorCode`

代表的な即時失敗コード:
- 所有権競合: `OWN2001`
- 不正遷移: `ARG3001`
- 事前キャンセル: `CT0001`
- 未初期化の誤用: `OwnerKeeperNotInitializedException (ARG3002)` を投げる

## メトリクス / ログ
- メトリクス（任意）
  - Counter: `ownerkeeper_operations_total{type}`
  - Counter: `ownerkeeper_operation_failures_total{type,error}`
  - Histogram: `ownerkeeper_operation_latency_ms{type}`
- ログ: `Info`/`Warning`/`Error` を `ConsoleLogger` で出力

有効化は `OwnerKeeperOptions` の `AutoRegisterMetrics`/`DebugMode` を使用。

## ハードウェア抽象化
- 実機統合は `OwnerKeeper.Hardware.IHardwareResource` を実装してください。
- テスト用途には `CameraStub` を同梱。`OwnerKeeperOptions.HardwareFactory`
  にファクトリを指定すれば差し替え可能です。

## ビルド / テスト
```bash
# リポジトリルートから
cd csharp
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build OwnerKeeper.sln -v minimal
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet test OwnerKeeper.sln -v minimal
```

## 開発者向けメモ
- アナライザ有効・警告はエラー扱い（`Directory.Build.props`）
- コメント／XML ドキュメントは英語（`AGENTS.md`）
- 事前整形は CSharpier のみ。pre-commit フックで `csharp/` 配下変更時に実行
  - インストール: `bash scripts/install-git-hooks.sh`

## バージョン
- ライブラリバージョンは `OwnerKeeperHost.LibraryVersion` から参照可能

## リリースノート
- `RELEASE_NOTES.md` を参照

## ライセンス
- `LICENSE` を参照

