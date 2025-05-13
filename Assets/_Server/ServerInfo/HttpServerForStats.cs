using UnityEngine;
using Hybriona;
public class HttpServerForStats 
{
    public LNSServerManager serverManager;
    private HttpServer httpServer;

    public void Start(LNSServerManager serverManager,int port)
    {
        this.serverManager = serverManager;
        httpServer = new HttpServer(port,2000);
        httpServer.Get("/", (context, router) =>
        {
           
            context.Response.SendResponse(JsonUtility.ToJson(serverManager.GetData()), HttpContentType.Json);
            context.Dispose();
            context = null;
        });
       
        httpServer.Start();

    }

    public void Stop()
    {
        if(httpServer != null)
        {
            httpServer.Stop();
            
        }
    }

    

}
