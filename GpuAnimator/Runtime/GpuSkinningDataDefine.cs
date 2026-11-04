using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

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

    /// <summary>
    /// Decodes bytes produced by the baking tool. Loading and caching the source
    /// asset remain the caller's responsibility; share the decoded data across instances.
    /// </summary>
    public static GpuSkinningAnimData FromBytes(byte[] bytes)
    {
        if (bytes == null)
            throw new ArgumentNullException(nameof(bytes));

        using (MemoryStream stream = new MemoryStream(bytes, false))
        {
            BinaryFormatter formatter = new BinaryFormatter();
            GpuSkinningAnimData data = formatter.Deserialize(stream) as GpuSkinningAnimData;
            if (data == null)
                throw new SerializationException("The asset does not contain GPU animation data.");

            return data;
        }
    }
}
