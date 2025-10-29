using UnityEngine;
using System.Collections;

public class TimedRotatePlatform : MonoBehaviour
{
    // === TIMING AND SPEED SETTINGS ===

    // Time the platform waits at each angle (Horizontal and Vertical)
    public float waitTime = 2.0f;

    // Time it takes to complete the rotation (e.g., 0.75 seconds for a smooth spin)
    public float rotationDuration = 0.75f;

    // === TARGET ANGLES ===

    // Horizontal position (0 degrees Z-rotation)
    private readonly Quaternion AngleHorizontal = Quaternion.Euler(0, 0, 0f);

    // Vertical position (-90 degrees Z-rotation for clockwise turn)
    private readonly Quaternion AngleVertical = Quaternion.Euler(0, 0, -90f);

    // Tracks the rotation destination
    private Quaternion targetAngle;

    void Start()
    {
        // Start the platform in the horizontal position and set the first target to Vertical
        transform.rotation = AngleHorizontal;
        targetAngle = AngleVertical;

        // Begin the timed rotation loop
        StartCoroutine(RotationCycle());
    }

    IEnumerator RotationCycle()
    {
        while (true) // Loop indefinitely
        {
            // 1. PAUSE at the current position
            yield return new WaitForSeconds(waitTime);

            // 2. SMOOTH ROTATION LOGIC

            Quaternion startRotation = transform.rotation;
            Quaternion endRotation = targetAngle;
            float elapsed = 0f;

            // Rotate smoothly from current angle to the target angle
            while (elapsed < rotationDuration)
            {
                // Interpolation factor (t goes from 0.0 to 1.0)
                float t = elapsed / rotationDuration;

                // Move the rotation based on the interpolation factor
                transform.rotation = Quaternion.Lerp(startRotation, endRotation, t);

                elapsed += Time.deltaTime;
                yield return null; // Wait for the next frame
            }

            // Ensure the rotation is exactly the target angle to prevent drift
            transform.rotation = endRotation;

            // 3. SWITCH TARGET
            // If we just reached the vertical angle, the next target is horizontal, and vice versa.
            if (targetAngle == AngleVertical)
            {
                targetAngle = AngleHorizontal;
            }
            else
            {
                targetAngle = AngleVertical;
            }
        }
    }

    // === PLAYER ATTACHMENT (FOR INTERACTION) ===

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // Attach player to platform when they land on it
            collision.gameObject.transform.SetParent(transform);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // Detach player when they jump off
            collision.gameObject.transform.SetParent(null);
        }
    }
}