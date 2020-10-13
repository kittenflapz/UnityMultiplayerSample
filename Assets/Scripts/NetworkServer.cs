using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using System;
using System.Text;
using System.Collections.Generic;
using System.Collections;

public class NetworkServer : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public ushort serverPort;
    private NativeList<NetworkConnection> m_Connections;
    public List<NetworkObjects.NetworkPlayer> m_Players; // A list of players

    void Start ()
    {
        m_Driver = NetworkDriver.Create();
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = serverPort;

        if (m_Driver.Bind(endpoint) != 0)
            Debug.Log("Failed to bind to port " + serverPort);
        else
            m_Driver.Listen();

        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);

        //StartCoroutine(SendHandshakeToAllClient());
        StartCoroutine(SendUpdateToAllClients());
    }

    //IEnumerator SendHandshakeToAllClient()
    //{
    //    while(true)
    //    {
    //        for (int i = 0; i < m_Connections.Length; i++)
    //        {
    //            if (!m_Connections[i].IsCreated)
    //                continue;
    //            HandshakeMsg m = new HandshakeMsg();
    //            m.player.id = m_Connections[i].InternalId.ToString();
    //            SendToClient(JsonUtility.ToJson(m), m_Connections[i]);
    //        }
    //        yield return new WaitForSeconds(2);
    //    }
    //}

    IEnumerator SendUpdateToAllClients()
    {
        while (true)
        {
            for (int i = 0; i < m_Connections.Length; i++)
            {
                if (!m_Connections[i].IsCreated)
                    continue;
                ServerUpdate(m_Connections[i]);
            }
            yield return new WaitForSeconds(2);
        }
    }

    void SendToClient(string message, NetworkConnection c){
        var writer = m_Driver.BeginSend(NetworkPipeline.Null, c);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }
    public void OnDestroy()
    {
        m_Driver.Dispose();
        m_Connections.Dispose();
    }

    void OnConnect(NetworkConnection c)
    {
        // send info of newly connected client to currently connected clients
        foreach (NetworkConnection currentlyConnectedClient in m_Connections)
        { 
            NewClientMsg ncMsg = new NewClientMsg();
            ncMsg.player.id = c.InternalId.ToString();
            SendToClient(JsonUtility.ToJson(ncMsg), currentlyConnectedClient);
        }

        m_Connections.Add(c);
        var newPlayersId = c.InternalId;
        var connMsg = new InitializeConnectionMsg();
        connMsg.yourID = newPlayersId.ToString();
        SendToClient(JsonUtility.ToJson(connMsg), c);

  

        // add this player to our list of players
        NetworkObjects.NetworkPlayer newPlayer = new NetworkObjects.NetworkPlayer();
        newPlayer.id = c.InternalId.ToString();
        m_Players.Add(newPlayer);
        Debug.Log("New player connected. Current list of players: ");
        // Sending the list of players currently connected (including this one) to the client (so client can spawn them)
        AlreadyHereMsg ahMsg = new AlreadyHereMsg();
        foreach(NetworkObjects.NetworkPlayer player in m_Players)
        {
            ahMsg.players.Add(player);
            Debug.Log(player.id);
        }
        SendToClient(JsonUtility.ToJson(ahMsg), c);
    }

    void OnData(DataStreamReader stream, int i){
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd){
            case Commands.HANDSHAKE:
            HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
            Debug.Log("Handshake message received!");
            break;
            case Commands.PLAYER_UPDATE:
            PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
            OnPlayerUpdate(puMsg);
            //Debug.Log("Player update message received!");
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

    void OnDisconnect(int i){
        Debug.Log("Client disconnected from server");
        foreach (var player in m_Players)
        {
            if (player.id == i.ToString())
            {
                m_Players.Remove(player);
                break;
            }
        }
        m_Connections[i] = default(NetworkConnection);
    }

    void Update ()
    {
        m_Driver.ScheduleUpdate().Complete();

        // CleanUpConnections
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {

                m_Connections.RemoveAtSwapBack(i);
                --i;
            }
        }

        // AcceptNewConnections
        NetworkConnection c = m_Driver.Accept();
        while (c  != default(NetworkConnection))
        {            
            OnConnect(c);

            // Check if there is another new connection
            c = m_Driver.Accept();
        }


        // Read Incoming Messages
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
                    OnData(stream, i);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    OnDisconnect(i);
                }

                cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            }
        }
    }


    void ServerUpdate(NetworkConnection c)
    {
        var updateMsg = new ServerUpdateMsg();
        updateMsg.players = m_Players;
        Debug.Log("Current list of players: ");
        foreach (NetworkObjects.NetworkPlayer player in m_Players)
        {
            Debug.Log("ID: " + player.id);
        }
        SendToClient(JsonUtility.ToJson(updateMsg), c);
    }

    void OnPlayerUpdate(PlayerUpdateMsg _puMsg)
    {


        // Eventually, update our list of players with the player's cube pos


    }

}