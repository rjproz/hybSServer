using System.Collections.Generic;
using UnityEngine;
using LiteNetLib.Utils;
public class LNSReader : NetDataReader
{


   

    public Vector2 GetVector2()
    {
        Vector2 v = Vector2.zero;
        v.x = base.GetFloat();
        v.y = base.GetFloat();
        return v;
    }

    public Vector3 GetVector3()
    {
        Vector3 v = Vector3.zero;
        v.x = base.GetFloat();
        v.y = base.GetFloat();
        v.z = base.GetFloat();

        return v;
    }

    public Vector4 GetVector4()
    {
        Vector4 v = Vector4.zero;
        v.x = base.GetFloat();
        v.y = base.GetFloat();
        v.z = base.GetFloat();
        v.w = base.GetFloat();

        return v;
    }

    public Quaternion GetQuaternion()
    {
        Quaternion quaternion = Quaternion.identity;
        quaternion.x = base.GetFloat();
        quaternion.y = base.GetFloat();
        quaternion.z = base.GetFloat();
        quaternion.w = base.GetFloat();
        return quaternion;
    }

    public Color GetColor()
    {
        Color color = Color.clear;
        color.r = base.GetFloat();
        color.g = base.GetFloat();
        color.b = base.GetFloat();

        return color;
    }


    //Pool manager

    public void Recycle()
    {
        PutIntoPool(this);
    }

    private static Queue<LNSReader> pool = new Queue<LNSReader>();
    private static object theLock = new object();

    public static LNSReader GetFromPool()
    {
        lock (theLock)
        {
            if (pool.Count > 0)
            {
                return pool.Dequeue();
            }
            return new LNSReader();
        }
    }

    protected static void PutIntoPool(LNSReader reader)
    {
        lock (theLock)
        {
            pool.Enqueue(reader);
        }
    }
}
