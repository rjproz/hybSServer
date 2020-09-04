using System.Text;
using UnityEngine;
using Hybriona;
public class HttpServerForStats 
{
    public LNSServerManager serverManager;
    private HttpServer httpServer;

    public void Start(LNSServerManager serverManager,int port)
    {
        this.serverManager = serverManager;
        httpServer = new HttpServer(port);
        httpServer.onHttpRequestReceived += OnHttpRequestReceived;
        httpServer.Start();
        
    }

    public void Stop()
    {
        if(httpServer != null)
        {
            httpServer.Stop();
            httpServer.onHttpRequestReceived = null;
        }
    }

    private void OnHttpRequestReceived(HttpRequest request, HttpResponse response)
    {
        response.SetHeader("Content-Type", "application/json");
        byte[] data = Encoding.ASCII.GetBytes(JsonUtility.ToJson(serverManager.GetData()));
        response.responseStream.Write(data, 0, data.Length);
        response.Send(200);
    }

}
