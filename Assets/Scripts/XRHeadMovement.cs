using UnityEngine;
using UnityEngine.XR;

public class XRHeadMovement : MonoBehaviour
{
    [Header("Head Tracking")]
    public bool enableHeadTracking = true;

    [Header("Sensitivity")]
    [Range(0.1f, 3f)]
    public float sensitivity = 1f;

    [Header("Smoothing")]
    [Range(0f, 20f)]
    public float smoothing = 10f;

    private InputDevice hmd;

    private Quaternion initialRotation;
    private Quaternion targetRotation;

    private float checkTimer = 0f;

    void Start()
    {
        initialRotation = transform.localRotation;
        targetRotation = initialRotation;
        CheckHMD(false);
    }

    void CheckHMD(bool logIfFound)
    {
        hmd = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        if (!hmd.isValid)
        {
            hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        }
        if (hmd.isValid && logIfFound)
        {
            Debug.Log("[XRHeadMovement] VR Headset terdeteksi: " + hmd.name);
        }
    }

    void Update()
    {
        if (!enableHeadTracking)
            return;

        // Cek headset secara berkala jika belum terdeteksi
        if (!hmd.isValid)
        {
            checkTimer += Time.deltaTime;
            if (checkTimer >= 1.0f)
            {
                checkTimer = 0f;
                CheckHMD(true);
            }
            return;
        }

        // Baca rotasi orientasi nyata dari headset VRidge / SteamVR (1:1 Tracking)
        Quaternion headRotation;
        if (hmd.TryGetFeatureValue(CommonUsages.deviceRotation, out headRotation))
        {
            // Terapkan rotasi secara langsung (1:1 murni seperti mouse)
            // Tanpa konversi Euler (mencegah gimbal lock / jitter)
            // Tanpa Slerp lag (responsif seketika saat badan / kepala berputar)
            transform.localRotation = headRotation;
        }
    }

    float NormalizeAngle(float angle)
    {
        if (angle > 180f)
            angle -= 360f;

        return angle;
    }
}