using System;
using LiteNetLib;
using LiteNetLib.Utils;

public class LNSClient : IDisposable
{
    
   
    public int networkid { get; set; }
    public string id { get; set; }
    public string displayname { get; set; }
    public string gameKey { get; set; }
    public string gameVersion { get; set; }
    public CLIENT_PLATFORM platform { get; set; }
   

    public NetPeer peer { get; set; }
    public LNSRoom connectedRoom { get; set; }
    public NetDataWriter writer { get; set; }

    private object thelock = new object();
    public LNSClient(NetPeer peer)
    {
        this.peer = peer;

        writer = new NetDataWriter();
    }

    ~LNSClient()
    {
        Dispose();
        GC.SuppressFinalize(this);
    }

  
    public void SendDisconnectEvent(bool leftroom)
    {
       

        if (connectedRoom != null)
        {
            connectedRoom.RemovePlayer(this);

            lock (thelock)
            {
                writer.Reset();
                writer.Put(LNSConstants.CLIENT_EVT_ROOM_DISCONNECTED);
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }
        
    }

   
    public void SendFailedToCreateRoomEvent(ROOM_FAILURE_CODE code)
    {
        lock(thelock)
        {
            writer.Reset();
            writer.Put(LNSConstants.CLIENT_EVT_ROOM_FAILED_CREATE);
            writer.Put((byte)code);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }

    public void SendRoomCreatedEvent()
    {
        lock (thelock)
        {
            writer.Reset();
            writer.Put(LNSConstants.CLIENT_EVT_ROOM_CREATED);
            //UnityEngine.Debug.Log("SendRoomCreatedEvent");
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }

    public void SendRoomJoinedEvent()
    {
        lock (thelock)
        {
            writer.Reset();
            writer.Put(LNSConstants.CLIENT_EVT_ROOM_JOINED);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }
    public void SendRoomReJoinedEvent()
    {
        lock (thelock)
        {
            writer.Reset();
            writer.Put(LNSConstants.CLIENT_EVT_ROOM_REJOINED);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }

    public void SendRoomFailedToJoinEvent(ROOM_FAILURE_CODE failureCode)
    {
        lock (thelock)
        {
            writer.Reset();
            writer.Put(LNSConstants.CLIENT_EVT_ROOM_FAILED_JOIN);
            writer.Put((byte)failureCode);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }

    public void SendRoomFailedToReJoinEvent(ROOM_FAILURE_CODE failureCode)
    {
        lock (thelock)
        {
            writer.Reset();
            writer.Put(LNSConstants.CLIENT_EVT_ROOM_FAILED_REJOIN);
            writer.Put((byte)failureCode);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }

    public void Dispose()
    {
        peer.Disconnect();
        connectedRoom = null;
    }

}
