# 快速开始

## 安装

使用 Unity 2020.2 或更高版本，在 **Window > Package Manager** 中选择 **+ > Add package from git URL**，输入：

```text
https://github.com/Novorizon/GpuAnimator.git#main
```

项目应使用 Built-in Render Pipeline。包内运行时和 Editor 代码已有独立程序集；`main` 不需要 PureMVCFramework。若调用代码自身使用 asmdef，请为它添加对 `GpuAnimator` 程序集的引用。

已有 ECS 项目可以改用 `#ecs`，但其 API 和依赖与本文不同，见[分支说明](index.md#分支与版本)。

## 准备原始资源

当前菜单工具会扫描 `Assets/Artworks/GpuRole/` 下的每个角色目录。以角色 `Hero` 为例，目录必须为：

```text
Assets/Artworks/GpuRole/
└── Hero/
    ├── Hero.fbx
    └── res/
        ├── Hero.png
        ├── Hero.mat
        ├── Hero_mask.png       # 可选
        ├── Idle.anim
        └── Walk.anim
```

这里的 `Idle`、`Walk` 仅为文件示例，播放器没有内置这些名称。运行时使用的是 `AnimationClip.name`，请按项目实际 Clip 名称播放。

当前工具的输入约定：

- 角色目录名、FBX 名和主贴图/材质名保持一致，建议大小写完全一致；目录名必须准确为 `res`，动画扩展名使用小写 `.anim`。
- `res/<Role>.png`、`res/<Role>.mat` 和至少一个 `.anim` 为现有检查逻辑的必需文件。即使 FBX 内嵌了动画，也需要将用于烘焙的动画准备成 `res` 中的 `.anim`。
- FBX 的激活层级中至少有一个对象带 `SkinnedMeshRenderer`（可位于根对象），且该对象名称包含角色名，例如 `Hero_Body`。禁用的 Renderer 组件仍会被检索；此处 `Contains` 匹配区分大小写，Renderer 的 `rootBone` 和骨骼绑定也必须有效。
- 当前实现只导出选中的一个 SkinnedMeshRenderer；有多个名称匹配时会选择遍历中的最后一个，不会自动合并身体、头发或装备。建议先准备单 SMR、单 submesh 的输入。
- 骨骼使用统一缩放，即同一骨骼 X/Y/Z 缩放一致；顶点使用最多四个骨骼权重。名称避免空格和中文，以满足现有生成器的命名检查。

输入 `.mat` 会参与资源检查和读取，但输出材质由工具重新创建，不会完整复制输入材质的属性。

## 烘焙

在 Unity 顶部菜单选择其中一项：

| 菜单 | 输出材质使用的 Shader |
| --- | --- |
| **GpuSkinning > No PBR** | `Custom/GpuSkinningAnimation` |
| **GpuSkinning > PBR** | `Custom/GpuSkinningAnimationPBR` |

每次执行会扫描全部角色目录，不仅处理当前选中的对象。工具在生成每个角色前会删除并重建已有的 `Assets/Main/Prefabs/GpuRole/<Role>/` 输出目录；请把自己维护的额外材质、prefab 变体等放在其他目录，避免重新烘焙时被替换。

以 `Hero` 为例，输出为：

```text
Assets/Main/Prefabs/GpuRole/Hero/
├── Hero_DynPre.prefab
├── Hero_DynMat.mat
├── Hero_Data.bytes
├── Hero_Data.png
├── <原始 sharedMesh.name>_Mesh.asset
├── Hero.png
└── Hero_mask.png               # 输入存在时复制
```

| 文件 | 用途 |
| --- | --- |
| `Hero_DynPre.prefab` | 含 MeshFilter、MeshRenderer 和 GpuAnimator 的生成对象 |
| `Hero_DynMat.mat` | 已配置动画 PNG、`_BoneNum`、`_AnimationTexSize`，并开启 Instancing |
| `Hero_Data.png` | GPU 读取的逐帧骨骼数据纹理；不是角色主贴图 |
| `Hero_Data.bytes` | 可按 TextAsset 加载的序列化元数据；由 `FromBytes` 解码 |
| `<sharedMesh.name>_Mesh.asset` | 保存网格及 UV 通道中的骨骼索引、权重；文件名取自原始 Mesh，未必等于角色名 |
| `Hero.png` / `Hero_mask.png` | 复制的主贴图及可选遮罩 |

prefab 不会自动加载 `.bytes` 或开始播放。使用 PBR 输出时，应结合项目灯光和材质需要配置其余属性，例如 LUT；工具只绑定它负责生成的资源。

## 首次播放

先通过项目自己的加载方式取得生成 prefab 和对应 `.bytes` TextAsset。以下代码假设 `loadedData`、`instance` 和 `clipName` 已由业务代码提供：

```csharp
// 同一角色解码一次，缓存结果，供后续实例共享。
GpuSkinningAnimData sharedData = GpuSkinningAnimData.FromBytes(loadedData.bytes);

GpuAnimator player = instance.GetComponent<GpuAnimator>();
if (player == null)
    player = instance.AddComponent<GpuAnimator>();

Renderer renderer = instance.GetComponent<Renderer>();
player.AutomaticUpdate = true;
if (player.Initialize(renderer, sharedData))
{
    bool started = player.TryPlay(clipName);
    // started == false 时按业务需要处理缺失或无效 Clip。
}
```

若 Renderer 就在组件所在对象上，可以调用 `player.Initialize(sharedData)`。Renderer 必须已经使用同一套烘焙资源的 Mesh 和材质；初始化只绑定引用，不配置材质或修正资源配对。

`TryPlay` 成功后立即显示起始帧，默认从 Clip 第一帧开始，循环方式采用烘焙设置。一次性动作可使用 `loop: false`，群体动画可使用 `randomizeStart: true`。

要检查实际可用的动画名称，可以遍历元数据，而不用猜测文件名：

```csharp
foreach (GpuSkinningAnimClip clip in sharedData.clips)
{
    if (clip != null)
        Debug.Log(clip.name);
}
```

完整操作示例见 [Basic Playback](../Samples~/BasicPlayback/README.md)，统一调度见 [Manual Crowd](../Samples~/ManualCrowd/README.md)。API 的失败和回调语义见 [API 参考](api.md)。

## 导入示例

在 Package Manager 的本包详情页 **Samples** 中导入所需示例。示例使用 Inspector 引用已有烘焙资源，不附带模型或预烘焙角色，也不会进入 Play 模式后自动选择动画。

Basic Playback 脚本挂到场景中的烘焙 prefab 实例，指定 Renderer、元数据 TextAsset 和项目自己的 Clip 名称；进入 Play 模式后，通过组件右键菜单 **Initialize Sample** 初始化，再执行 **Play Loop** 或 **Play Action**。

Manual Crowd 脚本挂到空 GameObject，指定根节点带 Renderer 的烘焙 prefab、元数据和 Clip 名称；进入 Play 模式后执行组件右键菜单 **Spawn Crowd**。它共享一次解码结果，关闭实例的自动更新并统一 Tick。字段和其余操作见各示例 README。
