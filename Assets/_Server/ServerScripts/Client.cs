using System.Net.Sockets;
using UnityEngine;
using System.IO;
using System;

public class Client
{
    public Server server { get; set; }

    public string id { get; private set; }
    public TcpClient socket { get; private set; }
    public Packet packet { get; private set; }
    public bool isFree { get; private set; } = true;
    
    public const int BUFFER_SIZE = 512;


    private byte[] buffer;
    private NetworkStream readStream;
    private NetworkStream writeStream;
    private object writelock = new object();
    public void Init(TcpClient socket)
    {
        isFree = false;
        var rnd = new System.Random();
       
        id = rnd.Next(1000000, 9999999) + "_"+ rnd.Next(1000000, 9999999);
        this.socket = socket;
        this.socket.ReceiveBufferSize = BUFFER_SIZE;
        this.socket.SendBufferSize = BUFFER_SIZE;

        if (buffer == null)
        {
            buffer = new byte[BUFFER_SIZE];
        }

        if (packet == null)
        {
            packet = new Packet();
        }
        packet.Reset();
        packet.onCompleteRawReceived = OnDataReceived;

        readStream = socket.GetStream();
        writeStream = socket.GetStream();
        readStream.BeginRead(buffer, 0, BUFFER_SIZE, new AsyncCallback(ReadCallback), null);
    }

    public void OnDataReceived(byte [] data)
    {
        //for(int i=0;i<data.Length;i++)
        //{
        //    Debug.Log(data[i] + " - "+ (char)data[i]);
        //}
        //send buffer to all connected clients
        server.SendToAllClientExcept(id, data);

    }

    public void SendData(byte [] data)
    {
        try
        {
            
           // writeStream.WriteByte((byte)((int)(data.Length / 256f)));
            //writeStream.WriteByte((byte)((int)(data.Length % 256f)));
            writeStream.BeginWrite(data, 0, data.Length,null,null);
        }catch
        {
            Disconnected();
        }
        //lock (writelock)
        //{
           
        //}
    }

    public void ReadCallback(IAsyncResult result)
    {
        try
        {
            int readed = readStream.EndRead(result);
            if (readed > 0)
            {
                //Debug.Log("readed: " + readed);
                packet.Populate(buffer, readed);

               
                readStream.BeginRead(buffer, 0, BUFFER_SIZE, new AsyncCallback(ReadCallback), null);
            }
            else
            {
                Disconnected();
            }

        }
        catch (System.Exception ex)
        {
            Debug.Log(ex.Message + " - " + ex.StackTrace);
            Disconnected();
        }
    }

    public void Disconnected()
    {
        isFree = true;
        if (socket != null)
        {
            socket.Close();
            socket.Dispose();
        }
    }

    public void Dispose()
    {
        isFree = true;
 
        if (socket != null)
        {
            socket.Close();
            socket.Dispose();
            buffer = null;
        }
    }
}

