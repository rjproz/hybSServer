
using System.Collections.Generic;

public class LNSConstants
{
    public const byte SERVER_EVT_FETCH_ROOM_LIST = 49;
    public const byte SERVER_EVT_CREATE_ROOM = 50;
    public const byte SERVER_EVT_CREATE_OR_JOIN_ROOM = 51;
    public const byte SERVER_EVT_JOIN_ROOM = 52;
    public const byte SERVER_EVT_LEAVE_ROOM = 53;
    public const byte SERVER_EVT_REJOIN_ROOM = 54;
    public const byte SERVER_EVT_JOIN_RANDOM_ROOM = 55;
    public const byte SERVER_EVT_MAKE_ME_MASTERCLIENT = 56;


    public const byte SERVER_EVT_LOCK_ROOM = 60;
    public const byte SERVER_EVT_UNLOCK_ROOM = 61;
    public const byte SERVER_EVT_RAW_DATA_NOCACHE = 62;
    public const byte SERVER_EVT_RAW_DATA_CACHE = 63;
    public const byte SERVER_EVT_RAW_DATA_TO_CLIENT = 64;
    public const byte SERVER_EVT_REMOVE_CLIENT_CACHE = 65;
    public const byte SERVER_EVT_REMOVE_ALL_CACHE = 66;
    public const byte SERVER_EVT_RAW_DATA_TO_NEARBY_CLIENTS = 67;



    public const byte CLIENT_EVT_ROOM_RAW = 0;
    public const byte CLIENT_EVT_ROOM_PLAYER_CONNECTED = 1;
    public const byte CLIENT_EVT_ROOM_PLAYER_DISCONNECTED = 2;
    public const byte CLIENT_EVT_ROOM_MASTERCLIENT_CHANGED = 3;
    public const byte CLIENT_EVT_ROOM_DISCONNECTED = 4;
    public const byte CLIENT_EVT_ROOM_CACHE_DATA = 5;

    public const byte CLIENT_EVT_ROOM_LIST = 9;
    public const byte CLIENT_EVT_ROOM_CREATED = 10;
    public const byte CLIENT_EVT_ROOM_JOINED = 11;
    public const byte CLIENT_EVT_ROOM_FAILED_CREATE = 12;
    public const byte CLIENT_EVT_ROOM_FAILED_JOIN = 13;
    public const byte CLIENT_EVT_ROOM_REJOINED = 14;
    public const byte CLIENT_EVT_ROOM_FAILED_REJOIN = 15;
    public const byte CLIENT_EVT_ROOM_FAILED_RANDOM_JOIN = 16;

    public const byte CLIENT_EVT_SERVER_EXECEPTION = 91;
    public const byte CLIENT_EVT_UNAUTHORIZED_CONNECTION = 92;
    public const byte CLIENT_EVT_UNAUTHORIZED_GAME = 93;
    public const byte CLIENT_EVT_USER_ALREADY_CONNECTED = 94;


}

[System.Serializable]
public class LNSRoomList
{
    public List<RoomData> list = new List<RoomData>();

    [System.Serializable]
    public class RoomData
    {
        public string id;
        public bool hasPassword;
        public int playerCount;
        public int maxPlayers;

    }
}

public enum ROOM_FAILURE_CODE
{
    ROOM_FULL = 0,
    ROOM_LOCKED = 1,
    ROOM_DOESNT_EXIST = 2,
    PASSWORD_MISMATCH = 3,
    REJOIN_NOT_AUTHORIZED = 4,
    VERSION_MISMATCH = 5,
    ROOM_ALREADY_EXIST = 6,
    UNAUTHORIZED_APP = 7,
    ROOM_WITH_SPECIFIED_FILTER_DOESNT_EXIST = 8
};


public enum CLIENT_PLATFORM
{
    UNITY_EDITOR = 0,
    DESKTOP_WINDOWS = 1,
    DESKTOP_MACOS = 2,
    DESKTOP_LINUX = 3,
    IOS = 4,
    ANDROID = 5
};

public enum CONNECTION_FAILURE_CODE
{
    UNKNOWN_ERROR = 0,
    SERVER_EXECPTION = 1,
    COULD_NOT_CONNECT_TO_HOST = 2,
    CONNECTION_IS_NOT_AUTHORIZED = 3,
    GAME_IS_NOT_AUTHORIZED = 4,
    USER_IS_ALREADY_CONNECTED = 5
}


