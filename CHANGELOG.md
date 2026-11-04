# Changelog

## 2.0.1 — 2026-11-05

- 添加可从 Unity Package Manager 导入的 Basic Playback 和 Manual Crowd 示例。
- 补充中文安装、烘焙、资源注入、API、对象池及故障排查文档。
- 播放器、Shader 和烘焙数据格式保持 2.0.0 的实现。

## 2.0.0

- `main` 使用独立的 GameObject/MonoBehaviour 播放器，资源由调用方传入。
- 原 PureMVCFramework ECS 版本保留在 `ecs` 分支。
- Runtime 与 Editor 分开编译，移除外部程序集引用。
- 保留旧 `.bytes` 元数据格式，并提供显式 `FromBytes` 解码接口。
