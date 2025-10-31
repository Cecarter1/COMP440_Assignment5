using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;      // Reference to Rigidbody2D
    [SerializeField] public float moveSpeed = 5f; // Speed multiplier

    private float horizontalMovement;             // Horizontal input value

    // Called before Start()
    private void Awake()
    {
        // Automatically assign Rigidbody2D if not already linked in Inspector
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        // Safety check in case no Rigidbody2D exists on this GameObject
        if (rb == null)
        {
            Debug.LogError("No Rigidbody2D found on Player! Please add one.");
        }
    }

    // FixedUpdate is better for physics calculations
    private void FixedUpdate()
    {
        if (rb != null)
        {
            rb.velocity = new Vector2(horizontalMovement * moveSpeed, rb.velocity.y);
        }
    }

    // Input System event for Move action
    public void Move(InputAction.CallbackContext context)
    {
        // If Move action type = Value (Vector2)
        horizontalMovement = context.ReadValue<Vector2>().x;

        // If your Move action type = Value (1D Axis), use:
        // horizontalMovement = context.ReadValue<float>();
    }
}
