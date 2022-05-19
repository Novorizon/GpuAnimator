using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor;
using UnityEngine;

/* 
使用限制：
	1.支持同一个fbx下有多个skinnedMeshRenderer，也支持一个skinnedMeshRenderer有多个subMesh，但这些网格模型都必须使用同一张主纹理贴图(多个也是能实现的，要自己特殊处理，看后面需求)
	2.无法保持fbx的节点层级，只会生成一个prefab，上述的网格信息都将被存到同一个mesh中

*/


public class GpuSkinningGenerator
{
    // 生成类型
    public enum GenerateType
    {
        None = 0,

        PBR,

        GpuInstance,
        // Noise Animation -- 自动instance 噪点顶点动画
        NoiseVerticesAnim,
    }

    // 每根骨骼每帧所占像素空间(0,1:rotation, 2,3:translation)
    //static readonly int DEFAULT_PER_FRAME_BONE_DATASPACE = 4;
    // 每根骨骼每帧所占像素空间(0,1,2,3:rotation, 4,5,6,7:translation)
    static int DEFAULT_PER_FRAME_BONE_DATASPACE = 8;

    public GameObject curGameObject = null;
    public RuntimeAnimatorController controller = null;
    public List<AnimationClip> clipList = null;
    public List<Mesh> meshList = null;


    private GenerateType genType = (GenerateType)0;
    private GpuSkinningAnimData animData = null;
    // 生成的网格
    Mesh instMesh = null;
    List<Vector4> boneIndicesList = null;
    List<Vector4> boneWeightsList = null;
    // 生成的材质
    Material instMaterial = null;
    // 原始的默认材质
    Material defaultMaterial = null;
    private AnimationClip[] animClips = null;
    private SkinnedMeshRenderer selectedSkinnedMeshRenderer = null;
    private List<GpuSkinningAnimClip> clipsData = null;
    Dictionary<Transform, int> boneIds = null;
    Dictionary<int, Matrix4x4> boneBindposes = null;
    string animTexturePath; // 动画纹理保存路径

    static int id = 100001;
    public GpuSkinningGenerator(GenerateType type = 0)
    {
        genType = type;
        clipsData = new List<GpuSkinningAnimClip>();
        boneBindposes = new Dictionary<int, Matrix4x4>();
        boneIndicesList = new List<Vector4>();
        boneWeightsList = new List<Vector4>();
    }

    public void reset()
    {
        curGameObject = null;
        animData = null;
        clipsData.Clear();
        if (boneIds != null)
        {
            boneIds.Clear();
        }
    }

    // 设置选中的模型
    public void setSelectedModel(GameObject obj, GenerateType type, List<AnimationClip> clips, SkinnedMeshRenderer skinnedMeshRenderer, List<Mesh> meshes)
    {
        if (obj == null)
        {
            Debug.LogError("select obj is null!!");
            animData = null;
            return;
        }

        if (curGameObject != obj || animData == null || genType != type || skinnedMeshRenderer != selectedSkinnedMeshRenderer)
        {
            genType = type;
            curGameObject = obj;
            clipList = clips;
            selectedSkinnedMeshRenderer = skinnedMeshRenderer;
            meshList = meshes;
            refreshGeneratorInfo();
        }
    }

    public void refreshGeneratorInfo()
    {
        animData = new GpuSkinningAnimData();
        int totalFrame = 0;
        int clipFrame = 0;
        clipsData.Clear();

        boneIds = resortBone(curGameObject);
        animData.totalBoneNum = boneIds.Count;

        animClips = clipList.ToArray();
        for (int i = 0; i < animClips.Length; ++i)
        {
            AnimationClip clip = animClips[i];
            clipFrame = (int)(clip.frameRate * clip.length);

            AnimationClipSettings clipSetting = AnimationUtility.GetAnimationClipSettings(clip);

            GpuSkinningAnimClip clipData = new GpuSkinningAnimClip(clip.name, totalFrame, totalFrame + clipFrame - 1, clipSetting.loopTime, clip.frameRate);
            clipsData.Add(clipData);

            totalFrame += clipFrame;
        }
        animData.totalFrame = totalFrame;
        animData.clips = clipsData.ToArray();

        long totalPixels = boneIds.Count * DEFAULT_PER_FRAME_BONE_DATASPACE * totalFrame;
        calTextureSize(totalPixels, out animData.texWidth, out animData.texHeight);
    }

    // 根据所需空间计算纹理大小
    private void calTextureSize(long totalPixels, out int width, out int height)
    {
        int step = 0;

        width = 32;
        height = 32;
        while (width * height < totalPixels)
        {
            if (step % 2 == 0)
            {
                width *= 2;
            }
            else
            {
                height *= 2;
            }
            ++step;
        }
    }

