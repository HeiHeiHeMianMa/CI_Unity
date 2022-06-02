using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


/*
 * 
 * 打包机部署jenkins
 * 参数 Address 填写打包机ip 目标工程端口
 * 
 * jenkins调起python
 * 第一个参数是address 第二个参数是打包参数的拼接
 * ptyhon对目标地址post  内容是打包参数拼接 返回log地址
 * 对log内容持续检查 打印
 * 检查到目标log内容 结束检查log
 * 推送企业微信打包结束消息
 * 
 * TODO 
 * 
 * //唯一关键字
 * //phython打包windows mac //log远程后只需要一台jenkins 只需要打包windows就可以
 * 完成企业微信@对应人
 * //完善SVN操作
 * 版本号操作
 * //log通过web发送给python 可以实现只在一台机器部署jenkins
 * 
*/

public enum CI_Step
{
    SvnUpdate,                  //SVN更新
    TableTojson,                //转表
    CompileHotFix,              //编译热更代码
    GenerateCLRBindingCode,     //热更转接口
    TranslateUpdateUIAssets,    //翻译UI
    CreateAssetBundle,         //打包
    setVersion,                 //版本配置
    UpdateDll,                  //更新Dll
    SvnCommit,                  //SVN提交

}

public class CI_Data : ScriptableObject
{
    readonly static string DataAssetPath = "Assets/CI_Data.asset";
    readonly static string JenkinsIPPath = "/Editor/CI/Config/CI_JenkinsIP.txt";
    readonly static string ConfigPathFormatUNC = "\\\\{0}\\CI\\CI_Config.json";
    readonly static string WindowLayoutPath = "Assets/Editor/CI/Config/CI.wlt";

    readonly static string FinishLog = "自动打包完成";
    readonly static string TAGWarning = "输入正确的端口号";

