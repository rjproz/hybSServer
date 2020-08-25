using System;
using System.Collections.Generic;
using System.Threading;
using LiteNetLib;
using UnityEngine;

public class LNSServer : IDisposable
{
    public int port { get; private set; }
    public string key { get; private set; }

    private Dictionary<int, LNSClient> clients = new Dictionary<int, LNSClient>();
    
    private Dictionary<string, LNSGame> games = new Dictionary<string, LNSGame>();

    public object thelock = new object();

    private bool disposed;
    public LNSServer(int port,string key)
    {
        this.port = port;
        this.key = key;
    }

    ~ LNSServer()
    {
        Dispose();
        GC.SuppressFinalize(this);
    }

    public void Start()
    {
        new Thread(() =>
        {
            EventBasedNetListener listener = new EventBasedNetListener();
            NetManager server = new NetManager(listener);
            server.Start(this.port);
            Debug.Log("Server Started");
            listener.ConnectionRequestEvent += request =>
            {
                Debug.Log("connection request recieved");
                
                
                //string clientKey = request.Data.GetString();
                //if (clientKey == this.key)
                //{
                //    byte initialcode = 0;
                //    if(request.Data.TryGetByte(out initialcode))
                //    {
                //        if (initialcode == LNSConstants.SERVER_EVT_REJOIN_ROOM)
                //        {
                //            string userid = request.Data.GetString();
                //            string displayName = request.Data.GetString();
                //            string roomid = request.Data.GetString();

                            
                //        }
                //    }
                //    request.Accept();

                   
                //}
                //else
                //{
                //    request.Reject();
                //}
                
                request.AcceptIfKey(this.key);
            };

            listener.PeerConnectedEvent += peer =>
            {
                lock (thelock)
                {
                    try
                    {
                        if (!clients.ContainsKey(peer.Id))
                        {
                            clients.Add(peer.Id, new LNSClient(peer));
                        }
                        else
                        {
                            clients[peer.Id].peer = peer;
                        }
                        clients[peer.Id].networkid = peer.Id;
                        Debug.Log("Connected : " + peer.Id + " | Total clients: " + clients.Count);
                    }
                    catch(System.Exception ex)
                    {
                        Debug.LogError(ex.Message + " - "+ex.StackTrace);
                    }
                }
               
             
            };

            listener.NetworkReceiveEvent += (NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod) =>
            {
                int peerId = peer.Id;
                LNSClient client = clients[peerId];

                byte instruction = reader.GetByte();

                if (client.connectedRoom == null)
                {

                  
                    string userid = reader.GetString();
                    string displayName = reader.GetString();
                    string gameKey = reader.GetString();
                    string version = reader.GetString();
                    LNSConstants.CLIENT_PLATFORM platform = (LNSConstants.CLIENT_PLATFORM)reader.GetByte();

                    if (string.IsNullOrEmpty(gameKey) || !gameKey.Contains("hybriona"))
                    {
                        client.peer.Disconnect();
                        //TODO Send unauthorized app error
                        return;
                    }

                    


                    client.id = userid;
                    client.displayname = displayName;
                    client.gameKey = gameKey;
                    client.gameVersion = version;
                    client.platform = platform;


                    LNSGame game = null;
                    if(games.ContainsKey(gameKey))
                    {
                        game = games[gameKey];
                    }
                    else
                    {
                        game = new LNSGame(gameKey, this);
                        games.Add(game.gameKey,game);
                    }

                    Dictionary<string,LNSRoom> rooms = game.rooms;
                    if (instruction == LNSConstants.SERVER_EVT_CREATE_ROOM)
                    {
                          

                        string roomid =  reader.GetString();
                        bool isPublic = reader.GetBool();
                        bool hasPassword = reader.GetBool();
                        string password = null;
                        if (hasPassword)
                        {
                            password = reader.GetString();
                        }
                        int maxPlayers = reader.GetInt();

                           

                        lock (thelock)
                        {
                            if (rooms.ContainsKey(roomid))
                            {
                                client.SendFailedToCreateRoomEvent(LNSConstants.ROOM_FAILURE_CODE.ROOM_ALREADY_EXIST); //: Room creation failed
                            }
                            else
                            {
                                LNSRoom room = new LNSRoom(roomid);

                                room.gameKey = gameKey;
                                room.gameVersion = version;
                                room.primaryPlatform = (byte)platform;

                                room.isPublic = isPublic;
                                room.password = password;
                                room.maxPlayers = maxPlayers;
                               


                                room.assocGame = game;
                                rooms.Add(roomid, room);
                                client.connectedRoom = room;
                                room.AddPlayer(client);
                                client.SendRoomCreatedEvent(); //: Room created 
                            }
                        }
                    }
                    else if (instruction == LNSConstants.SERVER_EVT_CREATE_OR_JOIN_ROOM)
                    {
                           
                        string roomid =  reader.GetString();
                        int maxPlayers = reader.GetInt();

                         
                        lock (thelock)
                        {
                            if (!rooms.ContainsKey(roomid))
                            {
                                LNSRoom room = new LNSRoom(roomid);
                                room.gameKey = gameKey;
                                room.gameVersion = version;
                                room.primaryPlatform = (byte)platform;

                                room.maxPlayers = maxPlayers;

                                room.assocGame = game;
                                rooms.Add(roomid, room);
                                client.connectedRoom = room;
                                room.AddPlayer(client);
                                client.SendRoomCreatedEvent();  //: Room created Event

                            }
                            else
                            {

                                LNSRoom room = rooms[roomid];
                                if (room.gameVersion != client.gameVersion)
                                {
                                    client.SendRoomFailedToJoinEvent(LNSConstants.ROOM_FAILURE_CODE.VERSION_MISMATCH);
                                }
                                else if (!room.isOpen)
                                {
                                    client.SendRoomFailedToJoinEvent(LNSConstants.ROOM_FAILURE_CODE.ROOM_LOCKED); // Room join failed
                                }
                                else if (room.playerCount < room.maxPlayers)
                                {
                                    client.SendRoomFailedToJoinEvent(LNSConstants.ROOM_FAILURE_CODE.ROOM_FULL);
                                }
                              
                                else
                                {
                                    client.connectedRoom = room;
                                    room.AddPlayer(client);

                                    client.SendRoomJoinedEvent();//: Room joined
                                }
                            }
                        }
                    }
                    else if (instruction == LNSConstants.SERVER_EVT_JOIN_ROOM)
                    {
                           

                        string roomid =  reader.GetString();
                        bool hasPassword = reader.GetBool();
                        string password = null;
                        if (hasPassword)
                        {
                            password = reader.GetString();
                        }

                          
                        lock (thelock)
                        {
                            if (rooms.ContainsKey(roomid))
                            {
                                LNSRoom room = rooms[roomid];

                                if (room.gameVersion != client.gameVersion)
                                {
                                    client.SendRoomFailedToJoinEvent(LNSConstants.ROOM_FAILURE_CODE.VERSION_MISMATCH);
                                }
                                else if ((room.hasPassword && password == null) || (room.hasPassword && room.password != password))
                                {
                                    client.SendRoomFailedToJoinEvent(LNSConstants.ROOM_FAILURE_CODE.PASSWORD_MISMATCH);
                                }
                                else if (!room.isOpen)
                                {
                                    client.SendRoomFailedToJoinEvent(LNSConstants.ROOM_FAILURE_CODE.ROOM_LOCKED);
                                }
                                else if (room.playerCount >= room.maxPlayers)
                                {
                                    client.SendRoomFailedToJoinEvent(LNSConstants.ROOM_FAILURE_CODE.ROOM_FULL);
                                }

                                else
                                {
                                    client.connectedRoom = room;
                                    room.AddPlayer(client);
                                    client.SendRoomJoinedEvent(); //: Room Joined
                                }
                            }
                            else
                            {
                                client.SendRoomFailedToJoinEvent(LNSConstants.ROOM_FAILURE_CODE.ROOM_DOESNT_EXIST); // Room join failed
                            }

                        }
                    }
                    else if (instruction == LNSConstants.SERVER_EVT_REJOIN_ROOM)
                    {
                          
                        string roomid =  reader.GetString();

                       
                        lock (thelock)
                        {
                            if (rooms.ContainsKey(roomid))
                            {
                                LNSRoom room = rooms[roomid];

                                if (room.playerCount >= room.maxPlayers)
                                {
                                    client.SendRoomFailedToReJoinEvent(LNSConstants.ROOM_FAILURE_CODE.ROOM_FULL);
                                }
                                else if (!room.CanReconnect(client))
                                {
                                    client.SendRoomFailedToReJoinEvent(LNSConstants.ROOM_FAILURE_CODE.REJOIN_NOT_AUTHORIZED);
                                }
                                else
                                {
                                    client.connectedRoom = room;
                                    room.AddPlayer(client);
                                    client.SendRoomReJoinedEvent(); //: Room ReJoined
                                }
                            }
                            else
                            {
                                client.SendRoomFailedToReJoinEvent(LNSConstants.ROOM_FAILURE_CODE.ROOM_DOESNT_EXIST); // Room join failed
                            }

                        }
                    }
                    

                }
                else
                {
                    if (instruction == LNSConstants.SERVER_EVT_LEAVE_ROOM)
                    {

                        client.SendDisconnectEvent(true);
                        client.connectedRoom = null;
                    }
                    else
                    {
                        client.connectedRoom.ProcessReceivedData(client, instruction,reader, deliveryMethod);
                    }
                }
               

                reader.Recycle();


            };
            listener.PeerDisconnectedEvent += (NetPeer peer, DisconnectInfo disconnectInfo) =>
            {
                lock (thelock)
                {
                    int peerId = peer.Id;
                    if (clients.ContainsKey(peerId))
                    {
                        clients[peerId].SendDisconnectEvent(false);
                        clients[peerId].Dispose();
                        clients.Remove(peerId);
                    }
                }
                Debug.Log("Disconnected : " + peer.Id);
            };


            while (true)
            {
                server.PollEvents();
                Thread.Sleep(15);
            }
        }).Start();
    }


    public void RemoveGame(LNSGame game)
    {
        lock(thelock)
        {
            if(games.ContainsKey(game.gameKey))
            {
                games.Remove(game.gameKey);
                game = null;
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
    }
}


