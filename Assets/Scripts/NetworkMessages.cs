using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NetworkMessages
{
    public enum Commands{
        PLAYER_UPDATE,
        SERVER_UPDATE,
        HANDSHAKE,
        PLAYER_INPUT,
        CONNECTION_MSG,
        NEW_CLIENT,
         DROPPED_CLIENT,
         ALREADY_HERE
           
    }

    [System.Serializable]
    public class NetworkHeader{
        public Commands cmd;
    }

    [System.Serializable]
    public class InitializeConnectionMsg : NetworkHeader 
    {
        public string yourID;
        public InitializeConnectionMsg()
        {
            cmd = Commands.CONNECTION_MSG;
            yourID = "";
        }
    }

    [System.Serializable]
    public class AlreadyHereMsg : NetworkHeader
    {
        public List<NetworkObjects.NetworkPlayer> players;
        public AlreadyHereMsg()
        {
            cmd = Commands.ALREADY_HERE;
            players = new List<NetworkObjects.NetworkPlayer>();
        }
    }

    [System.Serializable]
    public class NewClientMsg : NetworkHeader
    {
        public NetworkObjects.NetworkPlayer player;
        public NewClientMsg()
        {
            cmd = Commands.NEW_CLIENT;
            player = new NetworkObjects.NetworkPlayer();
        }
    }


    [System.Serializable]
    public class HandshakeMsg:NetworkHeader{
        public NetworkObjects.NetworkPlayer player;
        public HandshakeMsg(){      // Constructor
            cmd = Commands.HANDSHAKE;
            player = new NetworkObjects.NetworkPlayer();
        }
    }
    
    [System.Serializable]
    public class PlayerUpdateMsg:NetworkHeader{
        public NetworkObjects.NetworkPlayer player;
        public PlayerUpdateMsg(){      // Constructor
            cmd = Commands.PLAYER_UPDATE;
            player = new NetworkObjects.NetworkPlayer();
        }
    };

    [System.Serializable]
    public class DroppedClientMsg : NetworkHeader
    {
        public NetworkObjects.NetworkPlayer player;
        public DroppedClientMsg()
        {      // Constructor
            cmd = Commands.DROPPED_CLIENT;
            player = new NetworkObjects.NetworkPlayer();
        }
    };


    [System.Serializable]
    public class PlayerInputMsg:NetworkHeader{
        public Input myInput;
        public PlayerInputMsg(){
            cmd = Commands.PLAYER_INPUT;
            myInput = new Input();
        }
    }
    [System.Serializable]
    public class  ServerUpdateMsg:NetworkHeader{
        public List<NetworkObjects.NetworkPlayer> players;
        public ServerUpdateMsg(){      // Constructor
            cmd = Commands.SERVER_UPDATE;
            players = new List<NetworkObjects.NetworkPlayer>();
        }
    }
} 

namespace NetworkObjects
{
    [System.Serializable]
    public class NetworkObject{
        public string id;
    }
    [System.Serializable]
    public class NetworkPlayer : NetworkObject{
        public Color cubeColor;
        public Vector3 cubPos;

        public NetworkPlayer(){
            cubeColor = new Color();
        }
    }
}
