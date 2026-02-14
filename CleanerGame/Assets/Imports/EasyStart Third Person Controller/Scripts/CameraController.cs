
using UnityEngine;

/*
    This file has a commented version with details about how each line works. 
    The commented version contains code that is easier and simpler to read. This file is minified.
*/

/// <summary>
/// Camera movement script for third person games.
/// This Script should not be applied to the camera! It is attached to an empty object and inside
/// it (as a child object) should be your game's MainCamera.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Tooltip("Enable zoom in/out when scrolling the mouse wheel. Does not work with joysticks.")]
    public bool canZoom = true;
    [Tooltip("Camera zoom speed when using mouse wheel.")]
    public float zoomSpeed = 10f;
    [Tooltip("Minimum allowed field of view when zooming in.")]
    public float minFieldOfView = 25f;
    [Tooltip("Maximum allowed field of view when zooming out.")]
    public float maxFieldOfView = 70f;

    [Space]
    [Tooltip("Locked world position for the camera rig object.")]
    public Vector3 fixedPosition = new Vector3(0f, 12f, 0f);
    [Tooltip("Locked Euler angles for a top-down tilt (x = pitch, y = yaw, z = roll).")]
    public Vector3 fixedEulerAngles = new Vector3(60f, 0f, 0f);
    [Tooltip("Apply fixed position/rotation on Start. Keep enabled for static cameras.")]
    public bool lockOnStart = true;

    void Start()
    {
        if (lockOnStart)
        {
            transform.position = fixedPosition;
            transform.rotation = Quaternion.Euler(fixedEulerAngles);
        }
    }


    void Update()
    {
        if (lockOnStart)
        {
            transform.position = fixedPosition;
            transform.rotation = Quaternion.Euler(fixedEulerAngles);
        }

        // Set camera zoom when mouse wheel is scrolled
        if (canZoom && Camera.main != null)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f)
            {
                float nextFov = Camera.main.fieldOfView - (scroll * zoomSpeed);
                Camera.main.fieldOfView = Mathf.Clamp(nextFov, minFieldOfView, maxFieldOfView);
            }
        }
    }
}