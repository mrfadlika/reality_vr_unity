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

    // State interaksi mouse kamera 3D
    private bool isOrbiting = false;  // Putar sudut pandang (Klik Kanan Drag atau Alt + Klik Kiri Drag)
    private bool isPanning = false;   // Geser posisi kamera sejajar tanah (Klik Kiri Drag atau Klik Tengah Drag)

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

        // 1. Inisialisasi Drag Mouse saat pertama kali ditekan (Hanya cek isOverUI saat tombol ditekan agar gerakan tidak terputus saat melintasi UI)
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        {
            if (!isOverUI && !isDraggingInEditor)
            {
                if (Input.GetMouseButtonDown(1) || (Input.GetMouseButtonDown(0) && Input.GetKey(KeyCode.LeftAlt)))
                {
                    isOrbiting = true;
                }
                else if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(2))
                {
                    isPanning = true;
                }
            }
        }

        // Lepas state drag saat tombol mouse dilepaskan
        if (!Input.GetMouseButton(1) && !(Input.GetMouseButton(0) && Input.GetKey(KeyCode.LeftAlt)))
        {
            isOrbiting = false;
        }
        if (!Input.GetMouseButton(0) && !Input.GetMouseButton(2))
        {
            isPanning = false;
        }

        // 2. Putar Sudut Pandang Kamera (Orbit / Look Around) — Klik Kanan Drag atau Alt + Klik Kiri Drag
        if (isOrbiting)
        {
            targetYaw += Input.GetAxis("Mouse X") * lookSensitivity;
            targetPitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
            targetPitch = Mathf.Clamp(targetPitch, -85f, 85f);
        }

        // 3. Geser Posisi Kamera (Pan) — Klik Kiri Drag atau Klik Tengah Drag
        if (isPanning && !isOrbiting)
        {
            float deltaX = Input.GetAxis("Mouse X");
            float deltaY = Input.GetAxis("Mouse Y");

            if (Mathf.Abs(deltaX) > 0.0001f || Mathf.Abs(deltaY) > 0.0001f)
            {
                // Kecepatan geser proporsional terhadap ketinggian kamera dari tanah
                float currentHeight = Mathf.Max(5f, transform.position.y - 19.81f);
                float panFactor = currentHeight * 0.045f;

                Vector3 camRight = transform.right;
                Vector3 camForward = transform.forward;
                camRight.y = 0f;
                camForward.y = 0f;
                camRight.Normalize();
                camForward.Normalize();

                Vector3 panDelta = (-camRight * deltaX - camForward * deltaY) * panFactor;
                targetPos += panDelta;
            }
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
