using System.Collections;
using System.Collections.Generic;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

public class ServerLiteNetLib : MonoBehaviour
{
    // Start is called before the first frame update

    private Dictionary<int, NetPeer> clients = new Dictionary<int, NetPeer>();
    private static object thelock = new object();
    void Start()
    {
        Application.targetFrameRate = 10;
        new Thread(() =>
        {
            EventBasedNetListener listener = new EventBasedNetListener();
            NetManager server = new NetManager(listener);
            server.Start(10001);
            Debug.Log("Server Started");
            listener.ConnectionRequestEvent += request =>
            {
                Debug.Log("connection request recieved");
                
                request.Accept();

                //if (server.ConnectedPeersCount < 20 /* max connections */)
                //    request.AcceptIfKey("test");
                //else
                //    request.Reject();
            };
            
            listener.PeerConnectedEvent += peer =>
            {
                lock (thelock)
                {
                    if (!clients.ContainsKey(peer.Id))
                    {
                        clients.Add(peer.Id,peer);
                    }
                    else
                    {
                        clients[peer.Id] = peer;
                    }
                }
                Debug.Log("Connected : " + peer.Id);
                //NetDataWriter writer = new NetDataWriter();                 // Create writer class
                //writer.Put("Hello client!");                                // Put some string
                //peer.Send(writer, DeliveryMethod.ReliableOrdered);             // Send with reliability
            };

            listener.NetworkReceiveEvent += (NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod) =>
            {
              
                byte  [] data =  reader.GetRemainingBytes();
     
                reader.Recycle();
                
                server.SendToAll(data, deliveryMethod, peer);
                
            };
            listener.PeerDisconnectedEvent += (NetPeer peer, DisconnectInfo disconnectInfo) =>
            {
                lock(thelock)
                {
                    if(clients.ContainsKey(peer.Id))
                    {
                        clients.Remove(peer.Id);
                    }
                }
                Debug.Log("Disconnected : " + peer.Id);
            };


            while (true)
            {
                server.PollEvents();
                Thread.Sleep(15);
            }
        }).Start() ;
       
    }

   

    
}
