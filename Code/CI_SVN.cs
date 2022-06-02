using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Diagnostics;
using System.Text;
using Debug = UnityEngine.Debug;

public class CI_SVN
{
    public static bool Update(string path = "",int targetVersion = 1, bool userTheir = true)
    {
        path = Application.dataPath + path;
        Debug.Log("Svn更新:" + path);

        var foo = string.Format("update {0} {1}",
            userTheir ? "--accept theirs-full" : "--accept mine-full",
            targetVersion > 1 ? "-r " + targetVersion : ""
            );

        var pStr = CMD(foo, path);
        var output = pStr[0];
        var error = pStr[1];

        Debug.Log("Svn更新内容:" + output.ToString());

        SvnResolve(path);

        return string.IsNullOrEmpty(error);
    }

    public static bool Commit(string path, string log)
    {
        path = Application.dataPath + path;

        var pStr = CMD("status" + " \"{0}\"".EFormat(path));
        var output = pStr[0];
        var error = pStr[1];

        string[] opList = output.Split(new string[] { "\r\n" }, StringSplitOptions.None);
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < opList.Length; i++)
        {
            if (opList[i].Length < 8) continue;
            if (opList[i].Substring(0, 8).Contains("?"))
            {
                pStr = CMD("add \"{0}\" --no-ignore --force".EFormat(opList[i].Substring(8)), "");
                error += pStr[1];
                sb.AppendLine("修改: " + opList[i]);
            }
            else
            if (opList[i].Substring(0, 8).Contains("!"))
            {
                pStr = CMD("delete \"{0}\"".EFormat(opList[i].Substring(8)), "");
                error += pStr[1];
                sb.AppendLine("删除: " + opList[i]);
            }
        }
        Debug.Log("Svn提交:" + sb.ToString());

        pStr = CMD("commit \"{0}\" -m \"{1}\"".EFormat(path, log), "");
        output = pStr[0];
        error += pStr[1];
        Debug.Log("Svn提交内容:" + output.ToString());

        SvnResolve(path);

        return string.IsNullOrEmpty(error);
    }

    public static int ReadLastChangeRevision(string path = "")
    {
        var pStr = CMD("info " + ReadUrl(path), path);
        var output = pStr[0];
        var error = pStr[1];

        var startIndex = output.IndexOf("Last Changed Rev: ");
        startIndex = startIndex + "Last Changed Rev: ".Length;
        var endIndex = output.IndexOf("\r\n", startIndex);
        var vs_str = output.Substring(startIndex, endIndex - startIndex);
        int vs = 0;
        int.TryParse(vs_str, out vs);
        Debug.Log("Svn最新版本:" + vs);
        return vs;
    }

    static string ReadUrl(string path)
    {
        var pStr = CMD("info", path);
        var output = pStr[0];
        var error = pStr[1];

        var startIndex = output.IndexOf("URL: ");
        startIndex = startIndex + "URL: ".Length;
        var endIndex = output.IndexOf("\r\n", startIndex);
        var url_str = output.Substring(startIndex, endIndex - startIndex);
        //Debug.Log("Svn服务器地址:" + url_str);
        return url_str;
    }

    static void SvnResolve(string path)
    {
        var pStr = CMD("status", path);
        var output = pStr[0];
        var error = pStr[1];

        string[] retList = output.Split(new string[] { "\r\n" }, StringSplitOptions.None);
        List<string> conflictList = new List<string>();
        List<string> fileList = new List<string>();
        for (int i = 0; i < retList.Length; i++)
        {
            if (retList[i].Length < 8) continue;
            if (retList[i].Substring(0, 8).Contains("C"))
            {
                conflictList.Add(retList[i].Substring(0, 8));
                fileList.Add(retList[i].Substring(8));
            }
        }

        for (int i = 0; i < fileList.Count; i++)
        {
            CMD("resolve --accept theirs-full" + " \"{0}\"".EFormat(fileList[i]), "");
        }
        for (int i = 0; i < fileList.Count; i++)
        {
            CMD("revert" + " \"{0}\"".EFormat(fileList[i]), "");
        }
    }

    public static string[] CMD(string args, string workdir = null)
    {
        var pStartInfo = new ProcessStartInfo("svn");
        pStartInfo.Arguments = args;
        pStartInfo.CreateNoWindow = false;
        pStartInfo.UseShellExecute = false;
        pStartInfo.WorkingDirectory = workdir;
        pStartInfo.RedirectStandardError = true;
        pStartInfo.RedirectStandardInput = true;
        pStartInfo.RedirectStandardOutput = true;
        pStartInfo.StandardErrorEncoding = Encoding.UTF8;
        pStartInfo.StandardOutputEncoding = Encoding.UTF8;

        var p = Process.Start(pStartInfo);
        var output = p.StandardOutput.ReadToEnd(); 
        var error = p.StandardError.ReadToEnd(); 
        p.Close();
        if (!string.IsNullOrEmpty(error))
        {
            Debug.Log(args + " Output: " + output);
            Debug.LogError(args + " Error: " + error);
        }
        return new string[] { output, error };
    }

    #region 测试
    //[MenuItem("Tools/测试SVN更新 &1")]
    //public static void Test()
    //{
    //    Update("/TestObject/");
    //}
    //[MenuItem("Tools/测试SVN提交 &2")]
    //public static void Test2()
    //{
    //    Commit("/TestObject/", "测试测试");
    //}    
    //[MenuItem("Tools/测试SVN最新版本 &2")]
    //public static void Test2()
    //{
    //    Debug.Log(ReadLastChangeRevision(Application.dataPath));
    //}
    #endregion
}
