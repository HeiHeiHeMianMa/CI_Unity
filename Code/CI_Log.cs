using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class CI_Log
{
    private static FileStream fs;
    private static StreamWriter sw;

    public static void Start(string _path)
    {
        if (string.IsNullOrEmpty(_path)) return;

        var path = _path;
        var directory = Path.GetDirectoryName(path);

        try 
        {
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        }
        catch(Exception e)
        {
            Debug.LogError(e.ToString());
            return;
        }

        fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        sw = new StreamWriter(fs, Encoding.UTF8); 

        Application.logMessageReceivedThreaded -= Log;  
        Application.logMessageReceivedThreaded += Log;
    }

    public static void Stop()
    {
        Application.logMessageReceivedThreaded -= Log;
        if(sw != null) sw.Close();
        if (fs != null) fs.Close();
    }

    static void Log(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning) return;

        var dateStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); dateStr = condition.Contains(dateStr) ? "" : dateStr;
        var typeStr = string.Format("[{0}]", type);
        var stackStr = (type == LogType.Error || type == LogType.Exception) ? "\n" + stackTrace : "";
        var msg = string.Format("{0} {1} {2}", dateStr, typeStr.PadRight(9), condition + stackStr);
        sw.WriteLine(msg); 
        sw.Flush();
    }
}
