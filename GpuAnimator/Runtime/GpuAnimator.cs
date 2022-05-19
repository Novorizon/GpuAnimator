
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

    public static Dictionary<string, GpuSkinningAnimClip[]> clips;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="textAssetPath">动画资源名称</param>
    public static void AddAnimationComponent(Entity entity, string textAssetPath)
    {
        if (string.IsNullOrEmpty(textAssetPath))
            return;

        if (clips.ContainsKey(textAssetPath))
        {
            GpuAnimation gpu = entity.GetOrAddComponentData<GpuAnimation>();
            gpu.clips = clips[textAssetPath];
            gpu.prop = new MaterialPropertyBlock();
        }
        else
        {
            ResourceManager.Instance.LoadAssetAsync<TextAsset>(textAssetPath, (asset, _) =>
            {
                if (asset != null)
                {
                    MemoryStream ms = new MemoryStream(asset.bytes);
                    BinaryFormatter bf = new BinaryFormatter();
                    GpuSkinningAnimData data = (GpuSkinningAnimData)bf.Deserialize(ms);
                    ms.Close();
                    clips.Add(textAssetPath, data.clips);
                    GpuAnimation gpu = entity.GetOrAddComponentData<GpuAnimation>();
                    gpu.clips = data.clips;
                    gpu.clips = clips[textAssetPath];
                    gpu.prop = new MaterialPropertyBlock();
                }
            });
        }
    }

    public static void SetPropertyBlock(Entity entity)
    {
        if (entity.gameObject == null || entity.gameObject.GetComponent<Renderer>() == null)
            return;

        GpuAnimation gpu = entity.GetOrAddComponentData<GpuAnimation>();
        if (gpu == null)
            return;

        if (gpu.prop == null)
            gpu.prop = new MaterialPropertyBlock();
        entity.gameObject.GetComponent<Renderer>().GetPropertyBlock(gpu.prop);
    }

    /// <summary>
    ///  Play Animation of param "name". Call back after it is done if not loop.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="name"></param>
    /// <param name="callback"></param>
    /// <param name="userdata"></param>
    public static void Play(Entity entity, string name, Action<Entity, object> callback = null, object userdata = null)
    {
        GpuAnimation gpu = entity.GetOrAddComponentData<GpuAnimation>();
        if (gpu == null)
            return;

        for (int i = 0; i < gpu.clips.Length; i++)
        {
            if (gpu.clips[i].name == name)
            {
                gpu.current.clip = gpu.clips[i];
                gpu.current.clip.loop = true;
                gpu.current.lastFrame = gpu.current.currentFrame;
                int offset = Random.Range(0, (gpu.current.clip.endFrame - gpu.current.clip.startFrame) / 3);
                gpu.current.currentFrame = gpu.current.clip.startFrame + offset;
                gpu.duration = 1 / gpu.current.clip.frameRate;
                gpu.timer = 0;

                gpu.current.callback = callback;
                gpu.current.userdata = userdata;
            }
            break;
        }
    }

}