using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Networking.Transport;
using NetworkMessages;
using System;
using System.Text;

public class NetworkServer : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public ushort serverPort;
    private NativeList<NetworkConnection> m_Connections;

    private Dictionary<string, NetworkObjects.NetworkPlayer> connectedClientsDict = new Dictionary<string, NetworkObjects.NetworkPlayer>(); //Dictionary for all clients
    private Dictionary<string, float> heartbeat = new Dictionary<string, float>();

    public MineManager mineManager;
    public List<Vector3> allMinePositions;
    public Vector3 closestMine;
    private float maxDistance = 45; // the furthest the player could be from a mine

    private float lastHeartbeatSent;
    private float lastColourSent;

    void Start()
    {

        m_Driver = NetworkDriver.Create();
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = serverPort;
        if (m_Driver.Bind(endpoint) != 0)
            Debug.Log("Failed to bind to port " + serverPort);
        else
            m_Driver.Listen();
        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);

        allMinePositions = mineManager.minePositions;
        InvokeRepeating("SendAllInfo", 0.1f, 0.166f);
        InvokeRepeating("ChangeCubeColours", 0.1f, 0.166f);
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
        m_Connections.Dispose();
    }

    void Update()
    {
        m_Driver.ScheduleUpdate().Complete();
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {
                m_Connections.RemoveAtSwapBack(i);
                --i;
            }
        }
        NetworkConnection c = m_Driver.Accept();
        while (c != default(NetworkConnection)) 
        {
            OnConnect(c);
            c = m_Driver.Accept();
        }

        DataStreamReader stream; 
        for (int i = 0; i < m_Connections.Length; i++)
        {
            Assert.IsTrue(m_Connections[i].IsCreated); 
            NetworkEvent.Type cmd;
            cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            while (cmd != NetworkEvent.Type.Empty) 
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    OnData(stream, i, m_Connections[i]); 

                    heartbeat[m_Connections[i].InternalId.ToString()] = Time.time;
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    OnDisconnect(i);
                }

                cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            }
        }
        checkForUpdate();
    }

    void OnConnect(NetworkConnection c)
    { 
        // send the ID to the client so we know who they are
        Debug.Log("Accepted a connection");
        PlayerUpdateMsg internalIdMsg = new PlayerUpdateMsg();
        internalIdMsg.cmd = Commands.PLAYER_WHOAMI;
        internalIdMsg.player.id = c.InternalId.ToString();
        Assert.IsTrue(c.IsCreated); 
        SendToClient(JsonUtility.ToJson(internalIdMsg), c);

        // Send list of players who are already in the game to the joining client
        ServerUpdateMsg alreadyHerePlayers = new ServerUpdateMsg();
        alreadyHerePlayers.cmd = Commands.ALREADY_HERE_PLAYERS;
        foreach (KeyValuePair<string, NetworkObjects.NetworkPlayer> element in connectedClientsDict)
        {
            alreadyHerePlayers.players.Add(element.Value);
        }

        Assert.IsTrue(c.IsCreated); 
        SendToClient(JsonUtility.ToJson(alreadyHerePlayers), c);


        // Send info on the joining client to the clients who were already here
        PlayerUpdateMsg newPlayerMsg = new PlayerUpdateMsg();
        newPlayerMsg.cmd = Commands.SPAWN_NEW_PLAYER;
        newPlayerMsg.player.id = c.InternalId.ToString();
        for (int i = 0; i < m_Connections.Length; i++)
        {
            Assert.IsTrue(m_Connections[i].IsCreated);
            SendToClient(JsonUtility.ToJson(newPlayerMsg), m_Connections[i]);
        }

        // Add the new client to the list of connections
        m_Connections.Add(c);

        // Add the new client to our list of clients
        connectedClientsDict[c.InternalId.ToString()] = new NetworkObjects.NetworkPlayer(); 
    }

    void OnData(DataStreamReader stream, int i, NetworkConnection client)
    {
         // Get the data sent an turn it into a Json
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length, Allocator.Temp);
        stream.ReadBytes(bytes); 
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray()); 
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch (header.cmd)
        {
            case Commands.HANDSHAKE:
                HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
                Debug.Log("Handshake message received!");
                break;
            case Commands.PLAYER_UPDATE:
                PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                Debug.Log("Got info from " + puMsg.player.id);
                UpdateClientInfo(puMsg);
                break;
            case Commands.SERVER_UPDATE:
                ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
                Debug.Log("Server update message received!");
                break;
            default:
                Debug.Log("SERVER ERROR: Unrecognized message received!");
                break;
        }
    }


    void checkForUpdate()
    {
        List<string> deleteList = new List<string>();
        foreach (KeyValuePair<string, float> element in heartbeat)
        {
            // If the client is gone for longer than 5 seconds
            if (Time.time - element.Value >= 5f)
            {
                Debug.Log(element.Key.ToString() + "stopped sending updates!");
                deleteList.Add(element.Key); // add them to a list of clients to delete from the list
            }
        }

        if (deleteList.Count != 0)
        {
            //Delete disconnected client from list
            for (int i = 0; i < deleteList.Count; ++i)
            {
                connectedClientsDict.Remove(deleteList[i]);
                heartbeat.Remove(deleteList[i]);
            }

            DisconnectedPlayersMsg dpMsg = new DisconnectedPlayersMsg();
            dpMsg.disconnectedPlayers = deleteList;

            // Send message to all clients
            for (int i = 0; i < m_Connections.Length; i++)
            {
                if (deleteList.Contains(m_Connections[i].InternalId.ToString()) == true)
                {
                    continue;
                }
                Assert.IsTrue(m_Connections[i].IsCreated);
                SendToClient(JsonUtility.ToJson(dpMsg), m_Connections[i]);
            }
        }
    }

    void OnDisconnect(int i)
    {
        Debug.Log("Client " + m_Connections[i].InternalId.ToString() + " disconnected from server");
        m_Connections[i] = default(NetworkConnection);
    }


    void SendToClient(string message, NetworkConnection c)
    {
        var writer = m_Driver.BeginSend(NetworkPipeline.Null, c);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message), Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void SendAllInfo()
    {
        ServerUpdateMsg m = new ServerUpdateMsg();
        foreach (KeyValuePair<string, NetworkObjects.NetworkPlayer> client in connectedClientsDict)
        {
            m.players.Add(client.Value);
        }
        for (int i = 0; i < m_Connections.Length; i++)
        {
            Assert.IsTrue(m_Connections[i].IsCreated); 
            SendToClient(JsonUtility.ToJson(m), m_Connections[i]);
        }
    }

    void UpdateClientInfo(PlayerUpdateMsg puMsg)
    {
        if (connectedClientsDict.ContainsKey(puMsg.player.id))
        {
            connectedClientsDict[puMsg.player.id].id = puMsg.player.id;
            connectedClientsDict[puMsg.player.id].pos = puMsg.player.pos;
            connectedClientsDict[puMsg.player.id].isDead = puMsg.player.isDead;
        }
    }

    void ChangeCubeColours() // make player cubes redder as they get closer to mines
    {
        foreach (KeyValuePair<string, NetworkObjects.NetworkPlayer> client in connectedClientsDict)
        {
            closestMine = GetClosestMine(allMinePositions, client.Value.pos);
            float distance = Vector3.Distance(closestMine, client.Value.pos);
            float colorChanger = distance / maxDistance; // attempting to get a value between 0 and 1 ish
            Debug.Log("color changer value: " + colorChanger);
            colorChanger = Mathf.Clamp(colorChanger, 0, 1); // making sure
            client.Value.color = new Color(1.0f, colorChanger, colorChanger); // actually changing the colour
        }
    }

    Vector3 GetClosestMine(List<Vector3> minePositions, Vector3 currentPos)
    {
        Vector3 tMin = Vector3.zero;
        float minDist = Mathf.Infinity;
        foreach (Vector3 minePos in minePositions)
        {
            float dist = Vector3.Distance(minePos, currentPos);
            if (dist < minDist)
            {
                tMin = minePos;
                minDist = dist;
            }
        }
        return tMin;
    }
}