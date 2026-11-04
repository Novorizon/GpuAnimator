# GpuAnimator

`main` provides a standalone GameObject/MonoBehaviour player. It depends only on
Unity and does not load assets or require an ECS framework. The previous
PureMVCFramework entity/component implementation is preserved on the
[`ecs` branch](https://github.com/Novorizon/GpuAnimator/tree/ecs).

Version 2 replaces the static, entity-based API with a `GpuAnimator` component.
Existing ECS integrations can continue using the `ecs` branch.

## Installation

Add this Git URL in Unity Package Manager:

```text
https://github.com/Novorizon/GpuAnimator.git#main
```

The package targets Unity 2020.2 or later. The included shaders use the Built-in
Render Pipeline. Runtime and Editor code compile in separate assemblies; no
external framework packages or assembly GUID references are required.

## Caller-supplied resources

Load the prefab, mesh, material, animation texture and baked `.bytes` metadata
through your own loader. The renderer you pass in must already have its mesh
and GPU animation material configured. The generated material carries the
animation texture, bone count and texture dimensions.

Decode the metadata once and share it across instances:

```csharp
GpuSkinningAnimData sharedData = GpuSkinningAnimData.FromBytes(loadedData.bytes);
```

Then initialize each instance with its renderer and the shared metadata:

```csharp
GpuAnimator player = instance.GetComponent<GpuAnimator>();
if (player == null)
    player = instance.AddComponent<GpuAnimator>();

if (player.Initialize(renderer, sharedData))
    player.TryPlay(clipName);
```

Here `loadedData`, `instance`, `renderer` and `clipName` come from business code.
`Initialize(sharedData)` is also available when the renderer is on the same
GameObject. Newly generated prefabs already include a `GpuAnimator` component.
Initialization does not choose or start an animation.

`FromBytes` only decodes the existing baked format; it does not fetch a path or
load an asset. Keep loaded assets alive while their renderers use them. The
player does not clone materials or unload/destroy caller-owned resources.

## Playback

```csharp
bool started = player.TryPlay(clipName);
bool startedOnce = player.TryPlay(clipName, loop: false);
bool startedStaggered = player.TryPlay(clipName, randomizeStart: true, loop: true);

player.TryPlay(clipName, callback: (sender, userdata) =>
{
    // Called once when a non-looping animation completes.
}, loop: false);
```

`TryPlay` returns `false` before initialization, for unknown names or invalid clip
metadata. It starts at the first frame unless `randomizeStart` is enabled. A
missing loop override uses the baked clip setting without changing shared data.
`Play(clipName, callback, userdata)` is a convenience method with a randomized
starting phase. Select animations when business state changes rather than
restarting them every frame.

By default, the component advances with `Time.deltaTime` in `Update`. To drive
many instances from an external update loop, select manual updates:

```csharp
player.AutomaticUpdate = false;
player.Tick(deltaTime);
```

`Tick` is explicit and always advances valid playback, so disable automatic
updates before using it. `Stop()` stops playback and keeps the current pose.
`Clear()` also releases the injected references for pooling or reinitialization.
Neither invokes the completion callback. `IsInitialized`, `IsPlaying`,
`CurrentClipName` and `CurrentFrame` expose playback state.

Renderers and property blocks are cached. If business code changes the renderer's
property block directly during playback, call `player.SetPropertyBlock()` to
refresh the cached values before the next tick.

## Updating baked assets

GPU animation uses a rotation quaternion, translation and one uniform scale per
bone, followed by four-weight linear blend skinning. Use matching X/Y/Z scale;
independent axis scale and shear are outside this data format.

Rebake existing animation textures after updating. Uniform scale is now stored
directly instead of clamping the scale vector's magnitude, and byte decoding
matches the encoder's binary fractions. Old textures cannot recover scale
values already lost during baking. The data class and runtime assembly names
remain unchanged so the existing `.bytes` metadata format can still be decoded.

## Framework integration

Framework adapters only need to load/cache assets, call `Initialize` and select
clips. They may optionally drive manual ticks and call `Clear` when recycling an
instance. Async loading and cancellation stay in the adapter, which can check
instance lifetime before injecting a completed load. The GPU player has no
dependency on Framework, its resource manager or its entity types.

![image](https://github.com/Novorizon/GpuAnimator/raw/main/GPU%20Skinning.png)
