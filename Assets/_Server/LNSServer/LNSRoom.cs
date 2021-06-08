using System;
using System.Collections.Generic;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;
public class LNSRoom : IDisposable
{
    public string id { get; set; }
    public string gameKey { get; set; }
    public string gameVersion { get; set; }
    public byte primaryPlatform { get; set; }
    
    public LNSCreateRoomParameters roomParameters { get; set; }
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
            return !string.IsNullOrEmpty(roomParameters.password);
        }
    }

    public LNSGame assocGame { get; set; }
    public bool isOpen { get; private set; } = true;

    public List<LNSClient> clients { get; set; } = new List<LNSClient>();
    public List<string> disconnectedClients { get; set; } = new List<string>();
    
    public LNSClient masterClient { get; set; }
    public NetDataWriter writer { get; set; }

    private QuadTree<LNSClient> quadTree;
    private List<LNSClient> quadTreeSearchResults = new List<LNSClient>();

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


    public void Prepare()
    {
        if(roomParameters.isQuadTreeAllowed && quadTree == null)
        {
            quadTree = new QuadTree<LNSClient>(roomParameters.maxPlayers * 10, roomParameters.quadTreeBounds);
            
        }
        
    }

    public void ProcessReceivedData(LNSClient from,byte instructionCode,NetPacketReader reader, DeliveryMethod deliveryMethod)
    {
        byte code = instructionCode;
        if (code == LNSConstants.SERVER_EVT_LOCK_ROOM)
        {
            if (from.id == masterClient.id)
            {
                isOpen = false;
            }
            return;
        }
        else if (code == LNSConstants.SERVER_EVT_UNLOCK_ROOM)
        {
            if (from.id == masterClient.id)
            {
                isOpen = true;
            }
            return;
        }
        else if (code == LNSConstants.SERVER_EVT_RAW_DATA_TO_CLIENT)
        {
            string targetid = reader.GetString();

            lock (thelock)
            {
                for (int i = 0; i < clients.Count; i++)
                {
                    var targetClient = clients.Find(client => client.id == targetid);
                    writer.Reset();
                    writer.Put(LNSConstants.CLIENT_EVT_ROOM_RAW);
                    writer.Put(from.id);
                    writer.Put(reader.GetRemainingBytes());
                    targetClient.peer.Send(writer, deliveryMethod);
                }
            }
            
        }
        else if (code == LNSConstants.SERVER_EVT_RAW_DATA_TO_NEARBY_CLIENTS)
        {
         
            if (roomParameters.isQuadTreeAllowed)
            {

                
                Rect searchRect = new Rect(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
                from.position = searchRect.center;
               
                lock (thelock)
                {
                    quadTreeSearchResults.Clear();
                    quadTree.RetrieveObjectsInAreaNoAlloc(searchRect, ref quadTreeSearchResults);
                    //Debug.LogFormat("From {0} - Search Rect {1},{2} {3},{4} - Found: {5}", from.id, searchRect.center.x, searchRect.center.x, searchRect.width, searchRect.height, quadTreeSearchResults.Count);
                    writer.Reset();
                    writer.Put(LNSConstants.CLIENT_EVT_ROOM_RAW);
                    writer.Put(from.id);
                    writer.Put(reader.GetRemainingBytes());
                    for (int i = 0; i < quadTreeSearchResults.Count; i++)
                    {
                        if (quadTreeSearchResults[i].networkid != from.networkid)
                        {
                            quadTreeSearchResults[i].peer.Send(writer, deliveryMethod);
                        }
                    }
                }
            }

        }
        else if(code == LNSConstants.SERVER_EVT_RAW_DATA_CACHE)
        {
            /*
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
            }*/
        }
        else
        {
            
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
            if (roomParameters.isQuadTreeAllowed)
            {
                quadTree.Insert(client);
            }
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
               
                for (int i = 0; i < clients.Count; i++)
                {
                    if (clients[i].id != client.id)
                    {
                        client.writer.Reset();
                        client.writer.Put(LNSConstants.CLIENT_EVT_ROOM_PLAYER_CONNECTED);
                        client.writer.Put(clients[i].id);
                        client.writer.Put(clients[i].displayname);
                        client.writer.Put((byte) clients[i].platform);
                        client.writer.Put(clients[i].networkid);

                        client.peer.Send(client.writer, DeliveryMethod.ReliableOrdered);
                    }
                }

                writer.Reset();
                writer.Put(LNSConstants.CLIENT_EVT_ROOM_MASTERCLIENT_CHANGED);
                writer.Put(masterClient.id);
                
                client.peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }
               
        
    }

    public void RemovePlayer(LNSClient client)
    {
        string clientid = client.id;
        lock (thelock)
        {
            if (roomParameters.isQuadTreeAllowed)
            {
                quadTree.Remove(client);
            }
            clients.Remove(client);
            if (!disconnectedClients.Contains(clientid))
            {
                disconnectedClients.Add(clientid);
            }
            if (clients.Count == 0)
            {
                if (clients.Count <= 0)
                {
                    assocGame.RemoveRoom(this); // Destroy room
                }
                //new Thread(() =>
                //{
                //    try
                //    {
                //        for (int i = 0; i < 10; i++)
                //        {
                //            if (clients.Count > 0) // check if someone rejoined
                //            {
                //                return;
                //            }
                //            Thread.Sleep(5000);
                //        }
                //    }
                //    catch { }
                    
                //}).Start(); ;
                
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
            writer.Put((byte)client.platform);
            writer.Put(client.networkid);

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

    public bool IsFilterMatch(LNSJoinRoomFilter source)
    {
        if(roomParameters.filters == null && source.GetLength() > 0)
        {
            return false;
        }

        return roomParameters.filters.IsFilterMatch(source);
    }
}
