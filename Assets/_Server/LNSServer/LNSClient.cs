using System;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

public class LNSClient : IDisposable,IQuadTreeObject
{
    
    
    public int networkid { get; set; }
    public uint universalId { get; set; }
    public string id { get; set; }
    public string displayname { get; set; }
    public string gameKey { get; set; }
    public string gameVersion { get; set; }
    public CLIENT_PLATFORM platform { get; set; }
   

    public NetPeer peer { get; set; }
    public bool isWebGL { get { return peer == null; } }
    public LNSRoom connectedRoom { get; set; }
    public NetDataWriter writer { get; set; }
    public Vector2 position { get; set; } //Position in Quad tree
    private object thelock = new object();


    public LNSClient(NetPeer peer)
    {
        this.peer = peer;

        writer = new NetDataWriter();
    }

    public static LNSClient CreateWebGlClient()
    {
        var o =  new LNSClient(null);
        return o;
    }

    ~LNSClient()
    {
        Dispose();
        GC.SuppressFinalize(this);
    }

	public Vector2 GetPosition()
    {
        return position;
    }


    public void SendVerifiedMessage()
    {
        lock (thelock)
        {
            writer.Reset();
            writer.Put(LNSConstants.CLIENT_EVT_VERIFIED);
            writer.Put(universalId);
            if (isWebGL)
            {
                LNSServer.webSocketServer.SendOne(networkid, new ArraySegment<byte>(writer.Data, 0, writer.Length));
            }
            else
            {
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }
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

                if (isWebGL)
                {
                    LNSServer.webSocketServer.SendOne(networkid, new ArraySegment<byte>(writer.Data, 0, writer.Length));
                }
                else
                {
                    peer.Send(writer, DeliveryMethod.ReliableOrdered);
                }
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
            if (isWebGL)
            {
                LNSServer.webSocketServer.SendOne(networkid, new ArraySegment<byte>(writer.Data, 0, writer.Length));
            }
            else
            {
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }
    }

    public void SendRoomCreatedEvent()
    {
        lock (thelock)
        {
            writer.Reset();
            writer.Put(LNSConstants.CLIENT_EVT_ROOM_CREATED);
            writer.Put(connectedRoom.id);
            //UnityEngine.Debug.Log("SendRoomCreatedEvent");
            if (isWebGL)
            {
                LNSServer.webSocketServer.SendOne(networkid, new ArraySegment<byte>(writer.Data, 0, writer.Length));
            }
            else
            {
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }
    }

    public void SendRoomJoinedEvent()
    {
        lock (thelock)
        {
            writer.Reset();
            writer.Put(LNSConstants.CLIENT_EVT_ROOM_JOINED);
            writer.Put(connectedRoom.id);
            if (isWebGL)
            {
                LNSServer.webSocketServer.SendOne(networkid, new ArraySegment<byte>(writer.Data, 0, writer.Length));
            }
            else
            {
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }
    }
    public void SendRoomReJoinedEvent()
    {
        lock (thelock)
        {
            writer.Reset();
            writer.Put(LNSConstants.CLIENT_EVT_ROOM_REJOINED);
            writer.Put(connectedRoom.id);
            if (isWebGL)
            {
                LNSServer.webSocketServer.SendOne(networkid, new ArraySegment<byte>(writer.Data, 0, writer.Length));
            }
            else
            {
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }
    }

    public void SendRoomFailedToJoinEvent(ROOM_FAILURE_CODE failureCode)
    {
        lock (thelock)
        {
            writer.Reset();
            writer.Put(LNSConstants.CLIENT_EVT_ROOM_FAILED_JOIN);
            writer.Put((byte)failureCode);
            if (isWebGL)
            {
                LNSServer.webSocketServer.SendOne(networkid, new ArraySegment<byte>(writer.Data, 0, writer.Length));
            }
            else
            {
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }
    }

    public void SendRoomFailedToRandomJoin()
    {
        lock (thelock)
        {
            writer.Reset();
            writer.Put(LNSConstants.CLIENT_EVT_ROOM_FAILED_RANDOM_JOIN);
            if (isWebGL)
            {
                LNSServer.webSocketServer.SendOne(networkid, new ArraySegment<byte>(writer.Data, 0, writer.Length));
            }
            else
            {
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }
    }

    public void SendRoomFailedToReJoinEvent(ROOM_FAILURE_CODE failureCode)
    {
        lock (thelock)
        {
            writer.Reset();
            writer.Put(LNSConstants.CLIENT_EVT_ROOM_FAILED_REJOIN);
            writer.Put((byte)failureCode);
            if (isWebGL)
            {
                LNSServer.webSocketServer.SendOne(networkid, new ArraySegment<byte>(writer.Data, 0, writer.Length));
            }
            else
            {
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }
    }

    public void Dispose()
    {
        if (!isWebGL)
        {
            peer.Disconnect();
        }
        connectedRoom = null;
    }

    public void SendRoomList(LNSRoomList roomList)
    {
        lock (thelock)
        {
            NetDataWriter _writer = new NetDataWriter();
            _writer.Put(LNSConstants.CLIENT_EVT_ROOM_LIST);
            _writer.Put(UnityEngine.JsonUtility.ToJson(roomList));
            if (isWebGL)
            {
                LNSServer.webSocketServer.SendOne(networkid, new ArraySegment<byte>(_writer.Data, 0, _writer.Length));
            }
            else
            {
                peer.Send(_writer, DeliveryMethod.ReliableOrdered);
            }
            _writer = null;
        }
    }

    public void SendRoomExistResponse(string roomid, bool exists)
    {
        lock (thelock)
        {
            writer.Reset();
            writer.Put(LNSConstants.CLIENT_EVT_ROOM_EXISTS_RESPONSE);
            writer.Put(roomid);
            writer.Put(exists);
            if (isWebGL)
            {
                LNSServer.webSocketServer.SendOne(networkid, new ArraySegment<byte>(writer.Data, 0, writer.Length));
            }
            else
            {
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }
    }
}
