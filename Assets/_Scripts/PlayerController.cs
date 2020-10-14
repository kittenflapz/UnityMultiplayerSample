using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerController : MonoBehaviour
{
    [SerializeField]
    float moveSpeed = 0f;

    public bool isMe; // the one already in the scene when we start
    public bool isDead;


    private Rigidbody rigidBody;
    private Vector3 inputMove;
    private float turnY;
    private TextMeshProUGUI myIDLabel;


    // Use this for initialization
    void Start()
    {
        // reference components
        rigidBody = GetComponent<Rigidbody>();
        myIDLabel = gameObject.GetComponentInChildren<TextMeshProUGUI>();
    }
    void Update()
    {
        if (isDead)
        {
            myIDLabel.SetText(":(");
        }

        if (isMe)
        {
            if (isDead)
            {
                rigidBody.velocity = new Vector3(0f, 0f, 0f);
                rigidBody.isKinematic = true;
            }
            else
            {
                PlayerInput();
            }
        }
    }

    void FixedUpdate()
    {
        if (isMe)
        {
            Movement();
        }
    }

    void PlayerInput()
    {
        // fetch input and send to vector3
        float moveX = Input.GetAxis("Horizontal") * (moveSpeed / 2);
        float moveZ = Input.GetAxis("Vertical") * moveSpeed;
        inputMove = new Vector3(moveX, 0.0f, moveZ);
    }

    void Movement()
    {
        // add our input to rigidbody's forward facing velocity
        rigidBody.velocity = transform.forward * inputMove.z;

        // rotate player based on input
        turnY += inputMove.x;
        rigidBody.rotation = Quaternion.Euler(0.0f, turnY, 0.0f);
    }
}