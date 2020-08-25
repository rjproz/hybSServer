using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class Server
{
    public TcpListener listener;
    private List<Client> clients = new List<Client>();
    private static object writelock = new object();

    public void StartServer(int port)
    {
        listener = new TcpListener(IPAddress.Any, port);
        listener.Server.NoDelay = true;

        try
        {
            listener.Start();
            BeginAcceptClient();
            Debug.Log("Server started");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Server failed to start "+ex.Message);
        }
    }

    public void BeginAcceptClient()
    {
        listener.BeginAcceptTcpClient(new AsyncCallback(TcpBeginAcceptCallback), null);
        
    }


    private void TcpBeginAcceptCallback(IAsyncResult result)
    {
        try
        {
            TcpClient socket = listener.EndAcceptTcpClient(result);
            socket.NoDelay = true;
            
            Debug.Log("new connection from " + socket.Client.RemoteEndPoint.ToString());
            Client client = null;
            for (int i = 0; i < clients.Count; i++)
            {
                if (clients[i].isFree)
                {
                    client = clients[i];
                    Debug.Log("Found slot for " + socket.Client.RemoteEndPoint.ToString());
                    //Debug.Log("Is client null: "+(client == null));
                    break;
                }
            }
            if (client == null)
            {
                Debug.Log("create new slot " + socket.Client.RemoteEndPoint.ToString());
                client = new Client();
                client.server = this;
                clients.Add(client);
            }

            client.Init(socket);


            Debug.Log("Client count " + clients.Count);
        }
        catch(System.Exception ex)
        {
            Debug.LogError(ex.Message + " - " + ex.StackTrace);
        }
        BeginAcceptClient();
        

    }

    public void SendToAllClientExcept(string id, byte[] data)
    {
        lock (writelock)
        {
            for (int i = 0; i < clients.Count; i++)
            {
                if (!clients[i].isFree && clients[i].id != id)
                {
                    clients[i].SendData(data);
                }
            }
        }
    }
   
    public void Dispose()
    {
        listener.Stop();

        for (int i = 0; i < clients.Count; i++)
        {
            clients[i].Dispose();
            clients[i] = null;
        }
        clients.Clear();
    }
}
