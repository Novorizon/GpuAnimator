using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class GpuSkinningTools : EditorWindow
{
    // 默认原始资源目录
    public static readonly string DEFAULT_FBX_PATH = "Assets/Artworks/GpuRole/";
    // 默认新资源输出目录
    public static readonly string DEFAULT_OUTPUT_PATH = "Assets/Main/Prefabs/GpuRole/";

    // 默认Shader名称
    public static string DEFAULT_USE_SHADER_NAME = "Custom/GpuSkinningAnimation";
    public static readonly string DEFAULT_PBR_SHADER_NAME = "Custom/GpuSkinningAnimationPBR";
    public static readonly string DEFAULT_SHADER_NAME = "Custom/GpuSkinningAnimation";

    // 默认存储数据文件名称后缀
    static readonly string DEFAULT_SAVE_FILE_NAME = "_Data.bytes";

    // 默认网格文件名称后缀
    public static readonly string DEFAULT_SAVE_MESH_NAME = "_Mesh.asset";

    // 默认prefab文件名称后缀
    public static readonly string DEFAULT_SAVE_PREFAB_NAME = "_DynPre.prefab";

    // 默认material文件名称后缀
    public static readonly string DEFAULT_SAVE_MATERIAL_NAME = "_DynMat.mat";

    // 默认原始material文件名称后缀
    public static readonly string DEFAULT_SAVE_DEFAULT_MATERIAL_NAME = "_Default_DynMat.mat";

    // 默认主纹理名称
    static readonly string DEFAULT_MAIN_TEX_NAME_POSTFIX = ".png";

    static string parentFolder;

    // 生成类型
    static private GpuSkinningGenerator.GenerateType generateType = GpuSkinningGenerator.GenerateType.PBR;

    // 日志路径
    static string logPath = "";
    // 原始路径
    static string srcPath = "";
    // 文件输出路径
    static string savePath = "";
    // 数据文件名
    static string saveName = "";
    // prefab
    static string savePrefabName = "";
    // material
    static string saveMaterialName = "";

    static string mainTexPath = "";
    static string maskPath = "";
    private static List<AnimationClip> m_clipList = new List<AnimationClip>();
    private static List<Mesh> m_meshList = new List<Mesh>();
    private static List<Material> m_materialList = new List<Material>();
    private static List<GameObject> m_gameObjectList = new List<GameObject>();
    // 子模型列表
    static Dictionary<string, SkinnedMeshRenderer> skinnedMeshRenderersDict = new Dictionary<string, SkinnedMeshRenderer>();
    static string[] skinnedMeshRendererNames = new string[0];
    static int selectedSkinnedMeshRenderer = 0;



    static GpuSkinningGenerator generator = new GpuSkinningGenerator();

    //[MenuItem("Window/GpuSkinningTool")]
    private static void ShowWindow()
    {
        //parentFolder = Application.dataPath.Replace("Assets", "");
        //var window = GetWindow<GpuSkinningInstTools>();
        //window.minSize = new Vector2(600, 800);
        //window.titleContent = new GUIContent("GpuSkinningInstTools");
        //window.Show();
    }


    public class Errors
    {
        public string file;
        public List<string> formats = new List<string>();
        public Errors(string f, List<string> list)
        {
            file = f;
            formats = list;
        }
    }

    static public List<Errors> errors = new List<Errors>();
    static List<DirectoryInfo> directoryInfos = new List<DirectoryInfo>();

    static public bool CheckFiles(DirectoryInfo dir, GameObject selectedFbx)
    {
        bool hasMesh = false;
        bool hasAnim = false;

        Dictionary<string, bool> needs = new Dictionary<string, bool>();
        DirectoryInfo[] res = dir.GetDirectories("res");
        FileInfo[] fileInfos = dir.GetFiles();
        string name = dir.Name.ToLower();

        needs.Add(name + ".fbx", false);
        foreach (var file in fileInfos)
        {
            string f = file.Name.ToLower();
            if (needs.ContainsKey(f))
            {
                needs[f] = true;
            }
        }

        if (selectedFbx)
        {
            SkinnedMeshRenderer[] skinnedMeshRenderers = selectedFbx.GetComponentsInChildren<SkinnedMeshRenderer>();
            for (int i = 0; i < skinnedMeshRenderers.Length; ++i)
            {
                if (skinnedMeshRenderers[i].name.Contains(dir.Name))
                {
                    hasMesh = true;
                    break;
                }
            }
        }

        needs.Add(name + ".png", false);
        needs.Add(name + ".mat", false);
        if (res.Length > 0 && res[0].Name == "res")
        {
            fileInfos = res[0].GetFiles();
            foreach (var file in fileInfos)
            {
                string f = file.Name.ToLower();
                if (needs.ContainsKey(f))
                {
                    needs[f] = true;
                }
                if (file.Extension == ".anim")
                {
                    hasAnim = true;
                }
            }
        }
        List<string> files = new List<string>();
        foreach (var need in needs)
        {
            if (need.Value == false)
            {
                files.Add(need.Key);
            }
            if (!hasMesh)
            {
                files.Add(name + ".mesh");
            }
            if (!hasAnim)
            {
                files.Add("animation");
            }
        }
        if (files.Count > 0 || !hasAnim || !hasMesh)
        {
            Errors error = new Errors(dir.Name, files);
            errors.Add(error);
            return false;
        }
        else
        {
            return true;
        }

    }


    static public void Log()
    {
        logPath = "Assets/Resources/GPUSkinningLog.txt";
        StringBuilder sb = new StringBuilder();
        foreach (var error in errors)
        {
            sb.Append(error.file).Append("\n");
            error.formats.ForEach(format =>
            sb.Append(format).Append("\n")
            );
            sb.Append("\n\n");
        }
        StreamWriter streamWriter = new StreamWriter(logPath);
        streamWriter.Write(sb.ToString());
        streamWriter.Flush();
        streamWriter.Close();
        //File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
    }


    [MenuItem("GpuSkinning/No PBR")]
    static private void GPUSkinning()
    {
        //GpuSkinningUtility.CreatFileHighPrecision();
        DEFAULT_USE_SHADER_NAME = DEFAULT_SHADER_NAME;
        generateType = GpuSkinningGenerator.GenerateType.None;
        GenGPUSkinning();
    }

    [MenuItem("GpuSkinning/PBR")]
    static private void GenGPUSkinningPBR()
    {
        //GpuSkinningUtility.CreatFilePBR();
        DEFAULT_USE_SHADER_NAME = DEFAULT_PBR_SHADER_NAME;
        generateType = GpuSkinningGenerator.GenerateType.PBR;
        GenGPUSkinning();
    }


    //[MenuItem("GpuSkinning/GpuSkinningTool")]
    static private void GenGPUSkinning()
    {
        count = 0;
        parentFolder = Application.dataPath.Replace("Assets", "");

        errors.Clear();
        directoryInfos.Clear();

        DirectoryInfo dirInfo = new DirectoryInfo(DEFAULT_FBX_PATH);
        DirectoryInfo[] dirs = dirInfo.GetDirectories();
        foreach (var dir in dirs)
        {
            //string fbxPath = dir.FullName.Substring(dir.FullName.IndexOf(DEFAULT_FBX_PATH) + DEFAULT_FBX_PATH.Length) + "/" + dir.Name;
            //GameObject fbx = Resources.Load(fbxPath) as GameObject;
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(DEFAULT_FBX_PATH + dir.Name + "/" + dir.Name + ".fbx");

            bool isEnough = CheckFiles(dir, fbx);
            if (!isEnough)
            {
                //Debug.Log("Lack of " + dir.Name);
                continue;
            }


            GeResources(dir);
            //GameObject fbxObject = Instantiate(fbx); ;
            Generate(fbx);

        }
        EditorUtility.DisplayDialog("提示", "生成成功 " + count + "个", "OK");
        //Log();

    }


    static int count = 0;
    static private void Generate(GameObject selectedFbx)
    {
        if (selectedFbx)
        {
            count++;
            //Debug.Log(count);
            selectedSkinnedMeshRenderer = 0;
            srcPath = AssetDatabase.GetAssetPath(selectedFbx);
            srcPath = srcPath.Substring(0, srcPath.LastIndexOf("/") + 1);
            //savePath = srcPath + "Output/";
            savePath = DEFAULT_OUTPUT_PATH + selectedFbx.name + "/";
            saveName = selectedFbx.name + DEFAULT_SAVE_FILE_NAME;
            // mesh list
            skinnedMeshRenderersDict.Clear();
            SkinnedMeshRenderer[] skinnedMeshRenderers = selectedFbx.GetComponentsInChildren<SkinnedMeshRenderer>();
            skinnedMeshRendererNames = new string[skinnedMeshRenderers.Length];
            for (int i = 0; i < skinnedMeshRenderers.Length; ++i)
            {
                skinnedMeshRendererNames[i] = skinnedMeshRenderers[i].name;
                if (skinnedMeshRenderers[i].name.Contains(selectedFbx.name))
                //if(skinnedMeshRenderers[i].name.Contains(selectedFbx.name)|| skinnedMeshRenderers[i].name.Contains("Body"))
                {
                    selectedSkinnedMeshRenderer = i;
                    skinnedMeshRenderersDict.Add(skinnedMeshRendererNames[i], skinnedMeshRenderers[i]);

                }
            }

            savePrefabName = selectedFbx.name + DEFAULT_SAVE_PREFAB_NAME;
            saveMaterialName = selectedFbx.name + DEFAULT_SAVE_MATERIAL_NAME;

            mainTexPath = srcPath + "res/" + selectedFbx.name + DEFAULT_MAIN_TEX_NAME_POSTFIX;
            maskPath = srcPath + "res/" + selectedFbx.name + "_mask" + DEFAULT_MAIN_TEX_NAME_POSTFIX;

            //mainTexPath = srcPath +  selectedFbx.name + DEFAULT_MAIN_TEX_NAME_POSTFIX;


            //GetParts(selectedFbx);

            refreshPanel(selectedFbx);
            if (Directory.Exists(savePath))
                Directory.Delete(savePath, true);
            Directory.CreateDirectory(savePath);

            //原始材质
            string mainTex = savePath + selectedFbx.name + DEFAULT_MAIN_TEX_NAME_POSTFIX;
            if (File.Exists(mainTexPath))
            {
                File.Copy(mainTexPath, mainTex, true);
            }
            string maskTex = savePath + selectedFbx.name + "_mask" + DEFAULT_MAIN_TEX_NAME_POSTFIX;
            if (File.Exists(maskPath))
            {
                File.Copy(maskPath, maskTex, true);
            }

            // 骨骼动画
            generator.generate(parentFolder, savePath, saveName, saveMaterialName, savePrefabName, mainTex, generateType, maskTex);
        }
    }


    private static void GetParts(GameObject obj)
    {
        string assetPath = AssetDatabase.GetAssetPath(obj);
        UnityEngine.Object[] objs = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        m_meshList.Clear();
        m_clipList.Clear();
        m_materialList.Clear();
        m_gameObjectList.Clear();
        for (int i = 0; i < objs.Length; i++)
        {
            if (objs[i].hideFlags == (HideFlags.HideInHierarchy | HideFlags.NotEditable))
                continue;

            if (objs[i] is Mesh)
            {
                m_meshList.Add(objs[i] as Mesh);
            }
            else if (objs[i] is Material)
            {
                m_materialList.Add(objs[i] as Material);
            }
            else if (objs[i] is AnimationClip)
            {
                m_clipList.Add(objs[i] as AnimationClip);
            }
            else if (objs[i] is GameObject)
            {
                m_gameObjectList.Add(objs[i] as GameObject);
            }
        }
    }


    private static void GetAnimClips(GameObject obj)
    {
        string assetPath = AssetDatabase.GetAssetPath(obj);
        UnityEngine.Object[] objs = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        m_clipList.Clear();
        for (int i = 0; i < objs.Length; i++)
        {
            if (objs[i] is AnimationClip)
            {
                if (objs[i].hideFlags == (HideFlags.HideInHierarchy | HideFlags.NotEditable))
                    continue;

                //if (!IslegalString(objs[i].name))
                //{
                //    string strLog = string.Format("AnimationCLip  命名错误: {0} -> {1}", assetPath, objs[i].name);
                //    Debug.Log("命名错误", strLog);
                //}

                m_clipList.Add(objs[i] as AnimationClip);
            }
        }
    }

    private static void GeResources(DirectoryInfo dir)
    {
        m_clipList.Clear();
        string animPath = DEFAULT_FBX_PATH + dir.Name + "/res/";
        DirectoryInfo[] res = dir.GetDirectories("res");
        FileInfo[] fileInfos = dir.GetFiles();
        if (res.Length > 0 && res[0].Name == "res")
        {
            FileInfo[] fileres = res[0].GetFiles();
            foreach (var file in fileres)
            {
                if (file.Extension.Equals(".anim"))
                {
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath + file.Name);
                    if (clip.hideFlags == (HideFlags.HideInHierarchy | HideFlags.NotEditable))
                        continue;
                    m_clipList.Add(clip);
                    //Debug.Log(clip.name);
                }
                else if (file.Extension.Equals(".mat"))
                {
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(animPath + file.Name);
                    if (mat.hideFlags == (HideFlags.HideInHierarchy | HideFlags.NotEditable))
                        continue;
                    m_materialList.Add(mat);
                    //Debug.Log(mat.name);
                }
                else if (file.Name.Equals(dir.Name + ".png"))//mainTex
                {
                    //Material mat = AssetDatabase.LoadAssetAtPath<Material>(animPath + file.Name);
                    //if (mat.hideFlags == (HideFlags.HideInHierarchy | HideFlags.NotEditable))
                    //	continue;
                    //m_materialList.Add(mat);
                    //Debug.Log(mat.name);
                }
            }
        }
    }
    static private void refreshPanel(GameObject selectedFbx)
    {
        //GetAnimClips(selectedFbx);
        generator.setSelectedModel(selectedFbx, generateType, m_clipList, skinnedMeshRenderersDict[skinnedMeshRendererNames[selectedSkinnedMeshRenderer]], m_meshList);
    }


    static public void AnimClipState(AnimationClip clip, GpuSkinningAnimClip boneAnimation)
    {
        string path = "Assets/Resources/animClipState.txt";
        StringBuilder sb = new StringBuilder(128);
        sb.Append(clip.name).Append("\n");
        sb.Append(clip.frameRate).Append("\n");
        sb.Append(clip.length).Append("\n");
        sb.Append(clip.isLooping).Append("\n");
        sb.Append(boneAnimation.startFrame).Append("\n");
        sb.Append(boneAnimation.endFrame).Append("\n");
        sb.Append("\n\n");

        using (StreamWriter sw = File.AppendText(path))
        {
            sw.WriteLine(sb.ToString());
            sw.Flush();
            sw.Close();
        }
        //StreamWriter streamWriter = new StreamWriter(path);
        //streamWriter.Write(sb.ToString());
        //streamWriter.Flush();
        //streamWriter.Close();
        //File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
    }

}