    Matrix4x4 matrixMulFloat(ref Matrix4x4 matrix, float val)
    {
        matrix.m00 *= val;
        matrix.m01 *= val;
        matrix.m02 *= val;
        matrix.m03 *= val;
        matrix.m10 *= val;
        matrix.m11 *= val;
        matrix.m12 *= val;
        matrix.m13 *= val;
        matrix.m20 *= val;
        matrix.m21 *= val;
        matrix.m22 *= val;
        matrix.m23 *= val;
        matrix.m30 *= val;
        matrix.m31 *= val;
        matrix.m32 *= val;
        matrix.m33 *= val;
        return matrix;
    }
    Matrix4x4 matrixAddMatrix(Matrix4x4 mat1, Matrix4x4 mat2)
    {
        Matrix4x4 matrix = new Matrix4x4();
        matrix.m00 = mat1.m00 + mat2.m00;
        matrix.m01 = mat1.m01 + mat2.m01;
        matrix.m02 = mat1.m02 + mat2.m02;
        matrix.m03 = mat1.m03 + mat2.m03;
        matrix.m10 = mat1.m10 + mat2.m10;
        matrix.m11 = mat1.m11 + mat2.m11;
        matrix.m12 = mat1.m12 + mat2.m12;
        matrix.m13 = mat1.m13 + mat2.m13;
        matrix.m20 = mat1.m20 + mat2.m20;
        matrix.m21 = mat1.m21 + mat2.m21;
        matrix.m22 = mat1.m22 + mat2.m22;
        matrix.m23 = mat1.m23 + mat2.m23;
        matrix.m30 = mat1.m30 + mat2.m30;
        matrix.m31 = mat1.m31 + mat2.m31;
        matrix.m32 = mat1.m32 + mat2.m32;
        matrix.m33 = mat1.m33 + mat2.m33;
        return matrix;
    }

    void exportTexture(Texture2D texture, string path)
    {
        byte[] bytes = texture.EncodeToPNG();
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        FileStream fs = new FileStream(path, FileMode.Create);
        fs.Write(bytes, 0, bytes.Length);
        fs.Close();
    }

    void setAnimationTextureProperties(string path)
    {
        AssetDatabase.Refresh();
        TextureImporter texture = AssetImporter.GetAtPath(path) as TextureImporter;

        texture.textureFormat = TextureImporterFormat.RGBA32;
        texture.textureCompression = TextureImporterCompression.Uncompressed;

        texture.filterMode = FilterMode.Point;
        texture.mipmapEnabled = false;
        texture.npotScale = TextureImporterNPOTScale.None;
        texture.sRGBTexture = false;
        AssetDatabase.ImportAsset(path);
    }


    // 骨骼动画纹理
    public void generate(string parentFolder, string savePath, string dataFileName, string matFileName, string prefabFileName, string mainTexPath, GenerateType generateType, string maskTex)
    {
        genType = generateType;

        // 生成纹理数据
        GenerateTexAndMesh(parentFolder, savePath, dataFileName);


        // 生成材质
        generateMaterial(savePath, matFileName, mainTexPath, maskTex);

        // 生成prefab
        generatePrefab(savePath, prefabFileName, dataFileName, parentFolder);

        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();
    }

    public void generatePrefab(string savePath, string prefabFileName, string dataFileName, string parentFolder)
    {
        string prefabName = prefabFileName.Substring(0, prefabFileName.Length - ".prefab".Length);
        GameObject prefab = new GameObject(prefabFileName);

        // 组件
        MeshFilter meshFilter = prefab.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = instMesh;
        MeshRenderer renderer = prefab.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = instMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Material[] mats = new Material[] { instMaterial };
        if (defaultMaterial != null)
        {
            renderer.materials = new Material[] { instMaterial, defaultMaterial };
        }
        else
        {
            renderer.material = instMaterial;
        }


        string prefabPath = Path.Combine(savePath, prefabFileName);
        PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
        //GameObject obj = PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);

        //        MeshFilter filter = obj.GetComponent<MeshFilter>();
        //        if (filter == null || filter.sharedMesh == null)
        //        {
        //            filter.sharedMesh = instMesh;
        //            Debug.Log("no mesh");
        //            MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();
        //                meshRenderer.sharedMaterial = instMaterial;
        //        }
        //        if (filter != null || filter.sharedMesh != null)
        //        {
        //            if (filter.sharedMesh.uv2 == null)
        //                Debug.Log("no uv2");
        //        }


        GameObject.DestroyImmediate(prefab);
        AssetDatabase.Refresh();
    }

