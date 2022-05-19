using System;
using UnityEngine;

// 动画clip信息
[Serializable]
public class GpuSkinningAnimClip
{
    public string name;// 名称
    public int startFrame;// 起始帧
    public int endFrame;// 结束帧
    public bool loop;
    public float frameRate;

    public GpuSkinningAnimClip(string n, int sf, int ef, bool l, float frameRate)
    {
        name = n;
        startFrame = sf;
        endFrame = ef;
        loop = l;
        this.frameRate = frameRate;
    }

    public int Length()
    {
        return endFrame - startFrame + 1;
    }
}

[Serializable]
public class GpuSkinningAnimData
{
    public int texWidth;
    public int texHeight;
    public byte[] texBytes;

    public GpuSkinningAnimClip[] clips;

    public int totalFrame;
    public int totalBoneNum;
}