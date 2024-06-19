using System;
using System.Collections.Generic;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;
using Mirror.SimpleWeb;
using UnityEngine;

public class LNSServer : IDisposable
{
    public int port { get; private set; }
    public string key { get; private set; }
    public int serverTick { get; private set; }
    public int threadWaitMilliseconds { get; private set; }

    public Dictionary<int, LNSClient> clients = new Dictionary<int, LNSClient>();
    public Dictionary<int, LNSClient> clients_webgl = new Dictionary<int, LNSClient>();
    private List<string> connectedClientIds = new List<string>();

    public Dictionary<string, LNSGame> games = new Dictionary<string, LNSGame>();

    public static SimpleWebServer webSocketServer;
    //public static HybWebSocketServer webSocketServer;
    public object thelock = new object();

    private bool disposed;
    

    string logFilePath;
    public LNSServer(int port,string key,int serverTick)
    {
        this.port = port;
        this.key = key;
        this.serverTick = serverTick;

        threadWaitMilliseconds =  Mathf.RoundToInt(1000f / (float)this.serverTick);
        logFilePath = string.Format("Server_LOG_{0}.txt", this.port);

        int i = 0;
        try
        {
            while (i < 1000)
            {
                if(i == 5)
                {
                    break;
                }
                i++;
            }
        }
        finally
        {
            Debug.Log("Finally outside while (i) is " + i);
        }
    }

    ~ LNSServer()
    {
        Dispose();
        GC.SuppressFinalize(this);
    }

    public void Log(string data)
    {
        System.IO.File.AppendAllText(logFilePath, System.DateTime.Now+"\n"+data+"\n\n");
    }

    public void Log(string data,LNSClient client)
    {
        System.IO.File.AppendAllText(logFilePath, System.DateTime.Now + "\n" + data + string.Format(" by {0} {1} {2} {3}", client.gameKey, client.displayname, client.platform, client.id) +  "\n\n");
    }

