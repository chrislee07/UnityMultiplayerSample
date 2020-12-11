using Unity.Collections;
using System.Collections.Generic;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;
using System.Diagnostics;
using System.Collections.Specialized;
using System.Security.Cryptography;
using UnityEngine;
using System.Collections;
using System.CodeDom.Compiler;
using System.Threading;
using System.IO;

public class NetworkClient : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public string serverIP;
    public ushort serverPort;

    public Transform SpawnPoint;
    public GameObject playerGO; // our player gameobject
    public Dictionary<string, GameObject> currentPlayers; // currenttly connected players list
    public List<string> newPlayers, droppedPlayers; // new and dropped players list

    string test = "PLAYER_UPDATE";

    void Start ()
    {
        // Initialize variables
        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);
        var endpoint = NetworkEndPoint.Parse(serverIP,serverPort);
        m_Connection = m_Driver.Connect(endpoint);

        newPlayers = new List<string>();
        droppedPlayers = new List<string>();
        currentPlayers = new Dictionary<string, GameObject>();
    }
    
    void SendToServer(string message){
        var writer = m_Driver.BeginSend(m_Connection);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void OnConnect(){

        UnityEngine.Debug.Log("We are now connected to the server");

        playerGO = Instantiate(playerGO, SpawnPoint.position, SpawnPoint.rotation);

        StartCoroutine(UpdatePlayerPos());

        //// Example to send a handshake message:
        //PlayerConMsg m = new PlayerConMsg();
        //m.player.id = m_Connection.InternalId.ToString();
        //SendToServer(JsonUtility.ToJson(m));
    }

    void OnData(DataStreamReader stream){
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd){
            case Commands.PLAYER_CONNECTED:
            PlayerConMsg pcMsg = JsonUtility.FromJson<PlayerConMsg>(recMsg);
            UnityEngine.Debug.Log("Player connected message received!");
            break;
            case Commands.PLAYER_UPDATE:
            PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
            UnityEngine.Debug.Log("Player update message received! : " + recMsg);
            break;
            case Commands.SERVER_UPDATE:
            ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
            UnityEngine.Debug.Log("Server update message received!");
            break;
            default:
            UnityEngine.Debug.Log("Unrecognized message received!");
            break;
        }
    }

    void Disconnect(){
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
    }

    void OnDisconnect(){
        UnityEngine.Debug.Log("Client got disconnected from server");
        m_Connection = default(NetworkConnection);
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
    }

    IEnumerator UpdatePlayerPos()
    {
        DataStreamReader stream;
        NetworkEvent.Type cmd;
        cmd = m_Connection.PopEvent(m_Driver, out stream);

        for (int i = 1; i < 10; i++)
        {
            Vector3 currPosition = playerGO.transform.position;
            
            //UnityEngine.Debug.Log("Current Position: " + currPosition.ToString());

            PlayerUpdateMsg m = new PlayerUpdateMsg();
            m.player.cubPos = currPosition;
            SendToServer(JsonUtility.ToJson(m));
            yield return new WaitForSeconds(3);
            i = 1;

            while (cmd != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    OnData(stream);
                }
            }
        }
    }

    void Update()
    {
        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
        {
            return;
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        cmd = m_Connection.PopEvent(m_Driver, out stream);
        while (cmd != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                OnConnect();

            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                OnData(stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                OnDisconnect();
            }

            cmd = m_Connection.PopEvent(m_Driver, out stream);
        }

        //Player Movement with WASD
        float xDirection = Input.GetAxis("Horizontal");
        float zDirection = Input.GetAxis("Vertical");

        Vector3 moveDirection = new Vector3(xDirection, 0.0f, zDirection);

        playerGO.transform.position += moveDirection * 0.03f;


    }
}