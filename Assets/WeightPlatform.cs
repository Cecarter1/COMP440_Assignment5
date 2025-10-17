using UnityEngine;

public class WeightPlatform : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Max angle the platform will rotate (e.g., 30 degrees)")]
    [SerializeField] private float rotationMaxDegrees = 30f; // Default value from video
    [SerializeField] private float rotationSpeed = 20f; // Speed of rotation
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Player Check Settings")]
    [Tooltip("Distance the raycast goes down to check if the player is still standing on the platform")]
    [SerializeField] private float heightCheckDistance = 0.5f; // Default value from video

    private bool shouldPlatformRotate = false;
    private Transform player;
    private BoxCollider2D platformBoxCollider;
    private float platformLength;

    void Awake()
    {
        // Get component references
        platformBoxCollider = GetComponent<BoxCollider2D>();
    }

    void Start()
    {
        // Calculate the platform's half-length (for rotation multiplier)
        platformLength = platformBoxCollider.size.x / 2f;
    }

    void Update()
    {
        if (shouldPlatformRotate)
        {
            // 1. Calculate the target angle based on player position
            float rotationMultiplier = CalculateRotationMultiplier();
            float targetAngle = rotationMultiplier * rotationMaxDegrees;

            // Apply rotation towards the target angle
            RotatePlatform(targetAngle);

            // 2. Custom check to see if the player has left
            CheckForPlayerExit();
        }
        else
        {
            // If player is off, rotate back to 0 (horizontal)
            RotateBackToZero();
        }
    }

    private void RotatePlatform(float targetAngle)
    {
        // Define the target rotation Quaternion
        Quaternion targetRotation = Quaternion.AngleAxis(targetAngle, Vector3.forward);

        // Rotate smoothly towards the target rotation
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            Time.deltaTime * rotationSpeed
        );
    }

    private void RotateBackToZero()
    {
        // Rotate smoothly back to the upright position (Quaternion.identity is 0 rotation)
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.identity,
            Time.deltaTime * rotationSpeed
        );
    }

    private float CalculateRotationMultiplier()
    {
        // Calculate the player's X position relative to the platform's center
        Vector3 playerRelativePosition = transform.InverseTransformPoint(player.position);

        // Clamp the player's relative position between -1 and 1
        // This acts as the rotation multiplier: -1 at the left edge, 1 at the right edge
        float multiplier = Mathf.Clamp(playerRelativePosition.x / platformLength, -1f, 1f);

        // Invert the multiplier to match the rotation direction (e.g., player on right edge (positive x) causes negative rotation)
        return -multiplier;
    }

    // --- Player Collision and Exit Logic ---

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // Start rotating when player lands
            shouldPlatformRotate = true;
            player = collision.gameObject.transform;

            // Parent the player to the platform so they move/rotate together
            player.SetParent(transform);
        }
    }

    private void CheckForPlayerExit()
    {
        // Perform a BoxCast down from the platform to see if the player's layer is still present.
        // This is a common method used to reliably detect if a player has jumped off.
        if (!Physics2D.BoxCast(
                platformBoxCollider.bounds.center,
                platformBoxCollider.bounds.size,
                0f,
                Vector2.down,
                heightCheckDistance,
                playerLayerMask)
            )
        {
            // If the BoxCast does not hit the player, they have left the platform.
            shouldPlatformRotate = false;
            if (player != null)
            {
                // Detach the player from the platform
                player.SetParent(null);
                player = null;
            }
        }
    }
}