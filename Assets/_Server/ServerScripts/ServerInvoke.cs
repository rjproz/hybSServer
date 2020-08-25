using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ServerInvoke : MonoBehaviour
{
    // Start is called before the first frame update
    private Server server;
    void Start()
    {
        Application.targetFrameRate = 10;
        server = new Server();
        server.StartServer(49153);
    }


    private void OnDisable()
    {
        server.Dispose();
    }


}
