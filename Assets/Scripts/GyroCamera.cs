using UnityEngine;

public class GyroCamera : MonoBehaviour
{
    private bool gyroEnabled;
    private Gyroscope gyro;
    private GameObject cameraContainer;
    private Quaternion rot;

    [Header("Mouse Fallback (Desktop)")]
    public float mouseSensitivity = 2f;
    private float mouseX;
    private float mouseY;

    void Start()
    {
        cameraContainer = new GameObject("Camera Container");
        cameraContainer.transform.position = transform.position;
        cameraContainer.transform.rotation = transform.rotation;
        transform.SetParent(cameraContainer.transform);

        gyroEnabled = EnableGyro();
    }

    private bool EnableGyro()
    {
        if (SystemInfo.supportsGyroscope)
        {
            gyro = Input.gyro;
            gyro.enabled = true;

            cameraContainer.transform.rotation = Quaternion.Euler(90f, 90f, 0f);
            rot = new Quaternion(0, 0, 1, 0);
            return true;
        }
        return false;
    }

    void Update()
    {
        if (gyroEnabled)
        {
            transform.localRotation = gyro.attitude * rot;
        }
        else
        {
            // Desktop mouse look fallback
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
            {
                mouseX += Input.GetAxis("Mouse X") * mouseSensitivity;
                mouseY -= Input.GetAxis("Mouse Y") * mouseSensitivity;
                mouseY = Mathf.Clamp(mouseY, -80f, 80f);

                transform.localRotation = Quaternion.Euler(mouseY, mouseX, 0f);
            }
        }
    }
}

