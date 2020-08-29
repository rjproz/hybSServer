using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ServerStats;

public class LNSServerManager : MonoBehaviour
{
    // Start is called before the first frame update
    private LNSServer server_1;
    IEnumerator Start()
    {
        WaitForSeconds waitForSeconds = new WaitForSeconds(120);
        Application.targetFrameRate = 10;
        server_1 = new LNSServer(10002,"iamatestserver");
        server_1.Start();
        Debug.Log("Server key is iamatestserver");

        while(true)
        {
            yield return waitForSeconds;
            System.GC.Collect();
        }
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
