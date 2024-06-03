using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class HybWebSocketServer
{
    public Action<int> onConnect;
    public Action<int> onDisconnect;
    public Action<int,ArraySegment<byte>> onData;

    TcpListener tcpListener;
    X509Certificate2 sslCertificate;
    string protocol;
    private static readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    static int connectionIdCounter = 0;
    const int bufferSize = 16 * 1024;

    static Dictionary<int,(TcpClient client,Stream stream, SemaphoreSlim writeSemaphore)> connectedClients = new Dictionary<int, (TcpClient, Stream, SemaphoreSlim)>();
    public HybWebSocketServer(string sslPath,string sslPassword)
    {
        


        if (string.IsNullOrEmpty(sslPath))
        {
            protocol = "http";
            sslCertificate = null;
           
        }
        else
        {
            protocol = "https";
            sslCertificate = new X509Certificate2(sslPath, sslPassword,X509KeyStorageFlags.MachineKeySet);
           // httpListener.AuthenticationSchemeSelectorDelegate = (sender, host) => sslCertificate;
        }
        

    }

    public void Start(int port)
    {
        tcpListener = new TcpListener(System.Net.IPAddress.Any, port);
        tcpListener.Start();
        Process();
    }

    async Task Process()
    {
        while (!cancellationTokenSource.Token.IsCancellationRequested)
        {
            while (true)
            {
                var tcpClient = await tcpListener.AcceptTcpClientAsync();
                _ = HandleClientAsync(tcpClient); // Handle each client in a new task
            }
        }

        tcpListener.Stop();
    }

    async Task HandleClientAsync(TcpClient tcpClient)
    {
        Stream stream = null;
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            
            if (sslCertificate != null)
            {
                var sslStream = new SslStream(tcpClient.GetStream(), false);
                await sslStream.AuthenticateAsServerAsync(sslCertificate, clientCertificateRequired: false, checkCertificateRevocation: false);
                stream = sslStream;
            }
            else
            {
                stream = tcpClient.GetStream();
            }

            int bytesRead = 0;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
            {

                var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                if (IsWebSocketRequest(request))
                {
                    var response = CreateWebSocketHandshakeResponse(request);
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(response), 0, response.Length);
                    HandleWebSocketAsync(tcpClient, stream);
                    
                }
                else
                {
                    Debug.LogError("Invalid WebSocket request");
                    tcpClient.Close();
                    tcpClient.Dispose();
                    
                }
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception: {ex}");
            tcpClient.Close();
            tcpClient.Dispose();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }


    private  bool IsWebSocketRequest(string request)
    {
        return request.Contains("Upgrade: websocket");
    }

    private string CreateWebSocketHandshakeResponse(string request)
    {
        var key = new System.Text.RegularExpressions.Regex("Sec-WebSocket-Key: (.*)").Match(request).Groups[1].Value.Trim();
        var responseKey = Convert.ToBase64String(System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        return "HTTP/1.1 101 Switching Protocols\r\n" +
               "Upgrade: websocket\r\n" +
               "Connection: Upgrade\r\n" +
               $"Sec-WebSocket-Accept: {responseKey}\r\n\r\n";
    }

    private async Task HandleWebSocketAsync(TcpClient client, Stream stream)
    {
        var buffer = new byte[bufferSize];
        int connectionId = ++connectionIdCounter;
        var writeSemaphore = new SemaphoreSlim(1, 1);
        connectedClients.Add(connectionId, (client,stream, writeSemaphore));

        var messageBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        int messageBufferLength = 0;

        var maskingKey = new byte[4];

        if (onConnect != null)
        {
            onConnect(connectionId);
        }

        while (true)
        {
            try
            {
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    if (onDisconnect != null)
                    {
                        onDisconnect(connectionId);
                        connectedClients.Remove(connectionId);
                        stream.Close();
                        stream.Dispose();
                        writeSemaphore.Dispose();
                        client.Close();
                        client.Dispose();
                    }
                    break;
                }

                int offset = 0;
                while (offset < bytesRead)
                {
                    bool fin = (buffer[offset] & 0b10000000) != 0;
                    int opcode = buffer[offset] & 0b00001111;
                    bool masked = (buffer[offset + 1] & 0b10000000) != 0;
                    int payloadLength = buffer[offset + 1] & 0b01111111;

                    offset += 2;

                    if (payloadLength == 126)
                    {
                        payloadLength = BitConverter.ToUInt16(buffer, offset);
                        offset += 2;
                    }
                    else if (payloadLength == 127)
                    {
                        payloadLength = (int)BitConverter.ToUInt64(buffer, offset);
                        offset += 8;
                    }

                   
                    byte[] payload = ArrayPool<byte>.Shared.Rent(payloadLength);

                    try
                    {
                        if (masked)
                        {
                            Array.Copy(buffer, offset, maskingKey, 0, 4);
                            offset += 4;
                        }

                        Array.Copy(buffer, offset, payload, 0, payloadLength);
                        offset += payloadLength;

                        if (masked)
                        {
                            

                            for (int i = 0; i < payloadLength; i++)
                            {
                                payload[i] ^= maskingKey[i % 4];
                            }
                        }

                        if (messageBufferLength + payloadLength > messageBuffer.Length)
                        {
                            var newMessageBuffer = ArrayPool<byte>.Shared.Rent(messageBufferLength + payloadLength);
                            Array.Copy(messageBuffer, newMessageBuffer, messageBufferLength);
                            ArrayPool<byte>.Shared.Return(messageBuffer);
                            messageBuffer = newMessageBuffer;
                        }

                        Array.Copy(payload, 0, messageBuffer, messageBufferLength, payloadLength);
                        messageBufferLength += payloadLength;

                        if (fin)
                        {
                            //string msg = System.Text.Encoding.UTF8.GetString(messageBuffer, 0, messageBufferLength);
                            //Debug.Log($"opcode is {opcode} and msg:{msg}" );

                            if(onData != null)
                            {
                                onData(connectionId, new ArraySegment<byte>(messageBuffer, 0, messageBufferLength));
                            }
                            /*
                            var messageSegment = new ArraySegment<byte>(messageBuffer, 0, messageBufferLength);

                            if (opcode == 1) // Text frame
                            {
                                var message = Encoding.UTF8.GetString(messageSegment.Array, messageSegment.Offset, messageSegment.Count);
                                Debug.Log($"Received text message: {message} with mask? {masked}");
                            }
                            else if (opcode == 2) // Binary frame
                            {
                                Debug.Log($"Received binary message (length: {messageSegment.Count})");
                            }
                            */

                            /*
                            var messageSegment = new ArraySegment<byte>(messageBuffer, 0, messageBufferLength);
                            var frameSegment = CreateWebSocketFrame(messageSegment);

                            // Echo the message back without masking (server side)
                            await st.WriteAsync(frameSegment.Array, frameSegment.Offset, frameSegment.Count);

                            // Return the frame array to the pool after writing it
                            ArrayPool<byte>.Shared.Return(frameSegment.Array);
                            */
                            messageBufferLength = 0;
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(payload);
                    }
                   
                }

               

               
                
               
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex}");
                break;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(messageBuffer);
            }
        }
    }

    private static ArraySegment<byte> CreateWebSocketFrame(ArraySegment<byte> message)
    {
        int headerSize = 2 + (message.Count > 125 ? (message.Count <= ushort.MaxValue ? 2 : 8) : 0);
        int frameSize = headerSize + message.Count;

        byte[] frame = ArrayPool<byte>.Shared.Rent(frameSize);
        int offset = 0;

        frame[offset++] = 0b10000010; // FIN and binary frame opcode

        if (message.Count <= 125)
        {
            frame[offset++] = (byte)message.Count;
        }
        else if (message.Count <= ushort.MaxValue)
        {
            frame[offset++] = 126;
            BitConverter.GetBytes((ushort)IPAddress.HostToNetworkOrder((short)message.Count)).CopyTo(frame, offset);
            offset += 2;
        }
        else
        {
            frame[offset++] = 127;
            BitConverter.GetBytes((ulong)IPAddress.HostToNetworkOrder((long)message.Count)).CopyTo(frame, offset);
            offset += 8;
        }

        message.Array.AsSpan(message.Offset, message.Count).CopyTo(frame.AsSpan(offset));

        return new ArraySegment<byte>(frame, 0, frameSize);
    }

    public async void SendOne(int connectionId,ArraySegment<byte> data)
    {
        var clientInfo = connectedClients[connectionId];
        var frameSegment = CreateWebSocketFrame(data);
        await clientInfo.writeSemaphore.WaitAsync();
        try
        {
            await clientInfo.stream.WriteAsync(frameSegment.Array, frameSegment.Offset, frameSegment.Count);

        }
        finally
        {
            clientInfo.writeSemaphore.Release();
            ArrayPool<byte>.Shared.Return(frameSegment.Array);
        }
       
    }

    public void ProcessMessageQueue()
    {

    }

    public void KickClient(int connectionId)
    {
        var resource = connectedClients[connectionId];
        resource.Item2.Close();
        resource.Item2.Dispose();
        resource.Item1.Dispose();
        connectedClients.Remove(connectionId);

        
        
    }
}