    public void generateMaterial(string savePath, string matFileName, string mainTexPath, string maskTexPath)
    {
        instMaterial = null;
        defaultMaterial = null;

        Shader shader;

        shader = Shader.Find(GpuSkinningTools.DEFAULT_USE_SHADER_NAME);
        instMaterial = new Material(shader);
        instMaterial.enableInstancing = true;
        //defaultMaterial = defaultMat;

        Texture2D saved_animTex = AssetDatabase.LoadAssetAtPath<Texture2D>(animTexturePath);
        // 材质
        if (File.Exists(mainTexPath))
        {
            Texture2D mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(mainTexPath);
            instMaterial.SetTexture("_MainTex", mainTex);
        }
        if (File.Exists(maskTexPath))
        {
            Texture2D maskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(maskTexPath);
            instMaterial.SetTexture("_Mask", maskTex);
        }
        instMaterial.SetTexture("_AnimationTex", saved_animTex);
        instMaterial.SetInt("_BoneNum", animData.totalBoneNum);
        instMaterial.SetVector("_AnimationTexSize", new Vector4(animData.texWidth, animData.texHeight, 0, 0));

        string matPath = Path.Combine(savePath, matFileName);
        AssetDatabase.CreateAsset(instMaterial, matPath);
        EditorUtility.SetDirty(instMaterial);
        AssetDatabase.Refresh();
    }

