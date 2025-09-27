# OwnerKeeper 要件 - SysML分析

## 1. パッケージ図
```plantuml
@startuml
package "OwnerKeeperシステム" <<System>> {
    package "コアコンポーネント" {
        class リソース管理
        class 所有権ハンドラ
        class 状態管理
        class イベント通知
    }

    package "ハードウェア抽象化" {
        interface IHardwareResource
        class カメラリソース
        class リソーステーブル
    }

    package "APIレイヤー" {
        interface IApiInterface
        class ユーザーインターフェイス
        class 初期化管理
    }

    package "サポートサービス" {
        class ログ出力
        class メトリクス収集
        class 設定管理
        class エラーハンドラ
    }
}
@enduml
```

## 2. 要求図
```plantuml
@startuml
package "機能要求" {
    requirement "<<requirement>>\nREQ-OV-001" as OV001 {
        text = "同期APIを通じて非同期処理を開始し、\nリソース所有権を厳密に管理する"
        id = "REQ-OV-001"
        priority = "High"
    }

    requirement "<<requirement>>\nREQ-OW-001" as OW001 {
        text = "リソースは単独所有とし、\nAPIインターフェイス単位で所有権を割り当てる"
        id = "REQ-OW-001"
        priority = "High"
    }

    requirement "<<requirement>>\nREQ-TH-001" as TH001 {
        text = "すべての公開APIをスレッドセーフに実装する"
        id = "REQ-TH-001"
        priority = "High"
    }
}

package "非機能要求" {
    requirement "<<requirement>>\nREQ-PF-001" as PF001 {
        text = "ベストエフォートで低遅延・高スループットを目指す"
        id = "REQ-PF-001"
        priority = "Medium"
    }

    requirement "<<requirement>>\nREQ-SE-001" as SE001 {
        text = "識別子と所有権を紐付け、\n権限のないアクセスを防止する"
        id = "REQ-SE-001"
        priority = "High"
    }
}

OV001 ..> OW001 : <<derive>>
OW001 ..> TH001 : <<derive>>
OW001 ..> SE001 : <<derive>>
@enduml
```

## 3. 状態機械図 (REQ-ST-001~004)
```plantuml
@startuml
[*] --> 未初期化

未初期化 --> 初期化中 : Initialize()
初期化中 --> 準備完了 : 初期化成功
初期化中 --> エラー : 初期化失敗[SYS0001]

準備完了 --> ストリーミング : StartStreaming()
準備完了 --> 準備完了 : 設定変更
準備完了 --> 未初期化 : Shutdown/所有権解放

ストリーミング --> 一時停止 : Pause()
ストリーミング --> 停止 : Stop()
ストリーミング --> ストリーミング : 設定変更(ドライバ許容時)
ストリーミング --> エラー : HWエラー[HW1001-1002]

一時停止 --> ストリーミング : Resume()
一時停止 --> 停止 : Stop()

停止 --> 準備完了 : Prepare()/再初期化

エラー --> 準備完了 : Reset()成功
エラー --> 未初期化 : Shutdown

state ストリーミング {
    [*] --> アクティブ
    アクティブ : 画像データ取得中
    アクティブ : イベント通知中
}

state エラー {
    [*] --> リカバリ可能
    リカバリ可能 : リカバリ可能
    リカバリ可能 --> 致命的 : リカバリ失敗
    致命的 : 致命的エラー
}
@enduml
```

## 4. ブロック定義図
```plantuml
@startuml
block "OwnerKeeperライブラリ" <<block>> {
    part resourceManager : リソース管理
    part eventNotifier : イベント通知
    part logger : ログ出力

    port syncApi : ISyncApi
    port eventOut : IEventNotification
}

block "クライアントアプリ" <<external>> {
    port apiCall : IApiCall
    port eventReceiver : IEventReceiver
}

block "ハードウェアリソース" <<block>> {
    part camera : カメラデバイス
    port control : IHardwareControl
    port dataStream : IDataStream
}

"OwnerKeeperライブラリ".syncApi -- "クライアントアプリ".apiCall
"OwnerKeeperライブラリ".eventOut --> "クライアントアプリ".eventReceiver : <<flow>> イベント
"OwnerKeeperライブラリ" ..> "ハードウェアリソース".control : <<delegate>>
"ハードウェアリソース".dataStream --> "OwnerKeeperライブラリ" : <<flow>> データ
@enduml
```

