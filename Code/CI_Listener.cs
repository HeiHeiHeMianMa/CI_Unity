using UnityEngine;
using UnityEditor;
using System.Net;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Threading;
using System;
using System.Web;
using System.Net.NetworkInformation;
using System.Net.Sockets;

public class CI_Listener
{
    readonly static string LogPathFormatUNC = "\\\\{0}\\CI\\Logs\\{1}\\{2}.txt";

    public static string LogPath;
    public static string Context;

    private static HttpListener listener;
    private static Thread linstenThread;
    private static bool isWorking = false;

    public static void Start()
    {
        Stop();

        linstenThread = new Thread(RequestLoop);
        linstenThread.Start(Application.dataPath);

        isWorking = true;
    }

    public static void Stop()
    {
        listener?.Stop();
        listener = null;
        linstenThread?.Abort();
        linstenThread = null;
        isWorking = false;
    }

    public static bool IsWorking()
    {
        return isWorking;
    }

    private static async void RequestLoop(object rootPath)
    {
        listener = new HttpListener();
        listener.Prefixes.Add(string.Format("http://{0}:{1}/", GetIP(),CI_Data.Instance.port));
        listener.Start();

        while (true)
        {
            var context = await listener.GetContextAsync();

            try
            {
                StreamReader sr = new StreamReader(context.Request.InputStream, Encoding.Default, true);
                string postData = sr.ReadToEnd();

                postData = UnityWebRequest.UnEscapeURL(postData);
                sr.Close();
                Debug.Log("Unity Request: " + postData);

                var logPath = string.Format(LogPathFormatUNC, context.Request.RemoteEndPoint.Address, CI_Data.Instance.tag, DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss"));
                var svnVersion = CI_SVN.ReadLastChangeRevision((string)rootPath);
                var res = logPath + "," + svnVersion;
                Debug.Log("Unity Response: " + res);
                StreamWriter sw = new StreamWriter(context.Response.OutputStream);
                sw.Write(res);
                sw.Close();
                context.Response.Close();
                context = null;
                LogPath = logPath;
                Context = postData;
            }
            catch (Exception e)
            {
                Debug.LogError("获得消息出错：" + e.StackTrace);
            }
        }
    }

    public static string GetIP()
    {
        string output = "";

        foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            NetworkInterfaceType _type1 = NetworkInterfaceType.Wireless80211;
            NetworkInterfaceType _type2 = NetworkInterfaceType.Ethernet;

            if ((item.NetworkInterfaceType == _type1 || item.NetworkInterfaceType == _type2) && item.OperationalStatus == OperationalStatus.Up)
#endif 
            {
                foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                {
                    //IPv4
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        output = ip.Address.ToString();
                    }
                }
            }
        }
        return output;
    }

    /*
    public static void TestCommand()
    {
        Dictionary<string, string> data = new Dictionary<string, string>();

        data.Add("orderId", "10000001");
        data.Add("type", "compile");
        data.Add("logFile", "xxxxxxx");

        string jsonParam = MiniJSON.Json.Serialize(data);
        Debug.Log("[测试命令-发出]=" + jsonParam);
        byte[] body = Encoding.UTF8.GetBytes(jsonParam);
        UnityWebRequest request = new UnityWebRequest(CommandConfig.LintenAddress, "POST");
        request.uploadHandler = new UploadHandlerRaw(body);
        request.SetRequestHeader("Content-Type", "application/json;charset=utf-8");
        request.downloadHandler = new DownloadHandlerBuffer();
        UnityWebRequestAsyncOperation requestAsync = request.SendWebRequest();
        requestAsync.completed += (option) =>
        {
            Debug.Log("[测试命令-返回]=" + request.downloadHandler.text);
        };
    }
    */
}
