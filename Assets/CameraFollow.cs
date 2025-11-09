using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Follow Settings")]
    public float FollowSpeed = 2f;
    public float yOffset = 1f;
    public Transform target;

    [Header("Zoom Settings")]
    [Tooltip("The desired vertical half-size of the camera view. Higher values zoom OUT.")]
    public float targetZoomSize = 6.0f;
    public float zoomSpeed = 2.0f;

    // Cache the Camera component
    private Camera cam;

    void Awake()
    {
        // Get the Camera component attached to this same GameObject.
        cam = GetComponent<Camera>();

        // Safety check to ensure the script is attached correctly.
        if (cam == null || !cam.orthographic)
        {
            Debug.LogError("CameraFollow requires an Orthographic Camera component on this GameObject.");
            enabled = false;
        }
    }

    void Update()
    {
        // 1. Handle Position Tracking
        Vector3 newPos = new Vector3(target.position.x, target.position.y + yOffset, -10f);
        transform.position = Vector3.Slerp(transform.position, newPos, FollowSpeed * Time.deltaTime);

        // 2. Handle Zoom
        // Smoothly adjust the camera's size toward the targetZoomSize value
        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            targetZoomSize,
            zoomSpeed * Time.deltaTime
        );
    }
}