## 5. ユースケース図
```plantuml
@startuml
left to right direction

actor "クライアントアプリ" as Client
actor "管理者" as Admin

rectangle "OwnerKeeperシステム" {
    usecase "ライブラリ初期化\n(REQ-IN-001)" as UC_Init
    usecase "インターフェイス要求\n(REQ-AI-001)" as UC_Request
    usecase "ストリーミング開始\n(REQ-SN-001)" as UC_Stream
    usecase "イベント受信\n(REQ-EV-001)" as UC_Event
    usecase "エラー処理\n(REQ-ER-002)" as UC_Error
    usecase "リソース解放\n(REQ-OW-004)" as UC_Release
    usecase "ヘルスチェック\n(REQ-MN-002)" as UC_Health
    usecase "設定変更\n(REQ-CF-001)" as UC_Config
}

Client --> UC_Init
Client --> UC_Request
Client --> UC_Stream
Client --> UC_Event
Client --> UC_Release
Client --> UC_Config

Admin --> UC_Health
Admin --> UC_Init

UC_Stream ..> UC_Event : <<include>>
UC_Stream ..> UC_Error : <<extend>>
UC_Request ..> UC_Init : <<precondition>>
@enduml
```

## 6. アクティビティ図 - カメラストリーミングフロー
```plantuml
@startuml
start
:ライブラリ初期化 (REQ-IN-001);

if (初期化済み?) then (はい)
    :既存インスタンスを返却;
else (いいえ)
    :リソーステーブル作成;
    :ログ設定;
    :メトリクス初期化;
endif

:APIインターフェイス要求 (REQ-AI-001);

if (ライブラリ初期化済み?) then (いいえ)
    #pink:例外を投げる (REQ-IN-003);
    stop
else (はい)
    :必要に応じてUUID生成;
    :インターフェイス作成;
endif

partition "所有権取得" {
    :リソース利用可能性確認;

    if (リソース所有済み?) then (はい)
        #yellow:OWN2001イベント送信 (REQ-RC-001);
        stop
    else (いいえ)
        :所有権取得 (REQ-OW-001);
    endif
}

partition "ストリーミング処理" {
    :ストリーミング開始 (REQ-SN-001);
    :状態をストリーミングに変更;

    fork
        :フレーム処理;
    fork again
        :ハードウェア監視;
    fork again
        :メトリクス収集 (REQ-MN-001);
    end fork

    :成功イベント送信 (REQ-EV-002);
}

partition "エラー処理" {
    if (ハードウェアエラー?) then (はい)
        :状態をエラーに変更;
        :HWエラーイベント送信 (REQ-ER-002);
        :ロック解放 (REQ-OW-003);
    else (いいえ)
        :ストリーミング継続;
    endif
}

:ストリーミング停止;
:所有権解放 (REQ-OW-004);
:インターフェイス破棄;

stop
@enduml
```

## 7. シーケンス図 - イベント通知フロー
```plantuml
@startuml
participant "クライアント" as C
participant "APIインターフェイス" as API
participant "リソース管理" as RM
participant "状態管理" as SM
participant "イベント通知" as EN
participant "スレッドプール" as TP
participant "カメラデバイス" as CAM

C -> API : StartStreaming()
activate API
API -> RM : AcquireOwnership(resourceId)
activate RM

alt 所有権取得可能
    RM --> API : OwnershipHandle
    deactivate RM
    API -> SM : TransitionTo(Streaming)
    activate SM
    SM -> SM : 遷移検証
    SM --> API : StateChanged
    deactivate SM

    API -> CAM : BeginCapture()
    activate CAM
    API --> C : Return (同期)
    deactivate API

    CAM -> EN : FrameCaptured(data)
    activate EN
    EN -> TP : QueueWork(handler, args)
    activate TP
    TP -> C : OnStreamingStarted(EventArgs)
    deactivate TP
    deactivate EN

else 所有権競合 (REQ-RC-001)
    RM --> API : null (OWN2001)
    deactivate RM
    API -> EN : NotifyError(OWN2001)
    activate EN
    EN -> TP : QueueWork(errorHandler)
    activate TP
    TP -> C : OnError(OWN2001)
    deactivate TP
    deactivate EN
    API --> C : Return
    deactivate API
end
@enduml
```

