﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LNSGame : IDisposable
{
    public string gameKey;
    public LNSServer assocServer;
    public Dictionary<string, LNSRoom> rooms { get; private set; } = new Dictionary<string, LNSRoom>();

    public LNSGame(string gameKey, LNSServer assocServer)
    {
        this.gameKey = gameKey;
        this.assocServer = assocServer;
    }

    ~LNSGame()
    {
        Dispose();
        GC.SuppressFinalize(this);
    }


    public void RemoveRoom(LNSRoom room)
    {
        lock (assocServer.thelock)
        {
            if (rooms.ContainsKey(room.id))
            {
                rooms.Remove(room.id);
                room.Dispose();
            }
        }
        Debug.LogFormat("Total Rooms at {1} is {0} : ",gameKey,rooms.Count);

        if(rooms.Count == 0)
        {
            assocServer.RemoveGame(gameKey);
        }
    }

    

    public void Dispose()
    {
        rooms = null;
    }
}