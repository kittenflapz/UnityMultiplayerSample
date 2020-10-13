using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Player : MonoBehaviour // The representation of the player in the game.
{
    public string myID; // The ID of the cube.
    public bool markedForDestruction; // Can't destroy directly from anywhere but Update(), so makes sense to do it in this GameObject's Update().
    public Vector3 newTransformPos; // Can't get the GameObject's transform anywhere but Update(), so makes sense to do translations here using a temp variable.
    public NetworkClient networkMan; // A reference to the network manager, used later for making sure we don't move any cubes that aren't ours.
    public float speed; // Speed.

    public TextMeshProUGUI idLabel;

    // Start is called before the first frame update
    void Start()
    {
        markedForDestruction = false; // Obviously.
        newTransformPos = new Vector3(Random.Range(-3, 3), 0.0f, 0.0f); // so we can see more than one lol
        speed = 5.0f; // You can set this in the inspector if you like a speedier cube.
        networkMan = FindObjectOfType<NetworkClient>();
    }

    // Update is called once per frame
    void Update()
    {
        if (markedForDestruction) // True when the player is no longer sending heartbeats to the server, so has disconnected.
        {
            Destroy(gameObject); // No player? No cube.
        }

        idLabel.SetText(myID);

        if (myID != networkMan.myID) // For every cube that isn't me.
        {
            transform.position = newTransformPos; // Update their positions.
            return; // Then get out! Everything that happens after this is input that should ONLY happen for the client controlling this cube.
        }

        /**********INPUT***********/

        if (Input.GetKey(KeyCode.W))
        {
            transform.Translate(Vector3.forward * Time.deltaTime * speed);
        }

        if (Input.GetKey(KeyCode.S))
        {
            transform.Translate(-Vector3.forward * Time.deltaTime * speed);
        }

        if (Input.GetKey(KeyCode.A))
        {
            transform.Translate(Vector3.left * Time.deltaTime * speed);
        }

        if (Input.GetKey(KeyCode.D))
        {
            transform.Translate(-Vector3.left * Time.deltaTime * speed);
        }
    }
}