    static CI_Data _instance;
    public static CI_Data Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = (CI_Data)AssetDatabase.LoadMainAssetAtPath(DataAssetPath);
                if (_instance == null)
                {
                    _instance = CreateInstance<CI_Data>();
                    AssetDatabase.CreateAsset(_instance, DataAssetPath);
                }
            }
            return _instance;
        }
    }

    public bool autoBuilding;
    public static bool AutoBuilding
    {
        get
        {
            if (!ExistsData())
                return false;
            return Instance.autoBuilding;
        }
        set
        {
            Instance.autoBuilding = value;
            Instance.Save();
        }
    }
    public bool IsWaitCompile { get { return isWaitCompile; } set { isWaitCompile = value; Save(); } }

    //步骤队列
    public List<CI_Step> stepQueue = new List<CI_Step>();

    public string ip;
    public string port;
    public string tag = TAGWarning;
    public string logPath;

    [SerializeField]
    private bool isWaitCompile;

    public CI_Step Peek
    {
        get { return stepQueue[0]; }
    }

    public int this[CI_Step step]
    {
        get
        {
            string key = port + step.ToString();
            if (PlayerPrefs.HasKey(key))
            {
                return PlayerPrefs.GetInt(key);
            }
            return 0;
        }
        set
        {
            string key = port + step.ToString();
            PlayerPrefs.SetInt(key, value);
        }
    }

    public void BeginAotuBuild()
    {
        LoadWindowLayout();

        Log(LogType.nor, "-");
        Log(LogType.nor, "宏 " + PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup));
        Log(LogType.nor, "Version " + PlayerSettings.bundleVersion);

        AutoBuilding = true;
        InitStep();
        Execute();
    }

    public static void Update()
    {
        if (!string.IsNullOrEmpty(CI_Listener.Context))
        {
            Instance.logPath = CI_Listener.LogPath;
            CI_Log.Start(Instance.logPath);

            Instance.AnalysisStep(CI_Listener.Context);
            Instance.BeginAotuBuild();

            CI_Listener.LogPath = null;
            CI_Listener.Context = null;
        }
    }

    public void AnalysisStep(string str)
    {
        var steps = str.Split('=')[1].Split(',');
        this[CI_Step.SvnUpdate] = bool.Parse(steps[0]) ? 1 : 0;
        this[CI_Step.TableTojson] = bool.Parse(steps[1]) ? 1 : 0;
        this[CI_Step.CompileHotFix] = bool.Parse(steps[2]) ? 1 : 0;
        this[CI_Step.GenerateCLRBindingCode] = bool.Parse(steps[3]) ? 1 : 0;
        this[CI_Step.CreateAssetBundle] = (int)Enum.Parse(typeof(CI_ABType), steps[4]);
        this[CI_Step.UpdateDll] = (int)Enum.Parse(typeof(DllUpdateType), steps[5]);
        this[CI_Step.SvnCommit] = bool.Parse(steps[6]) ? 1 : 0;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("收到的字符串  解析后的值");
        sb.AppendLine("{0} ， {1} : {2}".EFormat(steps[0], this[CI_Step.SvnUpdate], CI_Step.SvnUpdate));
        sb.AppendLine("{0} ， {1} : {2}".EFormat(steps[1], this[CI_Step.TableTojson], CI_Step.TableTojson));
        sb.AppendLine("{0} ， {1} : {2}".EFormat(steps[2], this[CI_Step.CompileHotFix], CI_Step.CompileHotFix));
        sb.AppendLine("{0} ， {1} : {2}".EFormat(steps[3], this[CI_Step.GenerateCLRBindingCode], CI_Step.GenerateCLRBindingCode));
        sb.AppendLine("{0} ， {1} : {2}".EFormat(steps[4], this[CI_Step.CreateAssetBundle], CI_Step.CreateAssetBundle));
        sb.AppendLine("{0} ， {1} : {2}".EFormat(steps[5], this[CI_Step.UpdateDll], CI_Step.UpdateDll));
        sb.AppendLine("{0} ， {1} : {2}".EFormat(steps[6], this[CI_Step.SvnCommit], CI_Step.SvnCommit));

        Debug.Log(sb.ToString());
    }

    //添加步骤
    public void InitStep()
    {
        stepQueue.Clear();

        foreach (var item in Enum.GetValues(typeof(CI_Step)))
        {
            var step = (CI_Step)item;
            if (this[step] != 0)
            {
                AddStep(step);
            }
        }

        Save();
    }

    public void AddStep(CI_Step doneEvent)
    {
        for (int i = 0; i < stepQueue.Count; i++)
        {
            if (doneEvent == stepQueue[i])
                Log(LogType.error, "!!! 有相同的步骤索引");
        }

        stepQueue.Add(doneEvent);

        stepQueue.Sort();
    }
    //移除队底
    public void RemovePeek()
    {
        this[Peek] = 0;
        stepQueue.Remove(Peek);

        Save();
    }

    //执行
    public void Execute()
    {
        if (!AutoBuilding)
            return;

        //是否需要等待编译
        if (EditorApplication.isCompiling)
        {
            IsWaitCompile = true;
            Log(LogType.nor, "等待编译");
            return;
        }

        Log(LogType.main, "开始执行 {0}".EFormat(Peek));

        ExecuteStep(Peek);
    }

    //步骤完成
    public static void SetpDone(CI_Step step)
    {
        if (!ExistsData())
            return;

        Instance.SetpEventDone(step);
    }
    //步骤完成实际逻辑处
    void SetpEventDone(CI_Step step)
    {
        if (!AutoBuilding)
            return;

        if (step != Peek)
        {
            Log(LogType.error, string.Format("错误： 队底 {0} 和返回完成 {1} 不一致！！！", Peek, step));
            return;
        }

        Log(LogType.main, "完成 {0}".EFormat(step));

        //移除已完成项
        RemovePeek();

        //检查队列
        if (stepQueue.Count == 0)
        {
            AutoBuilding = false;
            Log(LogType.main, FinishLog); //完成
            CI_Log.Stop();
            return;
        }

        Execute();

    }

    //[InitializeOnLoad]
    //[UnityEditor.Callbacks.DidReloadScripts(999)]
    [InitializeOnLoadMethod]
    static void ReloadScripts()
    {
        if (!ExistsData() || !AutoBuilding)
            return;

        if (Instance.IsWaitCompile)
        {
            Log(LogType.nor, "编译完成，继续自动打包");
            Instance.IsWaitCompile = false;

            EditorApplication.delayCall = delegate ()
            {
                Instance.Execute();
            };
        }
    }

    public static void Start()
    {
        CI_Log.Start(Instance.logPath);
        CI_Listener.Start();
    }
    public static void OnDisable()
    {
        CI_Log.Stop();
        CI_Listener.Stop();
    }

    public static void Stop()
    {
        CI_Log.Stop();
        CI_Listener.Stop();
    }

    void ExecuteStep(CI_Step step)
    {
#if TEST
        switch (step)
        {
            case CI_Step.SvnUpdate:
                CI_SVN.Update("/TestObject/");
                SetpDone(step);
                break;
            case CI_Step.SvnCommit:
                //string cachePath = "{0}/../bin/{1}/cache/".EFormat(Application.dataPath, EditorUserBuildSettings.activeBuildTarget.ToString());
                CI_SVN.Commit("/TestObject/", "测试测试");
                SetpDone(step);
                break;
            case CI_Step.TableTojson:
            case CI_Step.CompileHotFix:
                var tex = File.ReadAllText(Application.dataPath + "/NewBehaviourScri1.cs");
                tex = tex.Replace("Update", "UpdateA");
                File.WriteAllText(Application.dataPath + "/NewBehaviourScri1.cs", tex);
                AssetDatabase.Refresh();
                SetpDone(step);
                break;
            case CI_Step.CreateAssetBundle:
                string targetPath = Application.streamingAssetsPath + "/";
                if (Directory.Exists(targetPath) == false) Directory.CreateDirectory(targetPath);

                BuildPipeline.BuildAssetBundles(targetPath, BuildAssetBundleOptions.UncompressedAssetBundle, BuildTarget.Android);
                Log(LogType.nor, "打包完成");
                AssetDatabase.Refresh();
                SetpDone(step);
                break;
            case CI_Step.GenerateCLRBindingCode:
            case CI_Step.setVersion:
            case CI_Step.UpdateDll:
            case CI_Step.TranslateUpdateUIAssets:
                EditorApplication.delayCall = delegate ()
                {
                    SetpDone(step);
                };
                break;
            default:
                Log(LogType.error, "步骤类型错误： " + step);
                return;
        }

#else
        MethodInfo method = null;
        switch (step)
        {
            case CI_Step.SvnUpdate:
                CI_SVN.Update();
                SetpDone(step);
                return;
            case CI_Step.SvnCommit:
                string cachePath = "{0}/../bin/{1}/cache/".EFormat(Application.dataPath, EditorUserBuildSettings.activeBuildTarget.ToString());
                CI_SVN.Commit("cachePath", "自动打包提交");
                SetpDone(step);
                return;
            case CI_Step.TableTojson:
                method = typeof(TableTool).GetMethod("TableToJsonNew", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { }, null);
                break;
            case CI_Step.CompileHotFix:
                method = typeof(ILRTools).GetMethod("CompileHotFixProject", BindingFlags.NonPublic | BindingFlags.Static);
                break;
            case CI_Step.GenerateCLRBindingCode:
                method = typeof(ILRuntimeCLRBinding).GetMethod("GenerateCLRBindingByAnalysis", BindingFlags.NonPublic | BindingFlags.Static);
                break;
            case CI_Step.CreateAssetBundle:
                string methodName = string.Empty;
                switch ((CI_ABType)this[step])
                {
#if UNITY_ANDROID
                    case CI_ABType.Android:
                        methodName = "Android";
                        break;
                    case CI_ABType.Android_TableAndCode:
                        methodName = "AndroidJsCfg";
                        break;
#endif
#if UNITY_IOS
                case CI_ABType.IOS:
                    methodName = "iOS";
                    break;
                case CI_ABType.IOS_TableAndCode:
                    methodName = "iOSJsCfg";
                    break;
#endif
#if UNITY_STANDALONE_WIN
                case CI_ABType.Win:
                    methodName = "Windows";
                    break;
                case CI_ABType.Win_TableAndCode:
                    methodName = "WindowsJsCfg";
                    break;
#endif
                    default:
                        Log(LogType.error, "资源打包类型指定错误： " + (CI_ABType)this[step]);
                        return;
                }
                method = typeof(Mihua.Assets.Editor.AssetBundleNew.ABAssetsCtrl).GetMethod(methodName);

                Log(LogType.nor, string.Format("资源打包类型： {0}", (CI_ABType)this[step]));
                break;
            case CI_Step.setVersion:
                break;
            case CI_Step.UpdateDll:
                switch ((DllUpdateType)this[step])
                {
                    case DllUpdateType.dll更新:
                        method = typeof(Mihua.Assets.Editor.BuildTools.BuildTool).GetMethod("EditorDllVSNum", BindingFlags.Public | BindingFlags.Static);
                        break;
                    case DllUpdateType.随包代码更新:
                        method = typeof(Mihua.Assets.Editor.BuildTools.BuildTool).GetMethod("EditorDllVSNum1", BindingFlags.Public | BindingFlags.Static);
                        break;
                    default:
                        Log(LogType.error, "更新dll类型指定错误： " + (DllUpdateType)this[step]);
                        return;
                }

                Log(LogType.nor, string.Format("dll更新类型： {0}", (DllUpdateType)this[step]));
                break;
            //case AutoBuildStep.TranslateUpdateUIAssets:
            //    Log(LogType.nor, string.Format("重新生成图集和字体 语言：{0}", TabelSeting.GetSetingAsset().curLanguage));
            //    method = typeof(LanguageUIMgr).GetMethod("UpdateUIAssetsAndUpdateUIAsst", BindingFlags.NonPublic | BindingFlags.Static);
            //    break;
            default:
                Log(LogType.error, "步骤类型错误： " + step);
                return;
        }
        var ret = method.Invoke(null, null);
        switch (step)
        {
            case CI_Step.TableTojson:
                if ((bool)ret)
                {
                    SetpDone(CI_Step.TableTojson);
                }
                break;
            default:
                break;
        }
#endif
    }

    public void LoadConfig()
    {
        string address = "http://{0}:{1}/".EFormat(ip, port);
        var ipStr = File.ReadAllText(Application.dataPath + JenkinsIPPath, Encoding.UTF8);
        var str = File.ReadAllText(ConfigPathFormatUNC.EFormat(ipStr), Encoding.UTF8);
        var arr = JsonConvert.DeserializeObject<JArray>(str);
        tag = TAGWarning;
        foreach (var item in arr)
        {
            var foo = item["address"].ToString();
            if (foo == address)
            {
                tag = item["tag"].ToString();
            }
        }
        CI_Listener.Start();
        Save();
    }

    public void LoadWindowLayout()
    {
        EditorUtility.LoadWindowLayout(WindowLayoutPath);
    }

    static bool ExistsData() { return AssetDatabase.LoadMainAssetAtPath(DataAssetPath) != null; }

    public void Save()
    {
        if (_instance != null)
        {
            EditorUtility.SetDirty(_instance);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    #region     Log处理
    public static void Log(LogType type, string str)
    {
        var dateStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        switch (type)
        {
            case LogType.nor:
            case LogType.main:
                Debug.Log(string.Format("--------- {0} {1}", dateStr, str));
                break;
            case LogType.error:
                AutoBuilding = false;
                Debug.LogError(string.Format("{0} {1}", dateStr, str));
                break;
            default:
                break;
        }
    }

    public enum LogType
    {
        nor,
        main,
        error
    }
    #endregion
}

public enum CI_ABType
{
    None,
#if UNITY_ANDROID
    Android,
    Android_TableAndCode,
#endif
#if UNITY_IOS
IOS,
IOS_TableAndCode,
#endif
#if UNITY_STANDALONE_WIN
Win,
Win_TableAndCode,
#endif
}

public enum DllUpdateType
{
    None,
    dll更新,
    随包代码更新
}