    public void Start()
    {
        new Thread(() =>
        {
            var tcpConfig = new TcpConfig(true, 120 * 1000, 120 * 1000); //120 seconds

            bool isSSL = true;

            List<string> hostnames = new List<string>();
#if UNITY_EDITOR_OSX
            isSSL = false;
            hostnames.Add("localhost");
#else
            hostnames.Add("localhost");
            hostnames.Add("vps.hybriona.com");
#endif
            isSSL = false;
            SslConfig sslConfig = new SslConfig(isSSL, "cert.pfx", "rjproz",System.Security.Authentication.SslProtocols.Tls12);

            System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            webSocketServer = new SimpleWebServer(10000, tcpConfig, 16 * 1024,3000, sslConfig) ;
            //if (isSSL)
            //{
            //    webSocketServer = new HybWebSocketServer("cert.pfx", "rjproz");
            //}
            //else
            //{
            //    webSocketServer = new HybWebSocketServer("", "rjproz");
            //}



            webSocketServer.onConnect += (connectionId)=> {
                LNSClient client = null;
                Debug.Log("webSocketServer Connected  : " + connectionId);
                if (!clients_webgl.ContainsKey(connectionId))
                {
                    client = LNSClient.CreateWebGlClient();
                    client.networkid = connectionId;
                    clients_webgl.Add(connectionId, client);
                }
                else
                {
                    client = clients_webgl[connectionId];
                    client.networkid = connectionId;
                }
                

                string msg = "I am RJproz\n\n";
                
                webSocketServer.SendOne(client.networkid, new ArraySegment<byte>(msg.ConvertToBytes()));
                
            };

            webSocketServer.onData += (connectionId, data) =>
            {
              
                LNSClient client = clients_webgl[connectionId];

                LNSReader reader = LNSReader.GetFromPool();
                reader.SetSource(data.Array, data.Offset, data.Count);
                var instruction = reader.GetByte();
                if (instruction == LNSConstants.SERVER_EVT_VERIFY_CLIENT)
                {
                    //Debug.Log("webSocketServer SERVER_EVT_VERIFY_CLIENT");

                    try
                    {
                        string clientKey = reader.GetString();
                        string userid = reader.GetString();



                        string displayName = reader.GetString();
                        string gameKey = reader.GetString();
                        string version = reader.GetString();
                        CLIENT_PLATFORM platform = (CLIENT_PLATFORM)reader.GetByte();


                        client.id = userid;
                        client.displayname = displayName;
                        client.gameKey = gameKey;
                        client.gameVersion = version;
                        client.platform = platform;


                        bool valid = !connectedClientIds.Contains(userid);
                        //LNSConstants.CLIENT_EVT_UNAUTHORIZED_GAME
                        //LNSConstants.CLIENT_EVT_USER_ALREADY_CONNECTED
                        if (valid)
                        {
                            connectedClientIds.Add(userid);
                            Debug.Log("Connected : " + connectionId + "| User ID: " + userid + " | GameKey: " + gameKey + " | Total clients: " + clients.Count + " | Total Web Client: " + clients_webgl.Count);
                            byte[] verifiedMsg = new byte[1];
                            verifiedMsg[0] = LNSConstants.CLIENT_EVT_VERIFIED;
                            webSocketServer.SendOne(client.networkid, new ArraySegment<byte>(verifiedMsg));
                        }
                        else
                        {
                            client.Dispose();
                            clients_webgl.Remove(connectionId);
                            webSocketServer.KickClient(connectionId);
                        }
                        

                      
                    }
                    catch {
                        
                        client.Dispose();
                        clients_webgl.Remove(connectionId);
                        webSocketServer.KickClient(connectionId);
                    }
                    finally
                    {
                        reader.Recycle();
                    }
                }
                else
                {

                    OnDataReceiveProcess(client, instruction, reader);
                }

            };


            webSocketServer.onDisconnect += (connectionId) =>
            {
                lock (thelock)
                {
                    int networkId = connectionId;
                    if (clients_webgl.ContainsKey(networkId))
                    {
                        LNSClient client = clients_webgl[networkId];
                        client.SendDisconnectEvent(false);
                        if (connectedClientIds.Contains(client.id))
                        {
                            connectedClientIds.Remove(client.id);
                        }
                        client.Dispose();
                        clients_webgl.Remove(networkId);
                        Debug.Log("Disconnected  : " + networkId);
                    }
                }
                
            };

            /*
            webSocketServer.onError += (connectionId, exception)=>
            {
                Debug.LogError($"webSocketServerError: {connectionId} and error: {exception.Message} {exception.StackTrace}");
            };*/

            //webSocketServer.Start();
            webSocketServer.Start((ushort)(this.port + 2));
            Debug.Log("WebSocket Server Started");

            EventBasedNetListener listener = new EventBasedNetListener();
            NetManager server = new NetManager(listener);
            server.Start(this.port);
            Debug.Log("UDP Server Started");
            listener.ConnectionRequestEvent += request =>
            {
                //Debug.Log("connection request recieved");

                lock (thelock)
                {
                    string clientKey = request.Data.GetString();
                    if(clientKey == "RJPROZ_IS_MASTER")
                    {

                    }
#if !UNITY_EDITOR
                    else if(clientKey != this.key)
                    {
                        NetDataWriter writer = new NetDataWriter(false, 1);
                        writer.Put(LNSConstants.CLIENT_EVT_UNAUTHORIZED_CONNECTION);
                        request.Reject(writer);
                    }
#endif
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
                                Debug.Log("Connected : " + client.networkid + "| User ID: " + userid + " | GameKey: " + gameKey + " | Total clients: " + clients.Count + " | Total Web Client: " + clients_webgl.Count);

                                Log(string.Format("Connected {0} {1} {2} {3}", gameKey, displayName, platform, userid));
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

           
           

            listener.NetworkReceiveEvent += (NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod) =>
            {
                int peerId = peer.Id;
                LNSClient client = clients[peerId];
                byte instruction = reader.GetByte();

                LNSReader reader1 = LNSReader.GetFromPool();
                reader1.SetSource(reader.GetRemainingBytes());// reader.RawData,reader.Position,reader.AvailableBytes);
                OnDataReceiveProcess(client, instruction, reader1,deliveryMethod);
                reader1.Recycle();
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
                webSocketServer.ProcessMessageQueue();
                Thread.Sleep(threadWaitMilliseconds); // 30 times per second
            }
        }).Start();
    }


    public void OnDataReceiveProcess(LNSClient client,byte instruction,LNSReader reader, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
    {
        if (client.connectedRoom == null)
        {

            string gameKey = client.gameKey;
            string version = client.gameVersion;
            CLIENT_PLATFORM platform = client.platform;



            LNSGame game = null;
            if (games.ContainsKey(gameKey))
            {
                game = games[gameKey];
            }
            else
            {
                game = new LNSGame(gameKey, this);
                games.Add(game.gameKey, game);
            }

            Dictionary<string, LNSRoom> rooms = game.rooms;
            if (instruction == LNSConstants.SERVER_EVT_CREATE_ROOM)
            {
                Debug.Log("Create room");
                string roomid = reader.GetString();
                LNSCreateRoomParameters roomParameters = LNSCreateRoomParameters.FromReader(reader);

                lock (thelock)
                {

                    if (rooms.ContainsKey(roomid.ToLower()))
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
                        room.Prepare();

                        rooms.Add(roomid.ToLower(), room);
                        client.connectedRoom = room;
                        client.SendRoomCreatedEvent(); //: Room created 
                        room.AddPlayer(client);


                        Log("Room created " + room.id, client);

                    }
                }
            }
            else if (instruction == LNSConstants.SERVER_EVT_CREATE_OR_JOIN_ROOM)
            {
                //Debug.Log("Create SERVER_EVT_CREATE_OR_JOIN_ROOM");
                string roomid = reader.GetString();
                int maxPlayers = reader.GetInt();
                lock (thelock)
                {
                    if (!rooms.ContainsKey(roomid.ToLower()))
                    {
                        LNSRoom room = new LNSRoom(roomid);

                        room.gameKey = gameKey;
                        room.gameVersion = version;
                        room.primaryPlatform = (byte)platform;
                        room.roomParameters = new LNSCreateRoomParameters();
                        room.roomParameters.maxPlayers = maxPlayers;
                        room.assocGame = game;
                        room.Prepare();
                        rooms.Add(roomid.ToLower(), room);
                        client.connectedRoom = room;
                        client.SendRoomCreatedEvent();  //: Room created Event
                        room.AddPlayer(client);

                        Log("Room created " + room.id, client);

                    }
                    else
                    {

                        LNSRoom room = rooms[roomid.ToLower()];
                        if (room.gameVersion != client.gameVersion)
                        {
                            client.SendRoomFailedToJoinEvent(ROOM_FAILURE_CODE.VERSION_MISMATCH);
                        }
                        else if (!room.isOpen)
                        {
                            client.SendRoomFailedToJoinEvent(ROOM_FAILURE_CODE.ROOM_LOCKED); // Room join failed
                        }

                        else if (room.playerCount >= room.roomParameters.maxPlayers)
                        {
                            client.SendRoomFailedToJoinEvent(ROOM_FAILURE_CODE.ROOM_FULL);
                        }

                        else
                        {
                            client.connectedRoom = room;
                            client.SendRoomJoinedEvent();//: Room joined
                            room.AddPlayer(client);

                            Log("Room joined " + room.id, client);


                        }
                    }
                }
            }
            else if (instruction == LNSConstants.SERVER_EVT_ROOM_EXIST_QUERY)
            {
                string roomid = reader.GetString();
                if (rooms.ContainsKey(roomid.ToLower()))
                {
                    client.SendRoomExistResponse(roomid, true);
                }
                else
                {
                    client.SendRoomExistResponse(roomid, false);
                }
            }
            else if (instruction == LNSConstants.SERVER_EVT_JOIN_ROOM)
            {


                string roomid = reader.GetString();
                bool hasPassword = reader.GetBool();
                string password = null;
                if (hasPassword)
                {
                    password = reader.GetString();
                }


                lock (thelock)
                {
                    if (rooms.ContainsKey(roomid.ToLower()))
                    {
                        LNSRoom room = rooms[roomid.ToLower()];

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

                            Log("Room joined " + room.id, client);

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
                    foreach (var roomKV in rooms)
                    {
                        LNSRoom room = roomKV.Value;
                        //Debug.Log("is room param null " + (room.roomParameters == null));
                        if (room.roomParameters.isPublic && room.gameVersion == client.gameVersion && !room.hasPassword && room.isOpen && room.playerCount < room.roomParameters.maxPlayers)
                        {
                            if (filter == null || room.IsFilterMatch(filter))
                            {
                                client.connectedRoom = room;
                                client.SendRoomJoinedEvent(); //: Room Joined
                                room.AddPlayer(client);

                                Log("Room random joined " + room.id, client);

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

                string roomid = reader.GetString();

                lock (thelock)
                {
                    if (rooms.ContainsKey(roomid.ToLower()))
                    {
                        LNSRoom room = rooms[roomid.ToLower()];

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
                            Log("Room rejoined " + room.id, client);

                        }
                    }
                    else
                    {
                        client.SendRoomFailedToReJoinEvent(ROOM_FAILURE_CODE.ROOM_DOESNT_EXIST); // Room join failed
                    }

                }
            }
            else if (instruction == LNSConstants.SERVER_EVT_FETCH_ROOM_LIST)
            {
                LNSJoinRoomFilter filter = LNSJoinRoomFilter.FromReader(reader);

                lock (thelock)
                {
                    LNSRoomList roomList = new LNSRoomList();
                    int counter = 0;
                    foreach (var roomKV in rooms)
                    {
                        LNSRoom room = roomKV.Value;
                        //Debug.Log("is room param null " + (room.roomParameters == null));
                        if (room.roomParameters.isPublic && room.gameVersion == client.gameVersion && room.isOpen && room.playerCount < room.roomParameters.maxPlayers)
                        {
                            if (filter == null || room.IsFilterMatch(filter))
                            {
                                string roomid = room.id;
                                bool roomHasPassword = room.hasPassword;
                                int currentPlayerCount = room.playerCount;
                                int maxPlayers = room.roomParameters.maxPlayers;

                                LNSRoomList.RoomData roomData = new LNSRoomList.RoomData();
                                roomData.id = roomid;
                                roomData.hasPassword = roomHasPassword;
                                roomData.playerCount = currentPlayerCount;
                                roomData.maxPlayers = maxPlayers;
                                roomList.list.Add(roomData);
                                counter++;
                                if (counter >= 100)
                                {
                                    break;
                                }
                            }
                        }
                    }

                    client.SendRoomList(roomList);
                    roomList.list = null;
                    roomList = null;

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
                client.connectedRoom.ProcessReceivedData(client, instruction, reader, deliveryMethod);
            }
        }
       
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


