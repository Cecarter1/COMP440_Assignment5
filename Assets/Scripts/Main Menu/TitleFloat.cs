using UnityEngine;

public class TitleFloat : MonoBehaviour
{
    [Header("Movement Settings")]
    public float amplitude = 15f;   // how far it moves (pixels-ish)
    public float frequency = 1f;    // how fast it moves

    private Vector3 startLocalPos;

    void Start()
    {
        // Save the starting position of the title (relative to the canvas)
        startLocalPos = transform.localPosition;
    }

    void Update()
    {
        // Move up and down over time using a sine wave
        float offsetY = Mathf.Sin(Time.time * frequency) * amplitude;

        // If you want left/right instead, use offsetX instead of offsetY
        Vector3 newPos = startLocalPos + new Vector3(0f, offsetY, 0f);

        transform.localPosition = newPos;
    }
}

