using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;
using UnityEngine.Networking;
using System.Collections.Generic;
using TMPro;

public class NetworkClient : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;

    public string serverIP;
    public ushort serverPort;

    public string myID;
    public TextMeshProUGUI idText;

    public GameObject playerCube;
    public List<Player> playersInGame;

    [Serializable]
    public class StillToSpawn // Players that still need to be spawned
    {
        public List<NetworkObjects.NetworkPlayer> playersStillToSpawn = new List<NetworkObjects.NetworkPlayer>();
    }

    [Serializable]
    public class DroppedPlayers // Players that have disconnected.
    {
        public List<NetworkObjects.NetworkPlayer> players = new List<NetworkObjects.NetworkPlayer>();
    }

    [Serializable]
    public class GameState
    {
        public List<NetworkObjects.NetworkPlayer> players = new List<NetworkObjects.NetworkPlayer>();// Players currently online ACCORDING TO THE SERVER - different than playersInGame!
    }


    [Serializable]
    public class NewPlayer // For spawning a new player's cube when they arrive at the party.
    {
        public NetworkObjects.NetworkPlayer player;
    }

    [Serializable]
    public class AlreadyHerePlayerList // We only use this when we're a new player joining and want to know who else is already at the party, so we can make cubes for them.
    {
        public List<NetworkObjects.NetworkPlayer> players;
    }

    void Start ()
    {
        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);
        var endpoint = NetworkEndPoint.Parse(serverIP,serverPort);
        m_Connection = m_Driver.Connect(endpoint);
        //Debug.Log(m_Connection.InternalId.ToString());
        InvokeRepeating("OnPlayerUpdate", 1, 1);
    }
    
    void SendToServer(string message){
        var writer = m_Driver.BeginSend(m_Connection);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void OnConnect(){
        Debug.Log("We are now connected to the server");
    }

    void OnPlayerUpdate()
    {
        PlayerUpdateMsg m = new PlayerUpdateMsg();
        SendToServer(JsonUtility.ToJson(m));
    }

    void OnData(DataStreamReader stream){
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);
        StillToSpawn sts = new StillToSpawn();

        switch (header.cmd)
        {
            case Commands.NEW_CLIENT: // this will maybe cause2 cubes to be spawned for each client right now
                NewClientMsg ncMsg = JsonUtility.FromJson<NewClientMsg>(recMsg);
                NewPlayer newPlayer = new NewPlayer();
                newPlayer.player = ncMsg.player;
                sts.playersStillToSpawn.Add(newPlayer.player);
                SpawnWaitingPlayers(sts);
                Debug.Log("new client message received!");
                break;
            case Commands.HANDSHAKE:
                HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
                Debug.Log("Handshake message received!");
                break;
            case Commands.PLAYER_UPDATE:
                PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                Debug.Log("Player update message received!");
                UpdatePlayers();
                break;
            case Commands.DROPPED_CLIENT: // The server is telling us that someone has left the cube party.
                DroppedClientMsg dcMsg = JsonUtility.FromJson<DroppedClientMsg>(recMsg); // Get the ID of the dropped player
                DroppedPlayers droppedPlayer = new DroppedPlayers();
                droppedPlayer.players.Add(dcMsg.player);
                DestroyPlayers(dcMsg.player.id); // Get rid of their cube.
                break;
            case Commands.SERVER_UPDATE:
                ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
                Debug.Log("Server update message received!");
                OnServerUpdate(suMsg.players);
                break;
            case Commands.CONNECTION_MSG:
                InitializeConnectionMsg icMsg = JsonUtility.FromJson<InitializeConnectionMsg>(recMsg);
                Debug.Log("Connection initialization message received!");
                OnConnectionInitialized(icMsg);
                break;
            case Commands.ALREADY_HERE: // This command should only come to the newly connected client - it's the server helpfully telling us who 
                                                // is already here, so we can spawn their cubes.
                AlreadyHereMsg ahMsg = JsonUtility.FromJson<AlreadyHereMsg>(recMsg); // Populate the list.
                AlreadyHerePlayerList ahPlayers = new AlreadyHerePlayerList();
                ahPlayers.players = ahMsg.players;
                //sts.playersStillToSpawn = ahPlayers.players;
                foreach (NetworkObjects.NetworkPlayer player in ahPlayers.players)
                {
                    sts.playersStillToSpawn.Add(player); // Spawn all the cubes!
                    Debug.Log("adding a player with id " + player.id + " to playersStillToSpawn list");
                }
                SpawnWaitingPlayers(sts);
                break;
            default:
                Debug.Log("Unrecognized message received!");
                break;
        }
    }

    void OnConnectionInitialized(InitializeConnectionMsg icMsg)
    {
        // Set my ID
        myID = icMsg.yourID;
        Debug.Log("Setting this client's ID to:" + myID);
        idText.SetText(myID);
        // Spawn myself
        //SpawnCube(myID);
    }

    void Disconnect(){
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
    }

    void OnDisconnect(){
        Debug.Log("Client got disconnected from server");
        m_Connection = default(NetworkConnection);
    }

    void OnServerUpdate(List<NetworkObjects.NetworkPlayer> serversListOfPlayers)
    {
        Debug.Log("got a list of players from the server as follows:");

        foreach (NetworkObjects.NetworkPlayer player in serversListOfPlayers)
        {
            Debug.Log("Player id: " + player.id);
        }
    }

    void DestroyPlayers(string _id) // This is where we destroy cubes. 
    {
        foreach (Player playerCube in playersInGame) // Go through all the cubes we have in the game currently.
        {
            if (playerCube.myID == _id) // If this is the droid we're looking for (based on the _id sent)
            {
                playerCube.markedForDestruction = true; // Tell the cube to delete itself on its next Update.
            }
        }
    }

    void UpdatePlayers() // Updating all the cube positions except my own.
    {
        //for (int i = 0; i < latestGameState.players.Length; i++) // Go through all the players the server says we have
        //{
        //    for (int j = 0; j < playersInGame.Count; j++) // And go through all the player cubes we have in game already
        //    {
        //        if (latestGameState.players[i].id == playersInGame[j].networkID) // If the player ID and the cube ID match
        //        {
        //            if (latestGameState.players[i].id != myAddress) // And it's NOT me (my position is updated in my own Input section of PlayerCube.cs)
        //            {
        //                // Send the position the server says these other cubes have to their cube objects.
        //                playersInGame[j].newTransformPos =
        //                  new Vector3(latestGameState.players[i].position.x, latestGameState.players[i].position.y, latestGameState.players[i].position.z);
        //            }
        //        }
        //    }
        //}
    }

    void SpawnWaitingPlayers(StillToSpawn sts)
    {
        foreach (NetworkObjects.NetworkPlayer player in sts.playersStillToSpawn)
        {
            Debug.Log("spawning a cube now with ID " + player.id);
            SpawnCube(player.id);
        }
       
        sts.playersStillToSpawn.Clear();
        sts.playersStillToSpawn.TrimExcess();
    }

    void SpawnCube(string id)
    {
        Instantiate(playerCube);
        Player newPlayer = playerCube.GetComponent<Player>();
        newPlayer.myID = id;
        playersInGame.Add(newPlayer);
    }

    public void OnDestroy()
    {
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
}