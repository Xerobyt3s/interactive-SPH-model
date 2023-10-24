using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Camera_controller : MonoBehaviour
{
    [SerializeField] Transform player_transform;
    [SerializeField] Transform camera_transform;
    [SerializeField] float mouse_sensitivity_x = 100f;
    [SerializeField] float mouse_sensitivity_y = 100f;

    float x_rotation;
    float mouse_x;
    float mouse_y;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Get the mouse input
        mouse_x = Input.GetAxis("Mouse X") * mouse_sensitivity_x * Time.deltaTime;
        mouse_y = Input.GetAxis("Mouse Y") * mouse_sensitivity_y * Time.deltaTime;

        // Rotate the player
        player_transform.Rotate(Vector3.up * mouse_x);

        // Rotate the camera
        x_rotation -= mouse_y;
        x_rotation = Mathf.Clamp(x_rotation, -90f, 90f);

        camera_transform.localRotation = Quaternion.Euler(x_rotation, 0f, 0f);
    }
}
