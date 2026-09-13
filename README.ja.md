<div align="center">

# mistlib-unity

**Unityの共有3D空間に向けたP2Pネットワークライブラリ。**

[English](README.md) · 日本語 · [简体中文](README.zh.md)

</div>

[MistLib](https://github.com/tik-choco-lab/mistlib)をUnity Package Managerから使うためのパッケージです。
C# APIとMistLib 0.6.2のネイティブライブラリを同梱しています。ピアはNostrリレーで互いを発見し、
WebRTCデータチャネルで通信します。エンジンは3D空間での距離に基づいて関心領域（AOI）と接続先を管理します。

## 開発状況

このパッケージは**実験段階**です。0.6.2のネイティブABIにはシャットダウンと
コールバック登録解除の関数がありません。初期化するクライアントは1プロセスにつき1つにしてください。
同じEditorでのPlay Modeの繰り返しやマネージドドメインの再読み込みは未サポートです。
実行の間にEditorを再起動してください。`Dispose()`は参加中のルームから退出し、マネージド側への
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
`#v0.6.2`のようなバージョン指定は、そのタグが作成されている場合にだけ利用できます。
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

同梱バイナリはMistLibの`v0.6.2` GitHub Releaseから取得しています。初期化時にネイティブバージョンと
パッケージのエンジンバージョンを照合します。上流バイナリとSHA-256は
[MistLib v0.6.2リリース](https://github.com/tik-choco-lab/mistlib/releases/tag/v0.6.2)から確認できます。

Unityのマネージドバインディング、サンプル、ドキュメントは**Apache-2.0**、MistLibのネイティブエンジンは
**MPL-2.0**です。依存ライブラリにはそれぞれのライセンスが適用されます。
[LICENSE.md](LICENSE.md)、[LICENSES](Packages/com.tik-choco.mistlib/LICENSES)、[Third Party Notices](Packages/com.tik-choco.mistlib/Third%20Party%20Notices.md)を参照してください。
