<div align="center">

# mistlib-unity

**面向 Unity 共享 3D 空间的点对点网络库。**

[English](README.md) · [日本語](README.ja.md) · 简体中文

</div>

这是通过 Unity Package Manager 使用 [MistLib](https://github.com/tik-choco-lab/mistlib) 的绑定包，
提供 C# API 并捆绑 MistLib 引擎 0.7.1；包版本和引擎版本均为 0.7.1。节点通过 Nostr 中继发现彼此，通过 WebRTC 数据通道通信。
引擎根据 3D 空间中的距离管理兴趣区域（AOI）和节点连接。

## 开发状态

此包处于**实验阶段**。0.7.1 原生 ABI 没有关闭运行时或注销回调的函数。
每个进程只能使用一个已初始化的客户端；不支持在同一个 Editor 进程中反复进入 Play Mode，也不支持托管域重载。
请在两次运行之间重启 Editor。`Dispose()` 会退出所有房间并停止向托管端分发事件，
但原生运行时会一直保留到进程退出。

## 环境要求

- Unity 6000.0 或更新版本。
- Windows x86_64、Linux x86_64 或 Apple 芯片的 macOS。
- Windows：提供 `VCRUNTIME140.dll` 的 Microsoft Visual C++ 运行库。
- Linux：glibc 2.34 或更新版本，以及兼容的 OpenSSL 3（`libssl.so.3`、`libcrypto.so.3`）。
- macOS：原生库的最低要求为 macOS 11；还需满足所用 Unity 版本的操作系统要求。
- 用于节点发现的出站 HTTPS／WebSocket 连接，以及通过 ICE／STUN 或 TURN 建立的 WebRTC 连接。

不支持 Android、iOS、WebGL、Windows ARM64、Intel Mac 和 Universal macOS Player。

## 安装

UPM 包位于 `Packages/com.tik-choco.mistlib/`。仓库 README 和开发脚本位于包目录之外。
从原来的仓库根目录布局迁移时，请在 Git 依赖 URL 中添加下面的 `?path=` 参数。

使用本地检出目录时，在 Unity Package Manager 中选择 **Install package from disk**，然后选择 `Packages/com.tik-choco.mistlib/package.json`。

如需从 `main` 分支安装，请通过 Package Manager 添加以下 Git URL，
或将其写入 `Packages/manifest.json`：

```json
{
  "dependencies": {
    "com.tik-choco.mistlib": "https://github.com/tik-choco-lab/mistlib-unity.git?path=Packages/com.tik-choco.mistlib#main"
  }
}
```

`#` 后面用于指定 Git 分支、标签或提交。如需使用开发分支，请改为 `#develop`。
`#v0.7.1` 这样的版本引用仅在对应标签已创建时可用。
使用分支引用时，更新包会获取该分支的变更；如需固定版本，请指定提交哈希。

## 最小用法

将以下组件添加到一个空 GameObject。空选项使用原生引擎的默认值；`NodeId` 为空时会生成唯一 ID。

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

原生回调的数据会复制到托管队列中。`PumpCallbacks()` 在调用 `Initialize` 的线程上分发回调，
通常是 Unity 主线程。也可以使用 `MistRuntimeHost` 管理客户端，并从 `Update` 自动处理回调。

`client.LeaveRoom("lobby")` 退出一个房间；`client.LeaveAllRooms()` 退出所有房间。
`Dispose()` 也会退出所有房间。0.7.1 的 C ABI 仍使用 `leave_room_id` 退出单个房间；
已弃用的原生 `leave_room()` 不接受参数，会退出所有房间。

`SendMessage` 接受以下 `MistDeliveryMethod` 值：

| 值 | 代码 | 传递方式 |
| --- | --- | --- |
| `ReliableOrdered` | 0 | 可靠，保持顺序 |
| `UnreliableOrdered` | 1 | 可能丢失消息，保持顺序 |
| `Unreliable` | 2 | 可能丢失消息或乱序 |
| `ReliableUnordered` | 3 | 可靠，无顺序保证；对不支持的节点回退到可靠有序传递 |

`MistClient.GetNativeVersion()` 和 `MistClient.GetNativeBuildInfoJson()` 无需初始化客户端即可查询
已加载的库。初始化后可通过 `client.NativeVersion` 和 `client.NativeBuildInfoJson` 获取版本和
具体构建信息。初始化要求引擎版本为 0.7.1。

## 引擎 0.7.1 的事件

`MistEventType` 使用 native/wasm 统一的应用事件代码 0–12。包版本 0.7.1 将 `Join` 重命名为
`PeerConnected`（2），将 `Leave` 重命名为 `PeerDisconnected`（3），以区分节点连接与房间生命周期，
不保留兼容别名。`AreaOfInterest*` 的名称和值保持不变。
新增 `RoomJoined`（10）、`RoomJoinFailed`（11）和 `RoomLeft`（12）。
完整代码及负载表见[英文版](README.md#events-in-engine-071)。

每个事件都通过 `RoomId` 标识房间。房间事件的 `FromNodeId` 为空。
`Initialize` 启动加入流程，应通过 `RoomJoined` 确认就绪；失败或启动取消通过 `RoomJoinFailed`
携带 UTF-8 原因，退出清理完成后通过 `RoomLeft` 通知。
节点连接、断开与 AOI 进入、离开的负载分别为固定字符串 `joined`、`left`、`entered`、`left`。
`NodePositionUpdated`（7）在每次 AOI tick 发送位置 JSON。
`AllConnectionsLost`（9）在已有连接的会话变为空时通知一次，重新连接后可再次触发；
初始空房间或显式退出不会触发。100/101 属于独立的 wasm 媒体回调，本原生包装器不公开这些事件。

## 示例

在 Package Manager 窗口中导入示例。

| 示例 | 演示内容 |
| --- | --- |
| [Capsule Sync](Packages/com.tik-choco.mistlib/Samples~/CapsuleSync/README.md) | 用 WASD 或方向键移动本地胶囊体，通过带序号的位置广播让其他节点的胶囊体跟随。建议从这里开始。 |
| [Basic Room](Packages/com.tik-choco.mistlib/Samples~/BasicRoom/README.md) | 初始化、加入房间、处理回调和记录事件。 |

两个节点需要两个进程，例如 Editor 加一个独立构建，或运行两个独立 Player。
两端必须使用相同的房间和节点发现配置。启动步骤及 Multiplayer Play Mode 说明请参阅 Capsule Sync README。
这些示例用于演示，不实现应用层身份验证，也不验证其他节点发来的游戏操作。

## 配置与隐私

只指定需要覆盖的值。例如：

```csharp
client.Initialize(new MistClientOptions
{
    InitialRoom = "lobby",
    ConfigJson = "{\"aoiRange\":20.0}"
});
```

省略的键使用原生默认值。`GetConfigJson()` 返回解析后的完整配置，其中也包含已配置的凭据。
未知的键会被拒绝。`SignalingUrl` 用于选择旧版 WebSocket 传输，不能与非空 `ConfigJson` 同时使用。

0.7.1 默认启用 CP50 存储传输设置，包括可靠无序的批量传输和自适应节奏控制。
可通过扁平配置 JSON 的 `storageTransfer` 或完整配置 JSON 的 `storage.transfer` 覆盖设置；
省略的键保留引擎默认值。

如需自己的节点发现范围，请配置 `signaling.nostr.relays`、`inviteSalt` 和 `inviteCode`。
节点之间的邀请配置必须一致。如果显式指定公共中继，却仍使用引擎内置的占位邀请参数，引擎会拒绝该配置。
默认配置会连接外部中继列表和 STUN 服务，可用于试运行示例；实际部署时请审查连接目标和配置。
邀请参数用于限定节点发现范围，不能代替应用层身份验证。WebRTC 节点和网络服务可能获知 IP 地址及连接元数据。

请勿将真实邀请码、TURN 凭据或私钥提交到版本库，也不要记录完整配置。
`MistRuntimeHost` 的 **Copy Effective Config JSON** 会将配置复制到可序列化的 Inspector 字段。
保存或分享场景和 Prefab 前，请移除凭据。即使没有提交到此仓库，嵌入已分发 Unity Player 的凭据仍可被提取。

## 原生引擎与许可证

捆绑的 Linux x86_64、Windows x86_64 和 macOS ARM64 库来自官方
[MistLib v0.7.1 发行版](https://github.com/tik-choco-lab/mistlib/releases/tag/v0.7.1)。
初始化要求引擎版本精确为 0.7.1，与包版本一致。
可使用 `scripts/fetch-engine.sh 0.7.1` 获取产物。`bash scripts/validate-package.sh` 检查包结构、
版本声明是否一致、原生导出符号以及三个 SHA-256 摘要。

Unity 托管绑定、示例和文档采用 **Apache-2.0**，MistLib 原生引擎采用 **MPL-2.0**。
其依赖库分别适用各自的许可证。请参阅 [LICENSE.md](LICENSE.md)、[LICENSES](Packages/com.tik-choco.mistlib/LICENSES) 和
[Third Party Notices](Packages/com.tik-choco.mistlib/Third%20Party%20Notices.md)。