    public void generateTexAndMesh(string parentFolder, string savePath, string dataFileName)
    {
        int numBones = animData.totalBoneNum;

        // 重新生成mesh
        rebuildAllMeshes(savePath, parentFolder);

        // 将骨骼矩阵写入纹理
        var tex2D = new Texture2D(animData.texWidth, animData.texHeight, TextureFormat.RGBA32, false);
        tex2D.filterMode = FilterMode.Point;
        int clipIdx = 0;
        int pixelIdx = 0;
        Vector2Int pixelUv;
        GpuSkinningAnimClip boneAnimation = null;
        AnimationClip clip = null;
        List<Matrix4x4> boneMatrices = null;

        //每个不同动画需要标记一下始末位置
        for (clipIdx = 0; clipIdx < animClips.Length; ++clipIdx)
        {
            boneAnimation = animData.clips[clipIdx];
            clip = animClips[clipIdx];
            //GpuSkinningTools.AnimClipState(clip, boneAnimation);
            for (int frameIndex = 0; frameIndex < boneAnimation.Length(); frameIndex++)
            {
                boneMatrices = samplerAnimationClipBoneMatrices(curGameObject, clip, (float)frameIndex / clip.frameRate);
                for (int boneIndex = 0; boneIndex < numBones; boneIndex++)
                {
                    Matrix4x4 matrix = boneMatrices[boneIndex];
                    Quaternion rotation = matrix.rotation;// ToQuaternion(matrix);//直接取rotation
                    //GpuSkinningUtility.WritePBR(matrix);

                    Vector3 scale = matrix.lossyScale;
                    float sx = Mathf.Floor(scale.x * 100.0f);
                    float sy = Mathf.Floor(scale.y * 100.0f);
                    float sz = Mathf.Floor(scale.z * 100.0f);
                    if ((sx - sy) > 5.0f
                        || (sx - sz) > 5.0f
                        || (sy - sz) > 5.0f)
                    {
                        Transform remapBone = null;
                        foreach (var key in boneIds.Keys)
                        {
                            if (boneIds[key] == boneIndex)
                            {
                                remapBone = key;
                                break;
                            }
                        }
                        string strLog = string.Format("AnimClip scale X Y Z not equal: {0} -> {1} {2}", curGameObject.name, boneAnimation.name, remapBone.transform.name);
                        Warning("AnimClip scale", strLog);
                    }

                    pixelUv = convertPixel2UV(pixelIdx++);

                    Color color1 = convertFloat16Bytes2Color(convertFloat32toFloat16Bytes(rotation.x), convertFloat32toFloat16Bytes(rotation.y));
                    tex2D.SetPixel(pixelUv.x, pixelUv.y, color1);
                    pixelUv = convertPixel2UV(pixelIdx++);
                    Color color2 = convertFloat16Bytes2Color(convertFloat32toFloat16Bytes(rotation.z), convertFloat32toFloat16Bytes(rotation.w));
                    tex2D.SetPixel(pixelUv.x, pixelUv.y, color2);
                    pixelUv = convertPixel2UV(pixelIdx++);
                    Color color3 = convertFloat16Bytes2Color(convertFloat32toFloat16Bytes(matrix.GetColumn(3).x), convertFloat32toFloat16Bytes(matrix.GetColumn(3).y));
                    tex2D.SetPixel(pixelUv.x, pixelUv.y, color3);
                    pixelUv = convertPixel2UV(pixelIdx++);
                    Color color4 = convertFloat16Bytes2Color(convertFloat32toFloat16Bytes(matrix.GetColumn(3).z), convertFloat32toFloat16Bytes(Mathf.Clamp01(matrix.lossyScale.magnitude)));
                    tex2D.SetPixel(pixelUv.x, pixelUv.y, color4);

                }
            }
        }
        tex2D.Apply();
        // 导出动画纹理
        animTexturePath = savePath + dataFileName.Replace(".bytes", "") + ".png";
        exportTexture(tex2D, animTexturePath);
        setAnimationTextureProperties(animTexturePath);

        // 序列化后存储

        string filePath = Path.Combine(parentFolder, savePath + dataFileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(fs, animData);
        fs.Close();
        AssetDatabase.Refresh();
    }

    public void GenerateTexAndMesh(string parentFolder, string savePath, string dataFileName)
    {
        int numBones = animData.totalBoneNum;

        // 重新生成mesh
        rebuildAllMeshes(savePath, parentFolder);

        // 将骨骼矩阵写入纹理
        var tex2D = new Texture2D(animData.texWidth, animData.texHeight, TextureFormat.RGBA32, false);
        tex2D.filterMode = FilterMode.Point;
        int clipIdx = 0;
        int pixelIdx = 0;
        Vector2Int pixelUv;
        GpuSkinningAnimClip boneAnimation = null;
        AnimationClip clip = null;
        List<Matrix4x4> boneMatrices = null;

        //每个不同动画需要标记一下始末位置
        for (clipIdx = 0; clipIdx < animClips.Length; ++clipIdx)
        {
            boneAnimation = animData.clips[clipIdx];
            clip = animClips[clipIdx];
            //GpuSkinningTools.AnimClipState(clip, boneAnimation);
            for (int frameIndex = 0; frameIndex < boneAnimation.Length(); frameIndex++)
            {
                boneMatrices = samplerAnimationClipBoneMatrices(curGameObject, clip, (float)frameIndex / clip.frameRate);
                for (int boneIndex = 0; boneIndex < numBones; boneIndex++)
                {
                    Matrix4x4 matrix = boneMatrices[boneIndex];
                    Quaternion rotation = matrix.rotation;// ToQuaternion(matrix);//直接取rotation
                    //GpuSkinningUtility.WriteHighPrecision(matrix);
                    Vector3 scale = matrix.lossyScale;
                    float sx = Mathf.Floor(scale.x * 100.0f);
                    float sy = Mathf.Floor(scale.y * 100.0f);
                    float sz = Mathf.Floor(scale.z * 100.0f);
                    if ((sx - sy) > 5.0f
                        || (sx - sz) > 5.0f
                        || (sy - sz) > 5.0f)
                    {
                        Transform remapBone = null;
                        foreach (var key in boneIds.Keys)
                        {
                            if (boneIds[key] == boneIndex)
                            {
                                remapBone = key;
                                break;
                            }
                        }
                        string strLog = string.Format("AnimClip scale X Y Z not equal: {0} -> {1} {2}", curGameObject.name, boneAnimation.name, remapBone.transform.name);
                        Warning("AnimClip scale", strLog);
                    }

                    Color color1 = Float32Bytes2Color(Float32toFloat32Bytes(rotation.x));
                    Color color2 = Float32Bytes2Color(Float32toFloat32Bytes(rotation.y));
                    Color color3 = Float32Bytes2Color(Float32toFloat32Bytes(rotation.z));
                    Color color4 = Float32Bytes2Color(Float32toFloat32Bytes(rotation.w));
                    Color color5 = Float32Bytes2Color(Float32toFloat32Bytes(matrix.GetColumn(3).x));
                    Color color6 = Float32Bytes2Color(Float32toFloat32Bytes(matrix.GetColumn(3).y));
                    Color color7 = Float32Bytes2Color(Float32toFloat32Bytes(matrix.GetColumn(3).z));
                    Color color8 = Float32Bytes2Color(Float32toFloat32Bytes(Mathf.Clamp01(matrix.lossyScale.magnitude)));

                    pixelUv = convertPixel2UV(pixelIdx++);
                    tex2D.SetPixel(pixelUv.x, pixelUv.y, color1);
                    pixelUv = convertPixel2UV(pixelIdx++);
                    tex2D.SetPixel(pixelUv.x, pixelUv.y, color2);
                    pixelUv = convertPixel2UV(pixelIdx++);
                    tex2D.SetPixel(pixelUv.x, pixelUv.y, color3);
                    pixelUv = convertPixel2UV(pixelIdx++);
                    tex2D.SetPixel(pixelUv.x, pixelUv.y, color4);

                    pixelUv = convertPixel2UV(pixelIdx++);
                    tex2D.SetPixel(pixelUv.x, pixelUv.y, color5);
                    pixelUv = convertPixel2UV(pixelIdx++);
                    tex2D.SetPixel(pixelUv.x, pixelUv.y, color6);
                    pixelUv = convertPixel2UV(pixelIdx++);
                    tex2D.SetPixel(pixelUv.x, pixelUv.y, color7);
                    pixelUv = convertPixel2UV(pixelIdx++);
                    tex2D.SetPixel(pixelUv.x, pixelUv.y, color8);
                }
            }
        }
        tex2D.Apply();
        // 导出动画纹理
        animTexturePath = savePath + dataFileName.Replace(".bytes", "") + ".png";
        exportTexture(tex2D, animTexturePath);
        setAnimationTextureProperties(animTexturePath);

        // 序列化后存储

        string filePath = Path.Combine(parentFolder, savePath + dataFileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(fs, animData);
        fs.Close();
        AssetDatabase.Refresh();
    }


    // 用一个RGB32像素，存储两个float16数据
    public static Color convertFloat16Bytes2Color(byte[] data1, byte[] data2)
    {
        Color color = new Color(data1[0] / 255.0f, data1[1] / 255.0f, data2[0] / 255.0f, data2[1] / 255.0f);
        return color;
    }

    // 用两个RGB24像素，存储三个float16数据
    private Color[] convertThreeFloat16Bytes2TwoColor(byte[] data1, byte[] data2, byte[] data3)
    {
        Color[] colors = new Color[2];
        colors[0] = new Color(data1[0] / 255.0f, data1[1] / 255.0f, data2[0] / 255.0f);
        colors[1] = new Color(data2[1] / 255.0f, data3[0] / 255.0f, data3[1] / 255.0f);
        return colors;
    }

    static List<int> integer_rst = new List<int>();
    public static byte[] convertFloat32toFloat16Bytes(float srcValue)
    {
        int integer = (int)srcValue;
        float floats = srcValue - integer;

        if (integer > 127)
        {
            // 超过float16的范围
            EditorUtility.DisplayDialog("警告!!", "模型数据值大于127，超过Float16的范围", "OK");
            integer = 127;
        }
        if (integer < -127)
        {
            // 超过float16的范围
            EditorUtility.DisplayDialog("警告!!", "模型数据值小于-127，超过Float16的范围", "OK");
            integer = -127;
        }

        // 1个符号位(+:1)，7个指数位，8个基数位
        int[] data = new int[16];
        int index = 0;

        // 符号 //1: 负  0:正
        if (srcValue > 0)
        {
            data[index++] = 0;
        }
        else
        {
            data[index++] = 1;
            floats = -(srcValue - integer);
            integer = -integer;
        }

        // 指数位
        integer_rst.Clear();
        while (integer > 0)
        {
            integer_rst.Add(integer % 2);
            integer /= 2;
        }
        if (integer_rst.Count < 7)
        {
            int length = 7 - integer_rst.Count;
            for (int i = 0; i < length; ++i)
            {
                data[index++] = 0;
            }
        }
        for (int i = 0; i < integer_rst.Count; ++i)
        {
            data[index++] = integer_rst[integer_rst.Count - 1 - i];
        }


        // 小数位
        int temp;
        for (int i = 0; i < 8; ++i)
        {
            floats *= 2;
            temp = (int)floats;
            data[index++] = temp;
            floats -= temp;
        }

        byte[] result = new byte[2];
        temp = 0;
        for (int i = 0; i < 8; ++i)
        {
            temp += (int)Math.Pow(2, 7 - i) * data[i];
        }
        result[0] = (byte)temp;
        temp = 0;
        for (int i = 8; i < 16; ++i)
        {
            temp += (int)Math.Pow(2, 15 - i) * data[i];
        }
        result[1] = (byte)temp;
        return result;
    }


    private Vector2Int convertPixel2UV(int idx)
    {
        int row = (int)(idx / animData.texWidth);
        int column = idx - row * animData.texWidth;
        return new Vector2Int(column, row);
    }


    private List<Matrix4x4> samplerAnimationClipBoneMatrices(GameObject obj, AnimationClip clip, float time)
    {
        Transform root = selectedSkinnedMeshRenderer.rootBone;

        List<Matrix4x4> matrices = new List<Matrix4x4>();
        clip.SampleAnimation(obj, time);
        for (int i = 0; i < animData.totalBoneNum; ++i)
        {
            Transform bone = null;
            foreach (var key in boneIds.Keys)
            {
                if (boneIds[key] == i)
                {
                    bone = key;
                    break;
                }
            }

            // 模型空间->骨骼空间->模型空间(这个世界空间不是unity真实世界的空间，它是我们新生成网格的模型空间)
            // 网格模型的顶点是模型空间下的，被绑定到骨骼上(父节点发生改变)所以需要将它转到骨骼节点的坐标系。模型空间->骨骼空间
            // bone.localToWorldMatrix是骨骼节点到模型空间的变换(在播放动画时，它记录了骨骼在模型空间下的变换).	骨骼空间->模型空间
            Matrix4x4 matrixBip001 = bone.localToWorldMatrix * boneBindposes[i];
            matrices.Add(matrixBip001);
        }


        return matrices;
    }

    private void rebuildAllMeshes(string savePath, string parentFolder)
    {
        SkinnedMeshRenderer sm = selectedSkinnedMeshRenderer;

        if (!IslegalString(sm.name))
        {
            string strLog = string.Format("Mesh  命名错误: {0} -> {1}", curGameObject.name, sm.name);
            Warning("命名错误", strLog);
        }

        string meshName = sm.sharedMesh.name;
        instMesh = null;

        string meshPath = Path.Combine(Path.GetDirectoryName(savePath), meshName) + GpuSkinningTools.DEFAULT_SAVE_MESH_NAME;
        if (File.Exists(parentFolder + meshPath.Replace("\\", "/")))
        {
            AssetDatabase.DeleteAsset(meshPath);
        }
        AssetDatabase.Refresh();

        if (sm.sharedMesh.subMeshCount > 1)
        {
            Debug.LogError("Not subMeshCount > 1:" + meshName);
        }

        instMesh = UnityEngine.Object.Instantiate<Mesh>(sm.sharedMesh);
        instMesh.uv2 = null;
        instMesh.uv3 = null;
        AssetDatabase.CreateAsset(instMesh, meshPath);

        rebulidMeshBindPose(curGameObject, sm, ref instMesh, boneIds);

        EditorUtility.SetDirty(instMesh);


        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();

    }

    /// 生成骨骼id
	private Dictionary<Transform, int> resortBone(GameObject target)
    {
        if (selectedSkinnedMeshRenderer == null)
        {
            Debug.LogError("Don't has SkinnedMeshRenderer component  " + target.name);
            return null;
        }

        Transform root = selectedSkinnedMeshRenderer.rootBone;
        if (root == null)
        {
            Debug.LogError("Don't has Root: " + target.name);
            return null;
        }

        Dictionary<Transform, int> mappedIdx = new Dictionary<Transform, int>();
        {
            // if (root != sm.rootBone)
            // {
            // 	Debug.LogError(sm.name + ":Root bone error:" + sm.rootBone.name);
            // 	continue;
            // }

            Transform[] smBones = selectedSkinnedMeshRenderer.bones;
            foreach (var b in smBones)
            {
                if (!mappedIdx.ContainsKey(b))
                {
                    mappedIdx.Add(b, mappedIdx.Count);
                }
            }
        }

        return mappedIdx;
    }

    // 将骨骼信息存入mesh顶点信息的uv1和uv2中 (uv1:骨骼id uv2:骨骼权重)
    private void rebulidMeshBindPose(GameObject obj, SkinnedMeshRenderer sm, ref Mesh targetMesh, Dictionary<Transform, int> inRemapBones)
    {
        // 初始化该节点renderer使用的骨骼列表
        /* unity中，对于一个mesh来说boneWeight.boneIndex对应的是当前节点SkinnedMeshRenderer的bones。它们都只是整个模型文件的部分骨骼，但它们boneIndex的顺序
		   和bones的顺序是相同的，所以这里会用remapIdx来记录当前节点使用到的骨骼id。 */
        Transform[] aBones = sm.bones;
        int numBones = aBones.Length;

        int[] remapIdx = new int[numBones];
        for (int i = 0; i < numBones; ++i)
            remapIdx[i] = i;

        if (inRemapBones != null && inRemapBones.Count > 0)
        {
            for (int i = 0; i < numBones; ++i)
            {
                if (!inRemapBones.ContainsKey(sm.bones[i]))
                {
                    Debug.LogError(targetMesh.name + ":在 mappedIdx 没有找到 Transform:" + sm.bones[i].name);
                    continue;
                }
                remapIdx[i] = inRemapBones[sm.bones[i]];
            }
        }

        boneIndicesList.Clear();
        boneWeightsList.Clear();
        Matrix4x4[] aBindPoses = targetMesh.bindposes;
        BoneWeight[] aBoneWeights = targetMesh.boneWeights;//boneIndex对应SkinnedMeshRenderer的Bones的顺序(这里只是部分骨骼)
        for (int i = 0; i < targetMesh.vertexCount; ++i)
        {
            Vector4 boneIndex = Vector4.zero;
            Vector4 boneWeight = Vector4.zero;
            BoneWeight bw = aBoneWeights[i];

            if (Mathf.Abs(bw.weight0) > 0.00001f)
            {
                boneIndex.x = remapIdx[bw.boneIndex0];
                boneWeight.x = bw.weight0;
            }
            else
            {
                Debug.LogError(targetMesh + " Idx:" + i + ": Bone 0 weight == 0.0f.");
                boneIndex.x = 0;
                boneWeight.x = 0.0f;
            }
            if (Mathf.Abs(bw.weight1) > 0.00001f)
            {
                boneIndex.y = remapIdx[bw.boneIndex1];
                boneWeight.y = bw.weight1;
            }
            else
            {
                boneIndex.y = 0;
                boneWeight.y = 0.0f;
            }
            if (Mathf.Abs(bw.weight2) > 0.00001f)
            {
                boneIndex.z = remapIdx[bw.boneIndex2];
                boneWeight.z = bw.weight2;
            }
            else
            {
                boneIndex.z = 0;
                boneWeight.z = 0.0f;
            }
            if (Mathf.Abs(bw.weight3) > 0.00001f)
            {
                boneIndex.w = remapIdx[bw.boneIndex3];
                boneWeight.w = bw.weight3;
            }
            else
            {
                boneIndex.w = 0;
                boneWeight.w = 0.0f;
            }
            boneIndicesList.Add(boneIndex);
            boneWeightsList.Add(boneWeight);


            float totalWeight = boneWeight.x + boneWeight.y + boneWeight.z + boneWeight.w;
            if (totalWeight - 1.0f > 0.00001f)
            {
                Debug.LogError("BoneIndex total more than 1.0f ...vertice id =" + i);
            }
            if (totalWeight - 1.0f < -0.00001f)
            {
                Debug.LogError("BoneIndex total less than 1.0f ...vertice id =" + i);
            }
        }
        targetMesh.SetUVs(1, boneIndicesList);
        targetMesh.SetUVs(2, boneWeightsList);

        // 记录原始网格的bindposes
        for (int bpIdx = 0; bpIdx < aBindPoses.Length; bpIdx++)
        {
            boneBindposes[remapIdx[bpIdx]] = aBindPoses[bpIdx];
        }
    }

    public GpuSkinningAnimData getAnimData()
    {
        return animData;
    }

    public static bool IslegalString(string str)
    {
        if (str.IndexOf(" ") > -1)
            return false;

        if (IsChina(str))
            return false;

        return true;
    }

    static bool IsChina(string CString)
    {
        bool BoolValue = false;
        for (int i = 0; i < CString.Length; i++)
        {
            if (Convert.ToInt32(Convert.ToChar(CString.Substring(i, 1))) > Convert.ToInt32(Convert.ToChar(128)))
            {
                BoolValue = true;
            }

        }
        return BoolValue;
    }

    static void Warning(string tile, string strLog)
    {
        Debug.LogError(strLog);

#if DISPLAY_DIALOG
		EditorUtility.DisplayDialog(tile, strLog , "OK");
#endif
    }

    bool CompareApproximately(float f0, float f1, float epsilon = 0.000001F)
    {
        float dist = (f0 - f1);
        dist = Mathf.Abs(dist);
        return dist < epsilon;
    }

    Quaternion ToQuaternion(Matrix4x4 mat)
    {
        float det = mat.determinant;
        if (!CompareApproximately(det, 1.0F, .005f))
            return Quaternion.identity;

        Quaternion quat = Quaternion.identity;
        float tr = mat.m00 + mat.m11 + mat.m22;

        // check the diagonal
        if (tr > 0.0f)
        {
            float fRoot = Mathf.Sqrt(tr + 1.0f);  // 2w
            quat.w = 0.5f * fRoot;
            fRoot = 0.5f / fRoot;  // 1/(4w)
            quat.x = (mat[2, 1] - mat[1, 2]) * fRoot;
            quat.y = (mat[0, 2] - mat[2, 0]) * fRoot;
            quat.z = (mat[1, 0] - mat[0, 1]) * fRoot;
        }
        else
        {
            // |w| <= 1/2
            int[] s_iNext = { 1, 2, 0 };
            int i = 0;
            if (mat.m11 > mat.m00)
                i = 1;
            if (mat.m22 > mat[i, i])
                i = 2;
            int j = s_iNext[i];
            int k = s_iNext[j];

            float fRoot = Mathf.Sqrt(mat[i, i] - mat[j, j] - mat[k, k] + 1.0f);
            if (fRoot < float.Epsilon)
                return Quaternion.identity;

            quat[i] = 0.5f * fRoot;
            fRoot = 0.5f / fRoot;
            quat.w = (mat[k, j] - mat[j, k]) * fRoot;
            quat[j] = (mat[j, i] + mat[i, j]) * fRoot;
            quat[k] = (mat[k, i] + mat[i, k]) * fRoot;
        }

        return QuaternionNormalize(quat);

    }

    public static Quaternion QuaternionNormalize(Quaternion quat)
    {
        float scale = new Vector4(quat.x, quat.y, quat.z, quat.w).magnitude;
        scale = 1.0f / scale;

        return new Quaternion(scale * quat.x, scale * quat.y, scale * quat.z, scale * quat.w);
    }



    public static byte[] Float32toFloat32Bytes(float srcValue)
    {
        byte[] bytes = BitConverter.GetBytes(srcValue);
        //return bytes;
        int integer = (int)srcValue;
        float floats = srcValue - integer;

        if (integer > 127)
        {
            EditorUtility.DisplayDialog("警告!!", "模型数据值大于127，超过Float16的范围", "OK");
            integer = 127;
        }
        if (integer < -127)
        {
            EditorUtility.DisplayDialog("警告!!", "模型数据值小于-127，超过Float16的范围", "OK");
            integer = -127;
        }

        // 1个符号位(+:1)，7个整数位，24个小数位
        const int INTEGER = 7;
        const int FLOAT = 24;
        int[] data = new int[32];
        int index = 0;

        // 符号 //1: 负  0:正
        if (srcValue > 0)
        {
            data[index++] = 0;
        }
        else
        {
            data[index++] = 1;
            floats = -(srcValue - integer);
            integer = -integer;
        }

        // 整数
        integer_rst.Clear();
        while (integer > 0)
        {
            integer_rst.Add(integer % 2);
            integer /= 2;
        }
        if (integer_rst.Count < INTEGER)
        {
            int length = INTEGER - integer_rst.Count;
            for (int i = 0; i < length; ++i)
            {
                data[index++] = 0;
            }
        }
        for (int i = 0; i < integer_rst.Count; ++i)
        {
            data[index++] = integer_rst[integer_rst.Count - 1 - i];
        }

        // 小数位
        int temp;
        for (int i = 0; i < FLOAT; ++i)
        {
            floats *= 2;
            temp = (int)floats;
            data[index++] = temp;
            floats -= temp;
        }

        byte[] result = new byte[4];
        temp = 0;
        for (int i = 0; i < INTEGER + 1; ++i)
        {
            temp += (int)Math.Pow(2, INTEGER - i) * data[i];
        }
        result[0] = (byte)temp;

        temp = 0;
        for (int i = 8; i < 16; ++i)
        {
            temp += (int)Math.Pow(2, 15 - i) * data[i];
        }
        result[1] = (byte)temp;

        temp = 0;
        for (int i = 16; i < 24; ++i)
        {
            temp += (int)Math.Pow(2, 23 - i) * data[i];
        }
        result[2] = (byte)temp;

        temp = 0;
        for (int i = 24; i < 32; ++i)
        {
            temp += (int)Math.Pow(2, 31 - i) * data[i];
        }
        result[3] = (byte)temp;
        return result;
        //|符号|   (高位)   整        数   (低位)      |     (高位)  小          数 (低位)              |
        //|  0   |  1  |  2  |  3  |  4  |  5  |  6  |  7  |  8  |  9  | 10  | 11 | 12  | 13  | 14  | 15  |

        //|        (高位)         小    数      (低位)   |      (高位)      小         数    (低位)       |
        //|  0   |  1  |  2  |  3  |  4  |  5  |  6  |  7  |  8 |  9  | 10  | 11  |  12 | 13  | 14  | 15  |
    }


    public static Color Float32Bytes2Color(byte[] data)
    {
        float r = data[0] / 255.0f;
        float g = data[1] / 255.0f;
        float b = data[2] / 255.0f;
        float a = data[3] / 255.0f;
        Color color = new Color(r, g, b, a);
        return color;
    }


    public static float Color2Float32(Color color)
    {
        float integers = color.r * 255;
        int integer = (int)integers;

        int flag = (integer / 128);//符号  <=128 flag为0 //1: 负  0:正

        integer = integer - flag * 128; // 整数部分
        float floats0 = color.g;
        float floats1 = color.b / (255.0f);
        float floats2 = color.a / (255.0f * 255.0f);

        float result = integer + floats0 + floats1 + floats2;
        result = result - 2 * flag * result;        //1: 负  0:正
        return result;
    }

    public static Vector4 ConvertColors2Halfs(Color color1, Color color2, Color color3, Color color4)
    {
        return new Vector4(
            Color2Float32(color1),
            Color2Float32(color2),
            Color2Float32(color3),
            Color2Float32(color4)
        );
    }

    /////////////////
    public static float convertFloat16BytesToHalf(int data1, int data2)
    {
        float f_data2 = data2;
        int flag = (data1 / 128);
        float result = data1 - flag * 128   // 整数部分
        + f_data2 / 256;    // 小数部分

        result = result - 2 * flag * result;        //1: 负  0:正

        return result;
    }

    public static Vector4 convertColors2Halfs(Color color1, Color color2)
    {
        return new Vector4(convertFloat16BytesToHalf(Mathf.FloorToInt(color1.r * 255f + 0.5f), Mathf.FloorToInt(color1.g * 255f + 0.5f)), convertFloat16BytesToHalf(Mathf.FloorToInt(color1.b * 255f + 0.5f), Mathf.FloorToInt(color1.a * 255f + 0.5f)), convertFloat16BytesToHalf(Mathf.FloorToInt(color2.r * 255f + 0.5f), Mathf.FloorToInt(color2.g * 255f + 0.5f)), convertFloat16BytesToHalf(Mathf.FloorToInt(color2.b * 255f + 0.5f), Mathf.FloorToInt(color2.a * 255f + 0.5f)));
    }
}
