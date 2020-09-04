using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ServerStats;

public class LNSServerManager : MonoBehaviour
{
    private int gameServerPort = 10002;
    private int statsServerPort = 10001;
    private string serverKey = "demokey";
    private LNSServer server_1;
    IEnumerator Start()
    {
        Application.targetFrameRate = 10;
       

        string [] commandLineArgs = System.Environment.GetCommandLineArgs();

        
        for (int i = 0; i < commandLineArgs.Length; i++)
        {
            
            if (commandLineArgs[i].ToLower() == "-gameserverport")
            {
                i++;
                if (i >= commandLineArgs.Length ||  !int.TryParse(commandLineArgs[i],out gameServerPort))
                {
                    LogCommandlineParamsError();
                    yield break;
                }
                
            }
            else if (commandLineArgs[i].ToLower() == "-statsserverport")
            {
                i++;
                if (i >= commandLineArgs.Length || !int.TryParse(commandLineArgs[i], out statsServerPort))
                {
                    LogCommandlineParamsError();
                    yield break;
                }
            }
            else if (commandLineArgs[i].ToLower() == "-serverkey")
            {
                i++;
                if (i >= commandLineArgs.Length)
                {
                    LogCommandlineParamsError();
                    yield break;
                }
                else
                {
                    serverKey = commandLineArgs[i];
                }
            }
        }

        HttpServerForStats httpServerForStats = new HttpServerForStats();
        httpServerForStats.Start(this, statsServerPort);

        
      
        server_1 = new LNSServer(gameServerPort, serverKey);
        server_1.Start();
        Debug.Log("Server key is "+ serverKey);

        WaitForSeconds waitForSeconds = new WaitForSeconds(120);
        while (true)
        {
            yield return waitForSeconds;
            System.GC.Collect();
        }
    }

    public void LogCommandlineParamsError()
    {
        Debug.LogError("\n\n<server build file> -gameserverport <game port> -statsserverport <stat port> -serverkey <secure server key>\n\n");
        Application.Quit();
    }
    public ServerStats GetData()
    {
        LNSServer _server = server_1;
        lock (_server.thelock)
        {
            ServerStats o = new ServerStats();

            foreach (var game in _server.games)
            {
                GameStats gameStats = new GameStats();
                gameStats.gameid = game.Key;
                gameStats.totalRooms = game.Value.rooms.Count;

                foreach (var room in game.Value.rooms)
                {
                    gameStats.totalClients += room.Value.playerCount;
                }
                o.games.Add(gameStats);
            }
            o.totalGames = _server.games.Count;
            o.totalClients = _server.clients.Count;
            return o;
        }
    }
    public void OnDisable()
    {
        server_1.Dispose();
    }

}

[System.Serializable]
public class ServerStats
{
    public int totalClients;
    public int totalGames;
    public int totalRooms;
    public List<GameStats> games = new List<GameStats>();

    [System.Serializable]
    public class GameStats
    {
        public string gameid;
        public int totalClients;
        public int totalRooms;

        //public RoomStats gameStats = new RoomStats();
    }

    [System.Serializable]
    public class RoomStats
    {
        public int totalClients;
    }
}
