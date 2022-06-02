using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Threading;
using System;
using System.IO;
using System.Text;

public class CI_Window : EditorWindow
{
    CI_Data data { get { return CI_Data.Instance; } }


    string vsNum;

    bool addressFold;

    private void OnGUI()
    {
        this.Repaint();
        CI_Data.Update();

        #region IP地址端口和Tag
        addressFold = EditorGUILayout.Foldout(addressFold, "Address", true);
        if(addressFold)
        {
            EditorGUILayout.LabelField("   IP:", data.ip);
            GUILayout.BeginHorizontal();
            data.port = EditorGUILayout.TextField("   Port:", data.port);
            if (GUILayout.Button("修改监听端口"))
            {
                data.ip = CI_Listener.GetIP();
                data.LoadConfig();
            }
            GUILayout.EndHorizontal();
        }

        GUIStyle sty = new GUIStyle();
        sty.fontSize = 24;
        sty.normal.textColor = Color.white;
        sty.contentOffset = new Vector2(17, -4);
        EditorGUILayout.LabelField(data.tag, sty);
        GUILayout.Space(10);
        #endregion

        EditorGUILayout.LabelField("宏", PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup));
        
        vsNum = EditorGUILayout.TextField("Version", vsNum);

        GUILayout.Space(8);

        ShowBool(CI_Step.SvnUpdate, "更新SVN：");
        ShowBool(CI_Step.TableTojson, "TableTojson：");

        GUILayout.BeginHorizontal();
        ShowBool(CI_Step.CompileHotFix, "CompileHotFix：");
        ShowBool(CI_Step.GenerateCLRBindingCode, "GenerateCLRBindingCode：");
        GUILayout.EndHorizontal();

        data[CI_Step.CreateAssetBundle] = (int)(CI_ABType)EditorGUILayout.EnumPopup("资源打包", (CI_ABType)data[CI_Step.CreateAssetBundle]);

        //data.vsType = EditorGUILayout.Popup("版本配置：", data.vsType, GetVSList().ToArray());

#if !UNITY_IOS
        data[CI_Step.UpdateDll] = (int)(DllUpdateType)EditorGUILayout.EnumPopup("代码：", (DllUpdateType)data[CI_Step.UpdateDll]);
#endif

        ShowBool(CI_Step.SvnCommit, "提交SVN：");

        GUILayout.Space(24);

        if (GUILayout.Button("开始打包"))
        {
            data.BeginAotuBuild();
        }
    }

    public void ShowBool(CI_Step step, string txt)
    {
        data[step] = EditorGUILayout.Toggle(txt, data[step] == 1) ? 1 : 0;
    }


    [MenuItem("Tools/Misc/自动打包")]
    static void OpenWindow()
    {
        if (Window != null)
            Window.Focus();
        else
            Window.Show();
    }

    void OnEnable()
    {
        CI_Data.Start();
    }

    void OnDisable()
    {
        CI_Data.OnDisable();
    }

    void OnDestroy()
    {
        CI_Data.Stop();
    }

    static CI_Window _window;
    public static CI_Window Window
    {
        get
        {
            if (Application.isBatchMode) return null;
            if (_window == null)
                _window = (CI_Window)GetWindow(typeof(CI_Window), false, "自动打包", true);

            _window.minSize = new Vector2(100, 100);

            _window.data.ip = CI_Listener.GetIP();

            _window.data.LoadWindowLayout();

            return _window;
        }
    }
}