# hybSServer: Simple Multiplayer Server for Unity

The UDP server is being build using [LiteNetLib](https://github.com/RevenantX/LiteNetLib). 
Websocket server is being build using [SimpleWebTransport] https://github.com/MirrorNetworking/SimpleWebTransport.
Websocket server is used to server Webgl clients.

Also, **hybSClient** repo can be found [here](https://github.com/rjproz/hybSClient)

## Features
1. Server key to avoid unauthorized clients
2. Theoretically, Unlimited Games and unlimited rooms in one server instance
3. Both Reliable and UnReliable Events
4. Room supports various parameters like game version, password, public/private mode and lock/unlock.
5. 2D QuadTree optimization for partially authoritative MMO or Battle Royale
### Linux Usage

```
./<linux_build_name>/hybsserver.x86_64 -gameserverport <port> -statserverport <port> -serverkey <key>
```

### Windows Usage

```
./<hybsserver_win>/HybSServer.exe -gameserverport <port> -statserverport <port> -serverkey <key>
```  

### Mac Usage

```
<mac_app_name>.app/Contents/MacOS/hybsserver_mac -gameserverport <port> -statserverport <port> -serverkey <key>
``` 
