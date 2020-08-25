using System;
using System.Collections.Generic;
using System.Text;
public class Packet : IDisposable
{
    public System.Action<byte []> onCompletePacketReceived;
    public System.Action<byte[]> onCompleteRawReceived;
    List<byte> raw = new List<byte>();
    List<byte> buffer = new List<byte>();
    private int headerPos = 0;
    private int readPos = 0;
    private int contentLength = 0;

    private byte[] readableBuffer;
    public void Dispose()
    {
        buffer.Clear();
        raw.Clear();
        buffer = null;
        readableBuffer = null;
        GC.SuppressFinalize(this);
    }

    public void Reset()
    {
        headerPos = 0;
        readPos = 0;
        contentLength = 0;
        buffer.Clear();
        raw.Clear();

    }

    public void Populate(byte[] bufferReference, int size, int offset = 0)
    {
        int i = 0;
        for (i = offset; i < size; i++)
        {
            //UnityEngine.Debug.Log(System.Convert.ToChar( bufferReference[i] ).ToString());
            if (headerPos == 0)
            {
                contentLength = bufferReference[i] * 256;
                raw.Add(bufferReference[i]);
                headerPos++;
            }
            else if (headerPos == 1)
            {
                contentLength += bufferReference[i];
                raw.Add(bufferReference[i]);
                headerPos++;
            }
            else if (readPos < contentLength)
            {
                buffer.Add(bufferReference[i]);
                raw.Add(bufferReference[i]);
                readPos++;

                if (readPos == contentLength)
                {
                    //Debug.Log(Encoding.ASCII.GetString(buffer.ToArray()));
                    onCompleteRawReceived(raw.ToArray());
                    Reset();
                }
            }
            else if (i < size)
            {
                //read header again
                Populate(bufferReference, size, i);
            }

        }

    }

    
}