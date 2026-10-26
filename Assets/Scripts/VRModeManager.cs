using UnityEngine;

public class VRModeManager : MonoBehaviour
{
    public Camera leftCamera;
    public Camera rightCamera;

    [Header("VR Case Settings")]
    public bool isSplitScreenVR = false;
    public float ipd = 0.064f; // Interpupillary distance (64mm)

    void Start()
    {
        SetupStereoCameras();
    }

    public void SetupStereoCameras()
    {
        if (leftCamera == null) leftCamera = GetComponent<Camera>();
        if (leftCamera == null) leftCamera = Camera.main;

        if (leftCamera != null && rightCamera == null)
        {
            GameObject rightEyeObj = new GameObject("RightEyeCamera");
            rightEyeObj.transform.SetParent(leftCamera.transform);
            rightEyeObj.transform.localPosition = new Vector3(ipd, 0, 0);
            rightEyeObj.transform.localRotation = Quaternion.identity;

            rightCamera = rightEyeObj.AddComponent<Camera>();
            rightCamera.CopyFrom(leftCamera);
        }

        SetSplitScreenVR(isSplitScreenVR);
    }

    public void SetSplitScreenVR(bool enable)
    {
        isSplitScreenVR = enable;

        if (leftCamera != null && rightCamera != null)
        {
            if (enable)
            {
                leftCamera.rect = new Rect(0f, 0f, 0.5f, 1f);
                rightCamera.rect = new Rect(0.5f, 0f, 0.5f, 1f);
                rightCamera.gameObject.SetActive(true);
            }
            else
            {
                leftCamera.rect = new Rect(0f, 0f, 1f, 1f);
                rightCamera.gameObject.SetActive(false);
            }
        }
    }
}

