using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
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
    public Action<int, ArraySegment<byte>> onData;

    private TcpListener tcpListener;
    private X509Certificate2 sslCertificate;
    private string protocol;
    private static readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    static int connectionIdCounter = 0;
    const int bufferSize = 16 * 1024;

    private static readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Create(bufferSize, Environment.ProcessorCount);
    private readonly Dictionary<int, (TcpClient client, Stream stream, SemaphoreSlim writeSemaphore)> connectedClients = new Dictionary<int, (TcpClient, Stream, SemaphoreSlim)>();
    private readonly object dictionaryLock = new object();

    public HybWebSocketServer(string sslPath, string sslPassword)
    {
        if (string.IsNullOrEmpty(sslPath))
        {
            protocol = "http";
            sslCertificate = null;
        }
        else
        {
            protocol = "https";
            sslCertificate = new X509Certificate2(sslPath, sslPassword, X509KeyStorageFlags.MachineKeySet);
        }
    }

    public int ConnectionCount()
    {
        return connectedClients.Count;
    }

    public Dictionary<int, (TcpClient, Stream, SemaphoreSlim)>.KeyCollection GetConnectionIds()
    {
        return connectedClients.Keys;
    }
    public void Start(int port)
    {
        tcpListener = new TcpListener(IPAddress.Any, port);
        tcpListener.Start();
        _ = Process();
    }

    private async Task Process()
    {
        while (true)
        {
            var tcpClient = await tcpListener.AcceptTcpClientAsync();
            _ = HandleClientAsync(tcpClient); // Handle each client in a new task
            Thread.Sleep(500);
        }
    }

    private async Task HandleClientAsync(TcpClient tcpClient)
    {
        Stream stream = null;
        var buffer = _bufferPool.Rent(bufferSize);
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

            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

            if (bytesRead > 0)
            {
                var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                if (IsWebSocketRequest(request))
                {
                    var response = CreateWebSocketHandshakeResponse(request);
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(response), 0, response.Length);
                    _ = HandleWebSocketAsync(tcpClient, stream);
                }
                else
                {
                    Debug.LogError("Invalid WebSocket request");
                    stream?.Close();
                    stream?.Dispose();
                    tcpClient?.Close();
                    tcpClient?.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception: {ex}");
            stream?.Close();
            stream?.Dispose();
            tcpClient?.Close();
            tcpClient?.Dispose();
        }
        finally
        {
            _bufferPool.Return(buffer);
        }
    }

    private bool IsWebSocketRequest(string request)
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
        var buffer = _bufferPool.Rent(bufferSize);
        int connectionId = Interlocked.Increment(ref connectionIdCounter);
        var writeSemaphore = new SemaphoreSlim(1, 1);
        client.NoDelay = true;
        client.SendTimeout = 5000;
        client.ReceiveTimeout = 20000;
        lock (dictionaryLock)
        {
            connectedClients.Add(connectionId, (client, stream, writeSemaphore));
        }
        onConnect?.Invoke(connectionId);
        

        var messageBuffer = _bufferPool.Rent(bufferSize);
        int messageBufferLength = 0;
        var maskingKey = new byte[4];

        
        
        try
        {
            while (client.Connected)
            {
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
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

                    byte[] payload = _bufferPool.Rent(payloadLength);

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

                        if (opcode == 0x8) // Close frame
                        {
                            client.Close();
                            break;
                        }

                        if (messageBufferLength + payloadLength > messageBuffer.Length)
                        {
                            var newMessageBuffer = _bufferPool.Rent(messageBufferLength + payloadLength);
                            Array.Copy(messageBuffer, newMessageBuffer, messageBufferLength);
                            _bufferPool.Return(messageBuffer);
                            messageBuffer = newMessageBuffer;
                        }

                        Array.Copy(payload, 0, messageBuffer, messageBufferLength, payloadLength);
                        messageBufferLength += payloadLength;

                        if (fin)
                        {
                            onData?.Invoke(connectionId, new ArraySegment<byte>(messageBuffer, 0, messageBufferLength));
                            messageBufferLength = 0;
                        }
                    }
                    finally
                    {
                        _bufferPool.Return(payload);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception: {ex}");
        }
        finally
        {
            _bufferPool.Return(buffer);
            _bufferPool.Return(messageBuffer);

            if (connectedClients.ContainsKey(connectionId))
            {
                DelayInvoke(()=>onDisconnect?.Invoke(connectionId));
                KickClient(connectionId);
            }
        }
    }

    private static ArraySegment<byte> CreateWebSocketFrame(ArraySegment<byte> message)
    {
        int headerSize = 2 + (message.Count > 125 ? (message.Count <= ushort.MaxValue ? 2 : 8) : 0);
        int frameSize = headerSize + message.Count;

        byte[] frame = _bufferPool.Rent(frameSize);
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

    public async void SendOne(int connectionId, ArraySegment<byte> data)
    {
        
        if (connectedClients.ContainsKey(connectionId))
        {
            
            var clientInfo = connectedClients[connectionId];
            if (clientInfo.client.Connected)
            {
                var frameSegment = CreateWebSocketFrame(data);
                await clientInfo.writeSemaphore.WaitAsync();
                try
                {

                    await clientInfo.stream.WriteAsync(frameSegment.Array, frameSegment.Offset, frameSegment.Count);

                }
                catch (SocketException socketEx)
                {
                    if (connectedClients.ContainsKey(connectionId))
                    {
                        DelayInvoke(() => onDisconnect?.Invoke(connectionId));
                        KickClient(connectionId);

                    }
                }
                catch
                {

                }
                finally
                {
                    _bufferPool.Return(frameSegment.Array);
                    clientInfo.writeSemaphore?.Release();

                }
            }
        }
       
    }

    public void KickClient(int connectionId)
    {
        try
        {
            var resource = connectedClients[connectionId];
            
            resource.stream?.Close();
            resource.stream?.Dispose();
            resource.client?.Close();
            resource.client?.Dispose();
            resource.writeSemaphore?.Dispose();
            lock (dictionaryLock)
            {
                connectedClients.Remove(connectionId);
            }
            System.GC.Collect();
        }
        catch (Exception ex)
        {
            Debug.LogError($"KICK CLIENT Exception: {ex}");
        }
    }

    private async void DelayInvoke(System.Action action)
    {
        await Task.Delay(100);
        action();
    }
}
