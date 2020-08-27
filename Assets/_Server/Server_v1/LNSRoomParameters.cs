using System;
using System.Collections;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

public class LNSCreateRoomParameters
{
    public bool isPublic { get; set; } = true;
    public string password { get; set; } = null;
    public int maxPlayers { get; set; } = 10;
    public LNSJoinRoomFilter filters { get; set; }

    public void AppendToWriter(NetDataWriter writer)
    {

        writer.Put(isPublic);
        if (string.IsNullOrEmpty(password))
        {
            writer.Put(false);
        }
        else
        {
            writer.Put(true);
            writer.Put(password);
        }

        if (filters != null)
        {
            filters.AppendToWriter(writer);
        }
        else
        {
            writer.Put((byte)0);
        }
        writer.Put(maxPlayers);
    }

    public static LNSCreateRoomParameters FromReader(NetPacketReader reader)
    {
        if (reader.AvailableBytes > 0)
        {
            LNSCreateRoomParameters o = new LNSCreateRoomParameters();
            o.isPublic = reader.GetBool();
            if (reader.GetBool())
            {
                o.password = reader.GetString();
            }


            o.filters = LNSJoinRoomFilter.FromReader(reader);


            o.maxPlayers = reader.GetInt();
            return o;

        }
        return null;
    }
}


public class LNSJoinRoomFilter
{
    private Dictionary<byte, byte> filters = new Dictionary<byte, byte>();

    public void Reset()
    {
        filters.Clear();
    }

    public bool Set(byte key, byte value)
    {
        if (filters.Count == 256)
        {
            return false;
        }
        if (filters.ContainsKey(key))
        {
            filters[key] = value;
        }
        else
        {
            filters.Add(key, value);
        }

        return true;
    }

    public byte GetLength()
    {
        return (byte)filters.Count;
    }

    public void AppendToWriter(NetDataWriter writer)
    {
        writer.Put((byte)filters.Count);
        foreach (var filter in filters)
        {
            writer.Put(filter.Key);
            writer.Put(filter.Value);
        }
    }

    public static LNSJoinRoomFilter FromReader(NetPacketReader reader)
    {
        int filterCount = reader.GetByte();
        if (filterCount > 0)
        {
            LNSJoinRoomFilter o = new LNSJoinRoomFilter();
            for (int i = 0; i < filterCount; i++)
            {
                o.Set(reader.GetByte(), reader.GetByte());
            }
            return o;
        }
        return null;
    }

    public bool IsFilterMatch(LNSJoinRoomFilter source)
    {
        foreach(var sourceFilter in source.filters)
        {
            if(!filters.ContainsKey(sourceFilter.Key) || filters[sourceFilter.Key] != sourceFilter.Value)
            {
                return false;
            }
        }
        return true;
    }
}
