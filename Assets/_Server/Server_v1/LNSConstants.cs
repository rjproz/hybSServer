using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LNSConstants 
{
    public const byte SERVER_EVT_CREATE_ROOM = 50;
    public const byte SERVER_EVT_CREATE_OR_JOIN_ROOM = 51;
    public const byte SERVER_EVT_JOIN_ROOM = 52;
    public const byte SERVER_EVT_LEAVE_ROOM = 53;
    public const byte SERVER_EVT_REJOIN_ROOM = 54;

    public const byte SERVER_EVT_LOCK_ROOM = 60;
    public const byte SERVER_EVT_UNLOCK_ROOM = 61;
    public const byte SERVER_EVT_RAW_DATA_NOCACHE = 62;
    public const byte SERVER_EVT_RAW_DATA_CACHE = 63;
    


    public const byte CLIENT_EVT_ROOM_RAW = 0;
    public const byte CLIENT_EVT_ROOM_PLAYER_CONNECTED = 1;
    public const byte CLIENT_EVT_ROOM_PLAYER_DISCONNECTED = 2;
    public const byte CLIENT_EVT_ROOM_MASTERCLIENT_CHANGED = 3;
    public const byte CLIENT_EVT_ROOM_DISCONNECTED = 4;

    public const byte CLIENT_EVT_ROOM_CREATED = 10;
    public const byte CLIENT_EVT_ROOM_JOINED = 11;
    public const byte CLIENT_EVT_ROOM_FAILED_CREATE = 12;
    public const byte CLIENT_EVT_ROOM_FAILED_JOIN = 13;
    public const byte CLIENT_EVT_ROOM_REJOINED = 14;
    public const byte CLIENT_EVT_ROOM_FAILED_REJOIN = 15;

    public enum ROOM_FAILURE_CODE {ROOM_FULL = 0 ,ROOM_LOCKED = 1,ROOM_DOESNT_EXIST = 2,PASSWORD_MISMATCH = 3, REJOIN_NOT_AUTHORIZED = 4,VERSION_MISMATCH = 5,ROOM_ALREADY_EXIST = 6, UNAUTHORIZED_APP = 7};
    public enum CLIENT_PLATFORM { UNITY_EDITOR = 0,DESKTOP_WINDOWS = 1, DESKTOP_MACOS = 2, DESKTOP_LINUX, IOS = 3, ANDROID = 4 };
}


