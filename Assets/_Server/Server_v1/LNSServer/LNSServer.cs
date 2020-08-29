using System;
using System.Collections.Generic;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

public class LNSServer : IDisposable
{
    public int port { get; private set; }
    public string key { get; private set; }

    public Dictionary<int, LNSClient> clients = new Dictionary<int, LNSClient>();
    private List<string> connectedClientIds = new List<string>();

    public Dictionary<string, LNSGame> games = new Dictionary<string, LNSGame>();

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

                lock (thelock)
                {
                    string clientKey = request.Data.GetString();
                    if(clientKey == "RJPROZ_IS_MASTER")
                    {

                    }
                    else if(clientKey != this.key)
                    {
                        NetDataWriter writer = new NetDataWriter(false, 1);
                        writer.Put(LNSConstants.CLIENT_EVT_UNAUTHORIZED_CONNECTION);
                        request.Reject(writer);
                    }
                    else
                    {
                        try
                        {
                            string userid = request.Data.GetString();
                            string displayName = request.Data.GetString();
                            string gameKey = request.Data.GetString();
                            string version = request.Data.GetString();
                            CLIENT_PLATFORM platform = (CLIENT_PLATFORM)request.Data.GetByte();


                            if (string.IsNullOrEmpty(gameKey) || !gameKey.Contains("hybriona") || string.IsNullOrEmpty(userid))
                            {
                                NetDataWriter writer = new NetDataWriter();
                                writer.Put(LNSConstants.CLIENT_EVT_UNAUTHORIZED_GAME);
                                request.Reject(writer);
                            }
                            else if (connectedClientIds.Contains(userid))
                            {
                                NetDataWriter writer = new NetDataWriter(false, 1);
                                writer.Put(LNSConstants.CLIENT_EVT_USER_ALREADY_CONNECTED);
                                request.Reject(writer);
                            }
                            else
                            {
                                NetPeer peer = request.Accept();
                                LNSClient client = null;
                                if (!clients.ContainsKey(peer.Id))
                                {
                                    client = new LNSClient(peer);
                                    clients.Add(peer.Id, client);
                                }
                                else
                                {
                                    client = clients[peer.Id];
                                    client.peer = peer;
                                }
                                client.networkid = peer.Id;
                                client.id = userid;
                                client.displayname = displayName;
                                client.gameKey = gameKey;
                                client.gameVersion = version;
                                client.platform = platform;
                                connectedClientIds.Add(userid);
                                Debug.Log("Connected : " + peer.Id + " | Total clients: " + clients.Count);
                            }

                        }
                        catch
                        {
                            NetDataWriter writer = new NetDataWriter(false, 1);
                            writer.Put(LNSConstants.CLIENT_EVT_SERVER_EXECEPTION);
                            request.Reject(writer);
                        }

                    }

                  
                }
                
            };

            //listener.PeerConnectedEvent += peer =>
            //{
            //    lock (thelock)
            //    {
            //        try
            //        {
                                           
                       
            //        }
            //        catch(System.Exception ex)
            //        {
            //            Debug.LogError(ex.Message + " - "+ex.StackTrace);
            //        }
            //    }
               
             
            //};

            listener.NetworkReceiveEvent += (NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod) =>
            {
                int peerId = peer.Id;
                LNSClient client = clients[peerId];
                byte instruction = reader.GetByte();

                if (client.connectedRoom == null)
                {

                    string gameKey = client.gameKey;
                    string version = client.gameVersion;
                    CLIENT_PLATFORM platform = client.platform;



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
                        Debug.Log("Create room");
                        string roomid = reader.GetString();
                        LNSCreateRoomParameters roomParameters = LNSCreateRoomParameters.FromReader(reader);
                        lock (thelock)
                        {
                           
                            if (rooms.ContainsKey(roomid))
                            {
                                client.SendFailedToCreateRoomEvent(ROOM_FAILURE_CODE.ROOM_ALREADY_EXIST); //: Room creation failed
                            }
                            else
                            {
                                LNSRoom room = new LNSRoom(roomid);

                                room.gameKey = gameKey;
                                room.gameVersion = version;
                                room.primaryPlatform = (byte)platform;

                                room.roomParameters = roomParameters;
                              


                                room.assocGame = game;
                                rooms.Add(roomid, room);
                                client.connectedRoom = room;
                                client.SendRoomCreatedEvent(); //: Room created 
                                room.AddPlayer(client);
                               
                            }
                        }
                    }
                    else if (instruction == LNSConstants.SERVER_EVT_CREATE_OR_JOIN_ROOM)
                    {
                           
                        string roomid =  reader.GetString();
                         
                        lock (thelock)
                        {
                            if (!rooms.ContainsKey(roomid))
                            {
                                LNSRoom room = new LNSRoom(roomid);
                                room.gameKey = gameKey;
                                room.gameVersion = version;
                                room.primaryPlatform = (byte)platform;

                                room.roomParameters = new LNSCreateRoomParameters();

                                room.assocGame = game;
                                rooms.Add(roomid, room);
                                client.connectedRoom = room;
                                client.SendRoomCreatedEvent();  //: Room created Event
                                room.AddPlayer(client);
                               

                            }
                            else
                            {

                                LNSRoom room = rooms[roomid];
                                if (room.gameVersion != client.gameVersion)
                                {
                                    client.SendRoomFailedToJoinEvent(ROOM_FAILURE_CODE.VERSION_MISMATCH);
                                }
                                else if (!room.isOpen)
                                {
                                    client.SendRoomFailedToJoinEvent(ROOM_FAILURE_CODE.ROOM_LOCKED); // Room join failed
                                }
                                else if (room.playerCount < room.roomParameters.maxPlayers)
                                {
                                    client.SendRoomFailedToJoinEvent(ROOM_FAILURE_CODE.ROOM_FULL);
                                }
                              
                                else
                                {
                                    client.connectedRoom = room;
                                    client.SendRoomJoinedEvent();//: Room joined
                                    room.AddPlayer(client);

                                    
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
                                    client.SendRoomFailedToJoinEvent(ROOM_FAILURE_CODE.VERSION_MISMATCH);
                                }
                                else if ((room.hasPassword && password == null) || (room.hasPassword && room.roomParameters.password != password))
                                {
                                    client.SendRoomFailedToJoinEvent(ROOM_FAILURE_CODE.PASSWORD_MISMATCH);
                                }
                                else if (!room.isOpen)
                                {
                                    client.SendRoomFailedToJoinEvent(ROOM_FAILURE_CODE.ROOM_LOCKED);
                                }
                                else if (room.playerCount >= room.roomParameters.maxPlayers)
                                {
                                    client.SendRoomFailedToJoinEvent(ROOM_FAILURE_CODE.ROOM_FULL);
                                }

                                else
                                {
                                    client.connectedRoom = room;
                                    client.SendRoomJoinedEvent(); //: Room Joined
                                    room.AddPlayer(client);
                                    
                                }
                            }
                            else
                            {
                                client.SendRoomFailedToJoinEvent(ROOM_FAILURE_CODE.ROOM_DOESNT_EXIST); // Room join failed
                            }

                        }
                    }
                    else if (instruction == LNSConstants.SERVER_EVT_JOIN_RANDOM_ROOM)
                    {

                        LNSJoinRoomFilter filter = LNSJoinRoomFilter.FromReader(reader);
                        lock (thelock)
                        {
                            bool found = false;
                            foreach(var roomKV in rooms)
                            {
                                LNSRoom room = roomKV.Value;
                                //Debug.Log("is room param null " + (room.roomParameters == null));
                                if(room.roomParameters.isPublic && room.gameVersion == client.gameVersion && !room.hasPassword && room.isOpen && room.playerCount < room.roomParameters.maxPlayers)
                                {
                                    if(filter == null || room.IsFilterMatch(filter))
                                    {
                                        client.connectedRoom = room;
                                        client.SendRoomJoinedEvent(); //: Room Joined
                                        room.AddPlayer(client);
                                        found = true;
                                        break;
                                    }
                                }
                            }

                            if (!found)
                            {
                                client.SendRoomFailedToRandomJoin(); // Room join failed
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

                                if (room.playerCount >= room.roomParameters.maxPlayers)
                                {
                                    client.SendRoomFailedToReJoinEvent(ROOM_FAILURE_CODE.ROOM_FULL);
                                }
                                else if (!room.CanReconnect(client))
                                {
                                    client.SendRoomFailedToReJoinEvent(ROOM_FAILURE_CODE.REJOIN_NOT_AUTHORIZED);
                                }
                                else
                                {
                                    client.connectedRoom = room;
                                    client.SendRoomReJoinedEvent(); //: Room ReJoined
                                    room.AddPlayer(client);
                                    
                                }
                            }
                            else
                            {
                                client.SendRoomFailedToReJoinEvent(ROOM_FAILURE_CODE.ROOM_DOESNT_EXIST); // Room join failed
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
                        LNSClient client = clients[peerId];
                        client.SendDisconnectEvent(false);
                        if (connectedClientIds.Contains(client.id))
                        {
                            connectedClientIds.Remove(client.id);
                        }
                        client.Dispose();
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
            Debug.Log("Removing Game: " + game.gameKey);
            if(games.ContainsKey(game.gameKey))
            {
                games.Remove(game.gameKey);
                game = null;
            }

            Debug.Log("Total Games :" + games.Count);
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


