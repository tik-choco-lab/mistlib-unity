<div align="center">

# mistlib-unity

**Unityの共有3D空間に向けたP2Pネットワークライブラリ。**

[English](README.md) · 日本語 · [简体中文](README.zh.md)

</div>

[MistLib](https://github.com/tik-choco-lab/mistlib)をUnity Package Managerから使うためのパッケージです。
C# APIとMistLibエンジン0.7.1を同梱し、パッケージとエンジンのバージョンはどちらも0.7.1です。ピアはNostrリレーで互いを発見し、
WebRTCデータチャネルで通信します。エンジンは3D空間での距離に基づいて関心領域（AOI）と接続先を管理します。

## 開発状況

このパッケージは**実験段階**です。0.7.1のネイティブABIにはシャットダウンと
コールバック登録解除の関数がありません。初期化するクライアントは1プロセスにつき1つにしてください。
同じEditorでのPlay Modeの繰り返しやマネージドドメインの再読み込みは未サポートです。
実行の間にEditorを再起動してください。`Dispose()`はすべてのルームから退出し、マネージド側への
イベント配信を止めますが、ネイティブランタイムはプロセス終了まで残ります。

## 必要な環境

- Unity 6000.0以降。
- Windows x86_64、Linux x86_64、またはAppleシリコンのmacOS。
- Windows：`VCRUNTIME140.dll`を提供するMicrosoft Visual C++ランタイム。
- Linux：glibc 2.34以降と互換性のあるOpenSSL 3（`libssl.so.3`、`libcrypto.so.3`）。
- macOS：ネイティブライブラリの最低要件はmacOS 11。利用するUnityのOS要件も満たしてください。
- ピア発見用のHTTPS／WebSocket接続と、ICE／STUNまたはTURNを通したWebRTC接続。

Android、iOS、WebGL、Windows ARM64、Intel Mac、Universal macOSプレイヤーは未サポートです。

## インストール

UPMパッケージは `Packages/com.tik-choco.mistlib/` に配置しています。リポジトリのREADMEと
開発用スクリプトはパッケージ外にあります。旧ルート構成から移行する場合は、Git依存のURLに
以下の `?path=` パラメーターを追加してください。

ローカルのチェックアウトを使う場合は、Unity Package Managerの**Install package from disk**で
`Packages/com.tik-choco.mistlib/package.json`を選択してください。

`main`ブランチからインストールするには、Package Managerから次のGit URLを追加するか、
`Packages/manifest.json`に記述します。

```json
{
  "dependencies": {
    "com.tik-choco.mistlib": "https://github.com/tik-choco-lab/mistlib-unity.git?path=Packages/com.tik-choco.mistlib#main"
  }
}
```

`#`の後ろはGitのブランチ・タグ・コミットを指定します。開発ブランチを使う場合は`#develop`に変更します。
`#v0.7.1`のようなバージョン指定は、そのタグが作成されている場合にだけ利用できます。
ブランチ指定ではパッケージ更新時にそのブランチの変更が反映されるため、固定した版が必要な場合はコミットハッシュを指定してください。

## 最小の使い方

次のコンポーネントを空のGameObjectに追加します。空のオプションにはネイティブエンジンの既定値が適用され、
`NodeId`が空の場合は一意なIDが生成されます。

```csharp
using TikChoco.MistLib;
using UnityEngine;

public sealed class RoomExample : MonoBehaviour
{
    private MistClient client;

    private void Start()
    {
        client = new MistClient();
        client.EventReceived += item =>
            Debug.Log($"MistLib: {item.Type} from {item.FromNodeId}");
        client.Initialize(new MistClientOptions { InitialRoom = "lobby" });
    }

    private void Update()
    {
        if (client != null && client.IsInitialized)
            client.PumpCallbacks();
    }

    private void OnDestroy() => client?.Dispose();
}
```

ネイティブコールバックのデータはマネージドキューにコピーされ、`PumpCallbacks()`により`Initialize`を呼んだ
スレッド（通常はUnityのメインスレッド）へ配信されます。`MistRuntimeHost`を使うと、クライアントの所有と
`Update`からのコールバック処理を任せられます。

`client.LeaveRoom("lobby")`は1つのルームから、`client.LeaveAllRooms()`はすべてのルームから退出します。
`Dispose()`もすべてのルームから退出します。0.7.1のC ABIでは個別退出に`leave_room_id`を使います。
非推奨のネイティブ関数`leave_room()`は引数を取らず、すべてのルームから退出します。

`SendMessage`には次の`MistDeliveryMethod`を指定できます。

| 値 | コード | 配信 |
| --- | --- | --- |
| `ReliableOrdered` | 0 | 信頼性あり、順序を維持 |
| `UnreliableOrdered` | 1 | 欠落の可能性あり、順序を維持 |
| `Unreliable` | 2 | 欠落・順序変更の可能性あり |
| `ReliableUnordered` | 3 | 信頼性あり、順序保証なし。非対応のピアには信頼性・順序保証ありで配信 |

`MistClient.GetNativeVersion()`と`MistClient.GetNativeBuildInfoJson()`はクライアントを初期化せずに
ロード済みライブラリを照会できます。初期化後は`client.NativeVersion`と`client.NativeBuildInfoJson`から
バージョンとビルド情報を取得できます。初期化には0.7.1が必要です。

## エンジン0.7.1のイベント

`MistEventType`はnative/wasm共通のコード0〜12を使います。パッケージ0.7.1では、ピア接続とルームの
ライフサイクルを区別するため、`Join`を`PeerConnected`（2）、`Leave`を`PeerDisconnected`（3）へ
改名し、互換aliasは残していません。`AreaOfInterest*`の名前と値は維持しています。
`RoomJoined`（10）、`RoomJoinFailed`（11）、`RoomLeft`（12）を追加しました。
全コードとペイロードの表は[英語版](README.md#events-in-engine-071)を参照してください。

各イベントの`RoomId`でルームを識別します。ルームイベントの`FromNodeId`は空です。
`Initialize`直後ではなく`RoomJoined`で参加完了を確認してください。参加失敗・起動キャンセルは
`RoomJoinFailed`のUTF-8理由文字列、退出処理完了は`RoomLeft`で通知します。
ピア接続・切断とAOI入退域のペイロードは固定文字列`joined`、`left`、`entered`、`left`です。
`NodePositionUpdated`（7）はAOIのtickごとに位置JSONを通知します。
`AllConnectionsLost`（9）は接続済みセッションが空になったとき1回通知し、再接続後に再度有効になります。
初期空室・明示的退出では発火しません。100/101は別のwasmメディアcallback専用で、このラッパーでは公開しません。

## サンプル

Package Managerのウィンドウからインポートしてください。

| サンプル | 内容 |
| --- | --- |
| [Capsule Sync](Packages/com.tik-choco.mistlib/Samples~/CapsuleSync/README.md) | WASDまたは矢印キーで自分のカプセルを動かし、連番付き位置ブロードキャストで相手のカプセルが追従する様子を確認できます。最初はこちらがおすすめです。 |
| [Basic Room](Packages/com.tik-choco.mistlib/Samples~/BasicRoom/README.md) | 初期化、ルーム参加、コールバック処理、イベントのログ出力を示します。 |

2ノードを動かすには、Editorとスタンドアロンビルド、または2つのスタンドアロンプレイヤーなど、
2つのプロセスが必要です。両方でルームとピア発見の設定を揃えてください。
起動手順やMultiplayer Play ModeについてはCapsule SyncのREADMEを参照してください。
サンプルは動作例であり、アプリケーションの認証やピアから受け取ったゲーム操作の検証は実装していません。

## 設定とプライバシー

変更する値だけ指定します。例えば次の設定でAOIの範囲を変更できます。

```csharp
client.Initialize(new MistClientOptions
{
    InitialRoom = "lobby",
    ConfigJson = "{\"aoiRange\":20.0}"
});
```

省略したキーにはネイティブ側の既定値が適用されます。`GetConfigJson()`は設定済みの認証情報も含む
解決済み設定を返します。未知のキーは拒否されます。`SignalingUrl`は従来のWebSocketトランスポートを選択し、
空でない`ConfigJson`との併用はできません。

0.7.1では、信頼性あり・順序保証なしのbulk転送と適応的ペーシングを含むCP50ストレージ転送設定が
既定で有効です。変更する場合はflat形式の設定JSONで`storageTransfer`、完全な設定JSONで
`storage.transfer`を指定します。省略したキーにはエンジンの既定値が適用されます。

独自のピア発見範囲を使う場合は`signaling.nostr.relays`、`inviteSalt`、`inviteCode`を設定します。
ピア同士で招待設定を一致させてください。公開リレーを明示的に指定しながら組み込みのプレースホルダーの
招待設定を使う構成は、エンジンに拒否されます。既定では外部のリレー一覧とSTUNサービスへ接続します。
サンプルの試用に利用できますが、実際の運用では接続先と設定を確認してください。
招待設定はピア発見の範囲を決めるもので、アプリケーションの認証を代替しません。
WebRTCのピアやネットワークサービスにはIPアドレスや接続メタデータが伝わる場合があります。

実際の招待コード、TURN認証情報、秘密鍵をGitへ登録しないでください。解決済み設定のログ出力も避けてください。
`MistRuntimeHost`の**Copy Effective Config JSON**は設定をシリアライズ対象のInspectorフィールドへコピーします。
シーンやPrefabを保存・共有する前に認証情報を除いてください。リポジトリに登録しなくても、配布するUnity
プレイヤーに埋め込んだ認証情報は取り出せます。

## ネイティブエンジンとライセンス

同梱のLinux x86_64、Windows x86_64、macOS ARM64ライブラリは公式の
[MistLib v0.7.1リリース](https://github.com/tik-choco-lab/mistlib/releases/tag/v0.7.1)の成果物です。
初期化にはパッケージと同じエンジン0.7.1との完全一致が必要です。
`scripts/fetch-engine.sh 0.7.1`で取得できます。`bash scripts/validate-package.sh`はパッケージ構造、
バージョン宣言の一致、ネイティブのエクスポート、3つのSHA-256を検証します。

Unityのマネージドバインディング、サンプル、ドキュメントは**Apache-2.0**、MistLibのネイティブエンジンは
**MPL-2.0**です。依存ライブラリにはそれぞれのライセンスが適用されます。
[LICENSE.md](LICENSE.md)、[LICENSES](Packages/com.tik-choco.mistlib/LICENSES)、[Third Party Notices](Packages/com.tik-choco.mistlib/Third%20Party%20Notices.md)を参照してください。
