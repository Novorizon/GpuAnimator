# GpuAnimator 使用文档

GpuAnimator 将骨骼动画离线写入纹理，在顶点 Shader 中执行蒙皮。`main` 分支提供独立的 `GpuAnimator : MonoBehaviour`：业务代码传入 Renderer 和动画元数据，组件负责选择帧并更新材质属性。

包声明的最低 Unity 版本为 **2020.2**，附带 Shader 面向 **Built-in Render Pipeline**。URP/HDRP 项目需要移植 Shader；仅导入本包不会完成管线适配。

## 从哪里开始

| 文档 | 内容 |
| --- | --- |
| [快速开始](getting-started.md) | 安装、原始资源目录、烘焙菜单、生成文件与首次播放 |
| [API 参考](api.md) | 初始化、播放、回调、手动推进和状态语义 |
| [项目接入](integration.md) | 资源缓存、框架适配、批量更新、对象池和 PropertyBlock |
| [问题排查](troubleshooting.md) | 烘焙失败、无动画、播放状态、贴图及渲染限制 |
| [Basic Playback 示例](../Samples~/BasicPlayback/README.md) | 单个对象的播放接入 |
| [Manual Crowd 示例](../Samples~/ManualCrowd/README.md) | 共享数据与外部统一推进 |

## 分支与版本

| 分支 | 播放方式 | 依赖与用途 |
| --- | --- | --- |
| [`main`](https://github.com/Novorizon/GpuAnimator/tree/main) | GameObject / MonoBehaviour，本文档对应的 2.x API | 依赖 Unity，由调用方加载资源；不依赖 ECS 或业务 Framework |
| [`ecs`](https://github.com/Novorizon/GpuAnimator/tree/ecs) | 原有 Entity / Component / System 实现 | 保留 PureMVCFramework 接入方式，继续维护已有 ECS 项目时使用 |

2.x 将旧的静态 Entity API 改为组件实例 API。旧代码中的 `GpuAnimator.Play(entity, ...)` 不能直接套用到 `main`；请按[接入文档](integration.md)迁移，或使用 `ecs` 分支。两个分支是替代版本，不要把它们同时导入同一个 Unity 项目。

## 数据与职责

烘焙工具生成 Mesh、GPU 动画材质、动画 PNG、`.bytes` 元数据和 prefab。PNG 保存逐帧骨骼变换；`.bytes` 保存 Clip 名称、帧区间、帧率等元数据，不包含可直接替代 PNG 的动画纹理。

资源加载器负责加载和保持资源有效，业务逻辑负责决定动画名称。`GpuAnimator` 不持有资源路径，不调用资源管理器，不自动选择动画，也不创建或销毁调用方的材质。

同一套烘焙资源的 `.bytes` 可以解码一次，在多个实例之间共享；每个实例通过自己的 `MaterialPropertyBlock` 保存当前帧。生成的材质已开启 GPU Instancing，但实际批处理结果仍取决于 Unity 的渲染条件，请在目标项目中检查。

## 格式边界

当前格式对每根骨骼保存旋转四元数、位移和一个统一缩放量，再执行最多四权重的线性混合蒙皮（LBS）。骨骼缩放应满足 X/Y/Z 一致；独立轴缩放、剪切不属于此格式。函数名中的 `DualQuaternionToMatrix` 不代表执行了双四元数混合蒙皮（DQS）。

播放按烘焙帧率离散采样，没有帧间插值或动画过渡混合。静态 Mesh 包围盒、多 SkinnedMeshRenderer 烘焙等边界见[问题排查](troubleshooting.md)。

从旧版本升级时应重新烘焙动画 PNG：统一缩放现直接保存实际缩放值，解码也已与编码的小数进制对齐。旧 PNG 中已经丢失的缩放信息无法在运行时恢复；`.bytes` 仍沿用现有数据类和序列化格式。

源码入口：[播放器](../GpuAnimator/Runtime/GpuAnimator.cs)、[元数据](../GpuAnimator/Runtime/GpuSkinningDataDefine.cs)、[烘焙工具](../GpuAnimator/Editor/GpuSkinningTools.cs)。
