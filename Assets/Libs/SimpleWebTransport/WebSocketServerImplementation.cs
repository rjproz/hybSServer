using System;
using System.Collections.Generic;
using WatsonWebsocket;

public class WebSocketServerImplementation 
{
    public event Action<int> onConnect;
    public event Action<int> onDisconnect;
    public event Action<int, ArraySegment<byte>> onData;
    public event Action<int, Exception> onError;
    WatsonWsServer server;
    int connectionIdCounter = 0;
    private Dictionary<int, Guid> connectionIdToGuid = new Dictionary<int, Guid>();
    private Dictionary<Guid, int> guidToConnectionId= new Dictionary<Guid, int>();

    public void Start()
    {
        server.Start();
    }
    public WebSocketServerImplementation(List<string> hostnames,int port,bool ssl)
    {
        server = new WatsonWsServer(hostnames, port, ssl);
        server.Logger = (data) => { UnityEngine.Debug.Log(data); };
        server.ClientConnected += ClientConnected;
        server.ClientDisconnected += ClientDisconnected;
        server.MessageReceived += MessageReceived;
       

       
    }

    public void SendOne(int connectionId, ArraySegment<byte> segment)
    {

        server.SendAsync(connectionIdToGuid[connectionId], segment);
    }

    public void KickClient(int connectionId)
    {
        server.DisconnectClient(connectionIdToGuid[connectionId]);
    }


    void ClientConnected(object sender, ConnectionEventArgs args)
    {
        int connectionId = ++connectionIdCounter;
        connectionIdToGuid.Add(connectionId, args.Client.Guid);
        guidToConnectionId.TryAdd(args.Client.Guid, connectionId);
        if (onConnect != null)
        {

            onConnect.Invoke(connectionId);
        }
#if UNITY_EDITOR
        Console.WriteLine("Client connected: " + args.Client.ToString());
#endif
    }

    void ClientDisconnected(object sender, DisconnectionEventArgs args)
    {
        int connectionId = guidToConnectionId[args.Client.Guid];
        guidToConnectionId.Remove(args.Client.Guid);
        connectionIdToGuid.Remove(connectionId);
        if (onDisconnect != null)
        {
            onDisconnect.Invoke(connectionId);
        }



#if UNITY_EDITOR
        Console.WriteLine("Client disconnected: " + args.Client.ToString());
#endif
    }

    void MessageReceived(object sender, MessageReceivedEventArgs args)
    {
        int connectionId = guidToConnectionId[args.Client.Guid];
        if (onData != null)
        {
            onData(connectionId, args.Data);
        }
#if UNITY_EDITOR
        Console.WriteLine("Message received from " + args.Client.ToString());
#endif
    }
}
