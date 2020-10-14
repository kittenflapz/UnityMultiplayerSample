using UnityEngine;
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;
using TMPro;

public class NetworkClient : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public string serverIP;
    public ushort serverPort;

    public string myID;

    private Dictionary<string, GameObject> connectedClientsDict = new Dictionary<string, GameObject>();

    public TextMeshProUGUI myIDLabel = null;

    [SerializeField]
    Transform player = null;

    [SerializeField]
    GameObject playerCube = null; 

    PlayerUpdateMsg playerInfo = new PlayerUpdateMsg();

    TextMeshProUGUI playerIDText = null;


    void Start()
    {
        m_Driver = NetworkDriver.Create(); 
        m_Connection = default(NetworkConnection);
        serverIP = "3.137.149.42";
        var endpoint = NetworkEndPoint.Parse(serverIP, serverPort);
        m_Connection = m_Driver.Connect(endpoint); 

        playerIDText = player.gameObject.GetComponentInChildren<TextMeshProUGUI>();
    }

    public void OnDestroy()
    {
        //Disconnect();
        m_Driver.Dispose();
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
    }

    void OnConnect()
    {
        Debug.Log("We are now connected to the server");
        InvokeRepeating("SendPlayerInfo", 0.1f, 0.0166f);
    }


    void OnData(DataStreamReader stream)
    {
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length, Allocator.Temp);
        stream.ReadBytes(bytes); //Get bytes
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray()); //convert bytes to JSON string
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg); //convert JSON to c# class

        switch (header.cmd)
        {
            case Commands.HANDSHAKE:
                HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
                Debug.Log("Handshake message received!");
                break;
            case Commands.PLAYER_WHOAMI:
                PlayerUpdateMsg internalId = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                Debug.Log("Got internalId from server");
                playerInfo.player.id = internalId.player.id;
                playerIDText.SetText(playerInfo.player.id);
                myIDLabel.SetText(playerInfo.player.id);
                break;
            case Commands.PLAYER_UPDATE:
                PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                Debug.Log("Got data from server, player Pos: " + puMsg.player.pos);
                break;
            case Commands.SERVER_UPDATE:
                ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
                Debug.Log("Server update message received!");
                UpdateClientsInfo(suMsg);
                break;
            case Commands.ALREADY_HERE_PLAYERS:
                ServerUpdateMsg alreadyHerePlayerInfo = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
                Debug.Log("existed player info received!");
                SpawnPlayersAlreadyHere(alreadyHerePlayerInfo);
                break;
            case Commands.SPAWN_NEW_PLAYER:
                PlayerUpdateMsg newPlayerMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                Debug.Log("new client info received!");
                SpawnNewPlayer(newPlayerMsg);
                break;
            case Commands.DISCONNECTED_PLAYER:
                DisconnectedPlayersMsg dpMsg = JsonUtility.FromJson<DisconnectedPlayersMsg>(recMsg);
                Debug.Log("Player disconnected :(");
                DeleteDisconnectPlayer(dpMsg);
                break;

            default:
                Debug.Log("Unrecognized message received!");
                break;
        }
    }

    void OnDisconnect()
    {
        Debug.Log("Client got disconnected from server");
        m_Connection = default(NetworkConnection);
    }

    void SendToServer(string message)
    {
         var writer = m_Driver.BeginSend(m_Connection);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message), Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void Disconnect()
    {
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
    }

    void SendPlayerInfo()
    {
        playerInfo.player.pos = player.position;
        playerInfo.player.color = player.gameObject.GetComponent<Renderer>().material.color;
        playerInfo.player.isDead = player.gameObject.GetComponent<PlayerController>().isDead;
        SendToServer(JsonUtility.ToJson(playerInfo));
    }

    void SpawnPlayersAlreadyHere(ServerUpdateMsg suMsg)
    {
        for (int i = 0; i < suMsg.players.Count; ++i)
        {
            GameObject cube = Instantiate(playerCube);

            connectedClientsDict[suMsg.players[i].id] = cube;
            cube.transform.position = suMsg.players[i].pos;

            cube.GetComponentInChildren<TextMeshProUGUI>().SetText(suMsg.players[i].id);
            cube.GetComponent<PlayerController>().isMe = false; // stops new cubes from being controlled by this client
        }
    }

    void SpawnNewPlayer(PlayerUpdateMsg puMsg)
    {
        GameObject cube = Instantiate(playerCube);

        connectedClientsDict[puMsg.player.id] = cube;
        cube.GetComponentInChildren<TextMeshProUGUI>().SetText(puMsg.player.id);
        cube.GetComponent<PlayerController>().isMe = false; // stops new cubes from being controlled by this client
    }

    void UpdateClientsInfo(ServerUpdateMsg suMsg)
    {
        for (int i = 0; i < suMsg.players.Count; ++i)
        {
            if (connectedClientsDict.ContainsKey(suMsg.players[i].id))
            {
                connectedClientsDict[suMsg.players[i].id].transform.position = suMsg.players[i].pos;
                connectedClientsDict[suMsg.players[i].id].GetComponent<Renderer>().material.color = suMsg.players[i].color;
                if (suMsg.players[i].isDead)
                {
                    connectedClientsDict[suMsg.players[i].id].GetComponent<PlayerController>().isDead = true;
                    connectedClientsDict[suMsg.players[i].id].GetComponentInChildren<TextMeshProUGUI>().SetText(":(");
                }
            }
         

            else if (playerInfo.player.id == suMsg.players[i].id)
            {
                player.gameObject.GetComponent<Renderer>().material.color = suMsg.players[i].color;
                playerInfo.player.color = suMsg.players[i].color;
                if (suMsg.players[i].isDead)
                {
                    connectedClientsDict[suMsg.players[i].id].GetComponent<PlayerController>().isDead = true;
                    connectedClientsDict[suMsg.players[i].id].GetComponentInChildren<TextMeshProUGUI>().SetText(":(");
                }
            }
        }
    }

    void DeleteDisconnectPlayer(DisconnectedPlayersMsg dcMsg)
    {
        for (int i = 0; i < dcMsg.disconnectedPlayers.Count; ++i)
        {
            if (connectedClientsDict.ContainsKey(dcMsg.disconnectedPlayers[i]))
            {
                Destroy(connectedClientsDict[dcMsg.disconnectedPlayers[i]]);
                connectedClientsDict.Remove(dcMsg.disconnectedPlayers[i]);
            }
        }
    }

}