## 8. パラメトリック図 - 性能制約
```plantuml
@startuml
object "性能要求" as Perf {
    制約: REQ-PF-001
    目標: "ベストエフォート"
}

object "メトリクス収集" as Metrics {
    API呼び出し数: Integer
    平均遅延: Duration
    エラー率: Percentage
    スループット: fps
}

object "メモリ管理" as Memory {
    バッファプール: ArrayPool<byte>
    最大バッファサイズ: 4MB
    再利用ポリシー: REQ-MM-001
}

object "スレッド安全性" as Thread {
    ロック戦略: ReaderWriterLock
    イベント実行: ThreadPool
    要求: REQ-TH-001
}

Perf --> Metrics : 監視
Metrics --> Memory : 影響
Memory --> Thread : 制約
Thread --> Perf : 影響
@enduml
```

## 9. 要求トレーサビリティマトリクス

| 要求ID | コンポーネント | 実装 | テスト戦略 |
|---------------|-----------|----------------|---------------|
| REQ-OV-001 | APIレイヤー | ISyncApiインターフェイス | 統合テスト |
| REQ-OV-002 | イベント通知 | デリゲートイベント | 単体テスト |
| REQ-OW-001 | リソース管理 | 所有権ハンドラ | 単体テスト |
| REQ-TH-001 | 全コンポーネント | スレッドセーフコレクション | 並行実行テスト |
| REQ-ST-001 | 状態管理 | State列挙型とFSM | 状態遷移テスト |
| REQ-ER-001 | APIレイヤー | 例外スロー | 単体テスト |
| REQ-ER-002 | イベント通知 | エラーイベント配信 | 統合テスト |
| REQ-MM-001 | リソース管理 | ArrayPool使用 | メモリリークテスト |
| REQ-SE-001 | APIレイヤー | 識別子バインディング | セキュリティテスト |

## 10. 内部ブロック図 - 所有権管理
```plantuml
@startuml
object "APIインターフェイス" as API {
    ユーザID: String
    インターフェイスID: UUID
    キャンセルトークン: Optional<CancellationToken>
}

object "所有権ハンドル" as OH {
    リソースID: Integer
    所有者ID: UUID
    取得日時: DateTime
    有効フラグ: Boolean
}

object "リソーステーブル" as RT {
    リソース群: Map<Integer, Resource>
    所有権マップ: Map<Integer, UUID>
}

object "カメラリソース" as CR {
    カメラID: Integer
    状態: CameraState
    設定: CameraSettings
}

API "1" --> "0..1" OH : 所有
OH "1" --> "1" CR : 制御
RT "1" --> "*" CR : 管理
RT "1" --> "*" OH : 追跡
@enduml
```

## 分析サマリー

### 要件から導出される主要アーキテクチャ決定事項:

1. **所有権モデル (REQ-OW-001~004)**
   - リソース毎に単一所有権
   - インターフェイスベースの所有権管理
   - Dispose/Finalizerによる自動クリーンアップ

2. **並行実行戦略 (REQ-TH-001)**
   - 全APIスレッドセーフ
   - ThreadPool上でイベントハンドラ実行
   - 可能な限りロックフリー

3. **エラーハンドリング (REQ-ER-001~003)**
   - 開発時エラーは例外
   - ランタイムエラーはイベント
   - 非同期処理では例外なし

4. **状態管理 (REQ-ST-001~004)**
   - 明示的状態機械
   - 検証された遷移
   - 状態依存の操作許可

5. **メモリ戦略 (REQ-MM-001~003)**
   - 大容量バッファはArrayPool
   - IDisposableパターン徹底
   - 所有権に紐づくリソースライフサイクル

### 重要な要件依存関係:
- REQ-IN-003 (初期化チェック) が REQ-AI-001 (インターフェイス作成) をブロック
- REQ-OW-001 (単一所有権) が REQ-RC-001 (競合処理) を強制
- REQ-TH-001 (スレッド安全性) が REQ-EV-004 (ThreadPool実行) を可能に
- REQ-ST-002 (状態遷移) が REQ-ER-001/002 (エラー戦略) を決定