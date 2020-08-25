using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LNSServerManager : MonoBehaviour
{
    // Start is called before the first frame update
    private LNSServer server_1;
    void Start()
    {
        Application.targetFrameRate = 10;
        server_1 = new LNSServer(10002,"iamatestserver");
        server_1.Start();
        Debug.Log("Server key is iamatestserver");
    }


    public void OnDisable()
    {
        server_1.Dispose();
    }
}
