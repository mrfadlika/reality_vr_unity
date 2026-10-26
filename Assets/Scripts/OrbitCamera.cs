using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    [Header("Fly & Look Speed")]
    public float flySpeed = 45f;
    public float lookSensitivity = 2.5f;
    public float zoomSpeed = 15f;

    public float pitch = 35f;
    public float yaw = 45f;
    public float distance = 100f;
    public Vector3 targetFocusPos = Vector3.zero;

    private float targetPitch;
    private float targetYaw;
    private Vector3 targetPos;
    private bool isInitialized = false;

    void Start()
    {
        Vector3 euler = transform.eulerAngles;
        pitch = euler.x;
        yaw = euler.y;
        targetPitch = pitch;
        targetYaw = yaw;
        targetPos = transform.position;
        isInitialized = true;
    }

    public void SetCameraAngle(float p, float y, float dist, Vector3 focusPoint)
    {
        pitch = p;
        yaw = y;
        distance = dist;
        targetFocusPos = focusPoint;

        targetPitch = p;
        targetYaw = y;

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rot;
        transform.position = focusPoint - (rot * Vector3.forward * distance);
        targetPos = transform.position;
    }

    public void FocusOnPosition(Vector3 position, float customDistance = 35f)
    {
        targetFocusPos = position;
        distance = customDistance;

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rot;
        transform.position = position - (rot * Vector3.forward * distance);
        targetPos = transform.position;
    }

    void LateUpdate()
    {
        if (!isInitialized)
        {
            targetPos = transform.position;
            targetPitch = pitch;
            targetYaw = yaw;
            isInitialized = true;
        }

        bool isOverUI = VRPlayerController.IsPointerOverAnyUI();
        bool isDraggingInEditor = YardEditorManager.Instance != null && YardEditorManager.Instance.IsDraggingAnything();

        // 1. Mouse Drag -> Putar Rotasi Drone Camera (Pitch & Yaw Look Around)
        if (!isOverUI && !isDraggingInEditor && (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
        {
            targetYaw += Input.GetAxis("Mouse X") * lookSensitivity;
            targetPitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
            targetPitch = Mathf.Clamp(targetPitch, -85f, 85f);
        }

        // Haluskan rotasi dengan interpolasi mulus (Mencegah patah-patah di WebGL)
        yaw = Mathf.Lerp(yaw, targetYaw, Time.deltaTime * 20f);
        pitch = Mathf.Lerp(pitch, targetPitch, Time.deltaTime * 20f);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rotation;

        // 2. WASD Drone Flying Movement (Sejajar dengan lantai / Horizontal)
        float moveH = Input.GetAxisRaw("Horizontal"); // A / D
        float moveV = Input.GetAxisRaw("Vertical");   // W / S

        Vector3 fwd = transform.forward;
        Vector3 rgt = transform.right;
        fwd.y = 0f; // Ratakan pitch agar W/S bergerak maju/mundur sejajar tanah
        rgt.y = 0f;
        fwd.Normalize();
        rgt.Normalize();

        Vector3 moveDir = fwd * moveV + rgt * moveH;

        // E / Space = Ketinggian Naik, Q / C = Ketinggian Turun
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) moveDir.y += 1f;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.C)) moveDir.y -= 1f;

        float currentSpeed = flySpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            currentSpeed *= 2.5f; // Shift Turbo Speed Boost
        }

        if (moveDir.sqrMagnitude > 0.001f)
        {
            targetPos += moveDir.normalized * currentSpeed;
        }

        // 3. Scroll Wheel Zoom / Move Forward
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            targetPos += transform.forward * (scroll * zoomSpeed * 10f);
        }

        // 4. Pengunci Ketinggian Minimum Kamera 3D agar Tidak Jatuh Menembus Lantai (Lantai vr-env Y = 19.0f)
        float minCamY = 19.5f;
        GameObject vrEnv = GameObject.Find("vr-env");
        if (vrEnv != null)
        {
            Renderer rend = vrEnv.GetComponentInChildren<Renderer>();
            float envTopY = (rend != null) ? rend.bounds.max.y : vrEnv.transform.position.y;
            minCamY = envTopY + 0.5f;
        }

        if (targetPos.y < minCamY)
        {
            targetPos.y = minCamY;
        }

        // Haluskan posisi kamera (Smooth Damping Lerp) untuk pergerakan mulus di WebGL
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 18f);
    }

    public static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F) angle += 360F;
        if (angle > 360F) angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }
}
