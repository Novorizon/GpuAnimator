using PureMVCFramework;
using PureMVCFramework.Entity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using Random = UnityEngine.Random;

public static class GpuAnimator
{
    public static Dictionary<string, GpuSkinningAnimClip[]> clips = new Dictionary<string, GpuSkinningAnimClip[]>();

    private static readonly int FrameIndexId = Shader.PropertyToID("_FrameIndex");
    private static readonly int BlendFrameIndexId = Shader.PropertyToID("_BlendFrameIndex");

    public static void AddAnimationComponent(Entity entity, string textAssetPath)
    {
        if (string.IsNullOrEmpty(textAssetPath))
            return;

        if (clips.TryGetValue(textAssetPath, out GpuSkinningAnimClip[] cachedClips))
        {
            SetClips(entity, cachedClips);
            return;
        }

        ResourceManager.Instance.LoadAssetAsync<TextAsset>(textAssetPath, (asset, _) =>
        {
            if (asset == null)
                return;

            // Another request for the same asset may have completed while this one was loading.
            if (!clips.TryGetValue(textAssetPath, out GpuSkinningAnimClip[] loadedClips))
            {
                using (MemoryStream stream = new MemoryStream(asset.bytes))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    GpuSkinningAnimData data = (GpuSkinningAnimData)formatter.Deserialize(stream);
                    loadedClips = data.clips;
                }
                clips[textAssetPath] = loadedClips;
            }

            SetClips(entity, loadedClips);
        });
    }

    private static void SetClips(Entity entity, GpuSkinningAnimClip[] animationClips)
    {
        GpuAnimation gpu = entity.GetOrAddComponentData<GpuAnimation>();
        gpu.clips = animationClips;
        if (gpu.current == null)
            gpu.current = new GpuAnimation.AnimationClip();
        if (gpu.prop == null)
            gpu.prop = new MaterialPropertyBlock();
    }

    public static void SetPropertyBlock(Entity entity)
    {
        GpuAnimation gpu = entity.GetOrAddComponentData<GpuAnimation>();
        if (gpu == null)
            return;

        if (gpu.prop == null)
            gpu.prop = new MaterialPropertyBlock();

        Renderer renderer = ResolveRenderer(entity, gpu);
        if (renderer != null)
            renderer.GetPropertyBlock(gpu.prop);
    }

    /// <summary>
    /// Plays the named animation with the existing randomized starting phase.
    /// The callback runs once after a non-looping animation finishes.
    /// </summary>
    public static void Play(Entity entity, string name, Action<Entity, object> callback = null, object userdata = null)
    {
        TryPlay(entity, name, callback, userdata, randomizeStart: true);
    }

    /// <summary>
    /// Plays a clip selected by the caller. Returns false when the clip is unavailable
    /// or its playback data is invalid. Set randomizeStart to stagger crowd animations.
    /// A null loop override uses the baked clip setting without changing shared data.
    /// </summary>
    public static bool TryPlay(Entity entity, string clipName, Action<Entity, object> callback = null, object userdata = null, bool randomizeStart = false, bool? loop = null)
    {
        if (string.IsNullOrEmpty(clipName))
            return false;

        GpuAnimation gpu = entity.GetOrAddComponentData<GpuAnimation>();
        if (gpu == null || gpu.clips == null)
            return false;

        for (int i = 0; i < gpu.clips.Length; i++)
        {
            GpuSkinningAnimClip clip = gpu.clips[i];
            if (clip == null || !string.Equals(clip.name, clipName, StringComparison.Ordinal))
                continue;

            if (clip.startFrame < 0 || clip.endFrame < clip.startFrame ||
                clip.frameRate <= 0 || float.IsNaN(clip.frameRate) || float.IsInfinity(clip.frameRate))
                return false;

            float duration = 1.0f / clip.frameRate;
            if (duration <= 0 || float.IsInfinity(duration))
                return false;

            if (gpu.current == null)
                gpu.current = new GpuAnimation.AnimationClip();

            int offset = randomizeStart ? Random.Range(0, (clip.endFrame - clip.startFrame) / 3) : 0;
            gpu.current.clip = clip;
            gpu.current.currentFrame = clip.startFrame + offset;
            gpu.current.lastFrame = gpu.current.currentFrame;
            gpu.current.completed = false;
            gpu.current.loop = loop ?? clip.loop;
            gpu.current.callback = callback;
            gpu.current.userdata = userdata;
            gpu.duration = duration;
            gpu.timer = 0;

            SetPropertyBlock(entity);
            ApplyFrame(entity, gpu);
            return true;
        }

        return false;
    }

    internal static void ApplyFrame(Entity entity, GpuAnimation gpu)
    {
        if (gpu.prop == null)
            gpu.prop = new MaterialPropertyBlock();

        Renderer renderer = ResolveRenderer(entity, gpu);

        gpu.prop.SetInt(BlendFrameIndexId, gpu.current.lastFrame);
        gpu.prop.SetInt(FrameIndexId, gpu.current.currentFrame);

        if (renderer != null)
            renderer.SetPropertyBlock(gpu.prop);
    }

    private static Renderer ResolveRenderer(Entity entity, GpuAnimation gpu)
    {
        if (!entity.gameObject)
        {
            gpu.renderer = null;
            return null;
        }

        if (gpu.renderer == null || gpu.renderer.gameObject != entity.gameObject)
        {
            gpu.renderer = entity.gameObject.GetComponent<Renderer>();
            if (gpu.renderer != null)
                gpu.renderer.GetPropertyBlock(gpu.prop);
        }

        return gpu.renderer;
    }
}
