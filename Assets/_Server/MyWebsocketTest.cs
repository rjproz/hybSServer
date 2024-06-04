using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MyWebsocketTest : MonoBehaviour
{
    HybWebSocketServer webSocketServer;
    private void Start()
    {
        bool isSSL = true;
#if UNITY_EDITOR_OSX
        isSSL = false;
#endif


       
        if (isSSL)
        {
            webSocketServer = new HybWebSocketServer("cert.pfx", "rjproz");
        }
        else
        {
            webSocketServer = new HybWebSocketServer("", "rjproz");
        }

        webSocketServer.onConnect = OnClientConnected;
        webSocketServer.onDisconnect = OnClientDisconnected;
        webSocketServer.onData = OnDataReceived;
        webSocketServer.Start(10010);
        
    }

    private void OnDataReceived(int connectionId, ArraySegment<byte> data)
    {
        foreach(int id in webSocketServer.GetConnectionIds())
        {
            if (id != connectionId)
            {
                webSocketServer.SendOne(id, data);
            }
        }
    }

    private void OnClientDisconnected(int connectionId)
    {
        Debug.Log($"Disconnected {connectionId} - Total Clients: {webSocketServer.ConnectionCount()}");
    }

    private void OnClientConnected(int connectionId)
    {
        Debug.Log($"Connected {connectionId} - Total Clients: {webSocketServer.ConnectionCount()}");
        //string msg = "Message for " + connectionId + " at " + System.DateTime.Now.ToString();
        //webSocketServer.SendOne(connectionId, new ArraySegment<byte>(msg.ConvertToBytes()));
    }
}
