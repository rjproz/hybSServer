using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

    static Dictionary<int,(TcpClient,Stream)> connectedClients = new Dictionary<int, (TcpClient, Stream)>();
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
            sslCertificate = new X509Certificate2(sslPath, sslPassword);
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
        
        try
        {
            var buffer = new byte[bufferSize];
            if (sslCertificate != null)
            {
                stream = new SslStream(tcpClient.GetStream(), false);
                await (stream as SslStream).AuthenticateAsServerAsync(sslCertificate, clientCertificateRequired: false, checkCertificateRevocation: false);
            }
            else
            {
                stream = tcpClient.GetStream();
            }

            int bytesRead = 0;

            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

            var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            if (IsWebSocketRequest(request))
            {
                var response = CreateWebSocketHandshakeResponse(request);
                await stream.WriteAsync(Encoding.UTF8.GetBytes(response), 0, response.Length);
                await HandleWebSocketAsync(tcpClient,stream);
            }
            else
            {
                Debug.LogError("Invalid WebSocket request");
                tcpClient.Close();
                tcpClient.Dispose();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception: {ex}");
            tcpClient.Close();
            tcpClient.Dispose();
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
        connectedClients.Add(connectionId, (client,stream));

        if(onConnect != null)
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
                        client.Close();
                        client.Dispose();
                    }
                    break;
                }

                if(onData != null)
                {
                    onData(connectionId, new ArraySegment<byte>(buffer, 0, bytesRead));
                }
                
               
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex}");
                break;
            }
           
        }
    }


    public void SendOne(int connectionId,ArraySegment<byte> data)
    {
        connectedClients[connectionId].Item2.WriteAsync(data);
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
