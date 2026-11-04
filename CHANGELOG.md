# Changelog

## 3.0.0 — 2026-11-05

- 公开数据类型 `GpuSkinningAnimData` 改为 `AnimationData`，`GpuSkinningAnimClip` 改为 `AnimationClipData`。
- 构造参数、相关 Editor 标识符、示例及文档统一使用完整的 Animation 名称。
- `AnimationData.FromBytes` 兼容旧 `.bytes` 中的类型名称，序列化字段、动画纹理格式及播放器组件名称保持不变。
- 这是源码 API 改名：升级时更新类型引用；已有修复版动画纹理不因本次改名而需要重新烘焙。

## 2.0.1 — 2026-11-05

- 添加可从 Unity Package Manager 导入的 Basic Playback 和 Manual Crowd 示例。
- 补充中文安装、烘焙、资源注入、API、对象池及故障排查文档。
- 播放器、Shader 和烘焙数据格式保持 2.0.0 的实现。

## 2.0.0

- `main` 使用独立的 GameObject/MonoBehaviour 播放器，资源由调用方传入。
- 原 PureMVCFramework ECS 版本保留在 `ecs` 分支。
- Runtime 与 Editor 分开编译，移除外部程序集引用。
- 保留旧 `.bytes` 元数据格式，并提供显式 `FromBytes` 解码接口。
