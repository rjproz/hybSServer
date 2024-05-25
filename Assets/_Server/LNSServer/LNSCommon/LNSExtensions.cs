using UnityEngine;

public static class LNSExtensions 
{

    public static byte [] ConvertToBytes(this string data)
    {
        return System.Text.Encoding.UTF8.GetBytes(data);
    }

    public static string ConvertToString(this byte [] data)
    {
        return System.Text.Encoding.UTF8.GetString(data,0,data.Length);
    }
}
