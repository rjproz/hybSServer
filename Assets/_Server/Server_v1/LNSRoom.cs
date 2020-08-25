using System;
using System.Collections.Generic;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

public class LNSRoom : IDisposable
{
    public string id { get; set; }
    public int maxPlayers { get; set; } = 100;
    public string password { get; set; } = null;
    public bool isPublic { get; set; } = true;
    public int playerCount
    {
        get
        {
            return clients.Count;
        }
    }

    public bool hasPassword
    {
        get
        {
            return !string.IsNullOrEmpty(password);
        }
    }

    public LNSServer server { get; set; }
    public bool isOpen { get; private set; } = true;

    public List<LNSClient> clients { get; set; } = new List<LNSClient>();
    public List<string> disconnectedClients { get; set; } = new List<string>();
    public LNSClient masterClient { get; set; }
    public NetDataWriter writer { get; set; }
    
    private object thelock = new object();
    public LNSRoom(string id)
    {
        this.id = id;

        writer = new NetDataWriter();
    }

    ~LNSRoom()
    {
        Dispose();
        GC.SuppressFinalize(this);
    }

    public void ProcessReceivedData(LNSClient from,byte instructionCode,NetPacketReader reader, DeliveryMethod deliveryMethod)
    {
        byte code = instructionCode;
        if(code == LNSConstants.SERVER_EVT_LOCK_ROOM)
        {
            isOpen = false;
            return;
        }
        else if (code == LNSConstants.SERVER_EVT_UNLOCK_ROOM)
        {
            isOpen = true;
            return;
        }


        lock (thelock)
        {
            writer.Reset();
            writer.Put(LNSConstants.CLIENT_EVT_ROOM_RAW);
            writer.Put(from.id);
            writer.Put(reader.GetRemainingBytes());
            for (int i = 0; i < clients.Count; i++)
            {
                if (clients[i].networkid != from.networkid)
                {
                    clients[i].peer.Send(writer, deliveryMethod);
                }
            }
        }
    }

    public bool CanReconnect(LNSClient client)
    {
        lock (thelock)
        {
            return disconnectedClients.Contains(client.id);
        }
    }


    public void AddPlayer(LNSClient client)
    {
        lock(thelock)
        {
           
            clients.Add(client);
            if (disconnectedClients.Contains(client.id))
            {
                disconnectedClients.Remove(client.id);
            }
        }
        SendPlayerConnectedEvent(client); // player connected event
        if (clients.Count == 1)
        {
            masterClient = client;
            SendMasterPlayerChangedEvent();  //Send master client changed event
        }
        else
        {
            lock (thelock)
            {
                client.writer.Reset();
                client.writer.Put(LNSConstants.CLIENT_EVT_ROOM_MASTERCLIENT_CHANGED);
                client.writer.Put(masterClient.id);
                client.peer.Send(client.writer, DeliveryMethod.ReliableOrdered);
                for (int i = 0; i < clients.Count; i++)
                {
                    if (clients[i].id != client.id)
                    {
                        client.writer.Reset();
                        client.writer.Put(LNSConstants.CLIENT_EVT_ROOM_PLAYER_CONNECTED);
                        client.writer.Put(clients[i].id);
                        client.writer.Put(clients[i].displayname);

                        client.peer.Send(client.writer, DeliveryMethod.ReliableOrdered);
                    }
                }
            }
        }
               
        
    }

    public void RemovePlayer(LNSClient client)
    {
        string clientid = client.id;
        lock (thelock)
        {
            clients.Remove(client);
            if (!disconnectedClients.Contains(clientid))
            {
                disconnectedClients.Add(clientid);
            }
            if (clients.Count == 0)
            {
                new Thread(() =>
                {
                    try
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            if (clients.Count > 0) // check if someone rejoined
                            {
                                return;
                            }
                            Thread.Sleep(5000);
                        }
                    }
                    catch { }
                    if (clients.Count <= 0)
                    {
                        server.RemoveRoom(this); // Destroy room
                    }
                }).Start(); ;
                
                return;
            }
           
        }
        
        SendPlayerDisconnectedEvent(client);
        if(clientid == masterClient.id)
        {
            masterClient = clients[0];
            SendMasterPlayerChangedEvent(); //Send master client changed event
        }

    }

   

    public void SendPlayerConnectedEvent(LNSClient client)
    {
        //UnityEngine.Debug.Log("SendPlayerConnectedEvent: "+client.displayname);
        lock (thelock)
        {
            writer.Reset();
            writer.Put(LNSConstants.CLIENT_EVT_ROOM_PLAYER_CONNECTED);
            //UnityEngine.Debug.Log("SendPlayerConnectedEvent " + client.id + " "+client.displayname);
            writer.Put(client.id);
            writer.Put(client.displayname);

            for (int i=0;i<clients.Count;i++)
            {
                if(clients[i].networkid != client.networkid)
                {
                    clients[i].peer.Send(writer, DeliveryMethod.ReliableOrdered);
                }
            }
        }
    }


    public void SendPlayerDisconnectedEvent(LNSClient client)
    {
        lock (thelock)
        {
            writer.Reset();
            writer.Put(LNSConstants.CLIENT_EVT_ROOM_PLAYER_DISCONNECTED);
            writer.Put(client.id);

            for (int i = 0; i < clients.Count; i++)
            {
                clients[i].peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }
    }


    public void SendMasterPlayerChangedEvent()
    {
        lock (thelock)
        {
            writer.Reset();
            writer.Put(LNSConstants.CLIENT_EVT_ROOM_MASTERCLIENT_CHANGED);
            writer.Put(masterClient.id);

            for (int i = 0; i < clients.Count; i++)
            {
                clients[i].peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }
    }

   
    public void Dispose()
    {
        if (clients != null)
        {
            clients.Clear();
            clients = null;
        }
        if (writer != null)
        {
            writer.Reset();
            writer = null;
        }
    }
}
