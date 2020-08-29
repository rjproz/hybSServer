using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using RipcordSoftware.HttpWebServer;
using UnityEngine;

public class HttpServerForStats : MonoBehaviour
{
    // Start is called before the first frame update
    public LNSServerManager serverManager;
    private HttpWebServer httpServer;

    private static LNSServerManager _serverManager;
    void Start()
    {
        _serverManager = serverManager;
        string ip = "127.0.0.1";
        if(Application.platform == RuntimePlatform.LinuxPlayer)
        {
            ip = "45.55.33.88";
        }
        Debug.Log("Server at " + ip);

        new Thread(() => { 
            var bindings = new HttpWebServer.Binding[] { new HttpWebServer.Binding(ip, 10001, false) };
            var config = new HttpWebServer.Config();
            httpServer = new HttpWebServer(bindings, config);  
            httpServer.Start(RequestCallback, RequestContinueCallback);
        }).Start();



       
    }


    private static bool RequestCallback(RipcordSoftware.HttpWebServer.HttpWebRequest request, RipcordSoftware.HttpWebServer.HttpWebResponse response)
    {
        //Debug.Log(JsonUtility.ToJson(_serverManager.GetData()));
        //response.ContentType = "text/html";
        response.ContentType = "application/json";
        Stream stream =  response.GetResponseStream(null);
        byte[] data = Encoding.ASCII.GetBytes(JsonUtility.ToJson(_serverManager.GetData()));
        stream.Write(data,0, data.Length);
      
        return true;
    }

    private static bool RequestContinueCallback(RipcordSoftware.HttpWebServer.HttpWebRequest request)
    {
        return true;
    }

    public static string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "127.0.0.1";
    }
}
