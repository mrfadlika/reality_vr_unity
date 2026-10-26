using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CharacterController))]
public class VRPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 6.0f;
    public float runSpeed = 10.0f;
    public float jumpHeight = 1.2f;
    public float gravity = -9.81f;
    public Transform vrCamera;

    [Header("VR / Walk Settings")]
    [Tooltip("Tinggi mata player dari tanah (misal 1.7 meter)")]
    public float eyeLevelHeight = 1.7f;

    public enum ControlHintPosition
    {
        BottomLeft,         // Sudut kiri bawah (Rapi di samping Search Bar)
        TopBelowButtons,    // Di atas (di bawah tombol 3D View / 2D View / Walk View)
        BottomAboveSearch   // Di bawah (tepat di atas kotak Search Bar)
    }

    [Header("Posisi Keterangan Kontrol Walk View")]
    [Tooltip("Pilih posisi kotak instruksi kontrol: Sudut Kiri Bawah (Default), Di Atas, atau Di Atas Search Bar")]
    public ControlHintPosition hintPosition = ControlHintPosition.BottomLeft;

    [Header("Head Bobbing")]
    public bool enableHeadBob = true;
    public float bobFrequency = 10f;
    public float bobAmount = 0.04f;
    private float bobTimer = 0f;

    [Header("Mouse Look Settings")]
    public float mouseSensitivity = 2.0f;
    private float mouseX;
    private float mouseY;

    private CharacterController controller;
    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (vrCamera == null && Camera.main != null)
        {
            vrCamera = Camera.main.transform;
        }

        if (ViewModeManager.Instance != null && ViewModeManager.Instance.currentMode == CameraViewMode.VRView)
        {
            SnapToFloor();
        }
    }

    void OnEnable()
    {
        if (ViewModeManager.Instance != null && ViewModeManager.Instance.currentMode != CameraViewMode.VRView)
        {
            enabled = false;
            return;
        }
        SnapToFloor();
    }

    void OnDisable()
    {
        // Pastikan kursor selalu dikembalikan bebas saat keluar dari mode Walk View
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnDestroy()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public float GetEnvFloorY()
    {
        GameObject vrEnv = GameObject.Find("vr-env");
        if (vrEnv == null) vrEnv = GameObject.Find("VR-Env");
        if (vrEnv == null) vrEnv = GameObject.Find("VR-ENV");

        if (vrEnv != null)
        {
            Renderer rend = vrEnv.GetComponentInChildren<Renderer>();
            if (rend != null) return rend.bounds.max.y;
            return vrEnv.transform.position.y;
        }
        return 19.0f;
    }

    public void SnapToFloor()
    {
        if (!enabled) return;
        if (ViewModeManager.Instance != null && ViewModeManager.Instance.currentMode != CameraViewMode.VRView) return;

        if (controller == null) controller = GetComponent<CharacterController>();
        
        bool isStandaloneCam = (transform.parent == null && (vrCamera == null || vrCamera == transform));

        if (controller != null)
        {
            controller.enabled = false;
            controller.height = eyeLevelHeight;
            controller.radius = 0.4f;
            controller.stepOffset = 0.4f;
            controller.slopeLimit = 45f;
            controller.minMoveDistance = 0f;

            if (isStandaloneCam)
            {
                controller.center = new Vector3(0, -eyeLevelHeight / 2f, 0);
            }
            else
            {
                controller.center = new Vector3(0, eyeLevelHeight / 2f, 0);
            }
        }

        float floorY = GetEnvFloorY();
        Vector3 playerPos = transform.position;

        if (isStandaloneCam)
        {
            playerPos.y = floorY + eyeLevelHeight;
        }
        else
        {
            playerPos.y = floorY;
        }
        transform.position = playerPos;

        if (controller != null) controller.enabled = true;

        // Baca rotasi awal agar kamera tidak menyentak
        if (vrCamera != null && vrCamera != transform)
        {
            vrCamera.localPosition = new Vector3(0, eyeLevelHeight, 0);
            vrCamera.localRotation = Quaternion.identity;

            Vector3 euler = vrCamera.eulerAngles;
            mouseX = euler.y;
            mouseY = euler.x;
            if (mouseY > 180f) mouseY -= 360f;
        }
        else
        {
            Vector3 euler = transform.eulerAngles;
            mouseX = euler.y;
            mouseY = euler.x;
            if (mouseY > 180f) mouseY -= 360f;
        }
    }

    public static bool IsPointerOverAnyUI()
    {
        // 1. Cek uGUI Canvas
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return true;

        Vector3 mPos = Input.mousePosition;
        float screenY = Screen.height - mPos.y; // 0 di atas, Screen.height di bawah

        // 2. Cek area tombol OnGUI atas
        if (screenY <= 90f)
            return true;

        // 3. Cek area Search Bar OnGUI bawah di tengah
        float searchW = 440f;
        float searchX = (Screen.width - searchW) / 2f;
        if (screenY >= Screen.height - 90f && mPos.x >= searchX && mPos.x <= searchX + searchW)
            return true;

        // 4. Cek area kotak petunjuk di sudut kiri bawah
        if (screenY >= Screen.height - 85f && mPos.x <= 380f)
            return true;

        return false;
    }

    void Update()
    {
        if (ViewModeManager.Instance != null && ViewModeManager.Instance.currentMode != CameraViewMode.VRView)
        {
            enabled = false;
            return;
        }

        bool isOverUI = IsPointerOverAnyUI();

        // 1. Kontrol Kursor Mouse (Kunci kursor saat klik di layar 3D, lepas saat tekan ESC)
        if (Input.GetMouseButtonDown(0) && !isOverUI)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        bool isXRActive = UnityEngine.XR.XRSettings.isDeviceActive;

        // 2. First-Person Mouse Look
        bool isCursorLocked = (Cursor.lockState == CursorLockMode.Locked);
        bool isDraggingMouse = (Input.GetMouseButton(0) || Input.GetMouseButton(1)) && !isOverUI;

        if (isCursorLocked || isDraggingMouse)
        {
            mouseX += Input.GetAxis("Mouse X") * mouseSensitivity;
            mouseY -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            mouseY = Mathf.Clamp(mouseY, -85f, 85f);

            if (vrCamera != null && vrCamera != transform)
            {
                // Putar Badan Player secara horizontal (Yaw)
                transform.rotation = Quaternion.Euler(0f, mouseX, 0f);
                
                // Tilt Kepala vertikal (Pitch) HANYA jika tidak dalam sesi VR aktif (anti-jitter)
                if (!isXRActive)
                {
                    vrCamera.localRotation = Quaternion.Euler(mouseY, 0f, 0f);
                }
            }
            else
            {
                transform.rotation = Quaternion.Euler(mouseY, mouseX, 0f);
            }
        }

        // 3. Direct WASD + VR Controller Walk Movement
        float moveH = 0f;
        float moveV = 0f;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) moveV += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) moveV -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) moveH += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) moveH -= 1f;

        if (moveH == 0f) moveH = Input.GetAxisRaw("Horizontal");
        if (moveV == 0f) moveV = Input.GetAxisRaw("Vertical");


#if !UNITY_WEBGL
        // Input Thumbstick VR Controller (Left Hand -> Jalan Maju/Mundur/Samping)
        UnityEngine.XR.InputDevice leftHand = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
        if (leftHand.isValid)
        {
            Vector2 thumbstick;
            if (leftHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out thumbstick))
            {
                if (thumbstick.magnitude > 0.15f)
                {
                    moveH += thumbstick.x;
                    moveV += thumbstick.y;
                }
            }
        }

        // Input Thumbstick VR Controller (Right Hand -> Putar/Belok Badan)
        UnityEngine.XR.InputDevice rightHand = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
        if (rightHand.isValid)
        {
            Vector2 rThumbstick;
            if (rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out rThumbstick))
            {
                if (Mathf.Abs(rThumbstick.x) > 0.2f)
                {
                    mouseX += rThumbstick.x * 75f * Time.deltaTime;
                    if (vrCamera != null && vrCamera != transform)
                    {
                        transform.rotation = Quaternion.Euler(0f, mouseX, 0f);
                    }
                    else
                    {
                        transform.rotation = Quaternion.Euler(mouseY, mouseX, 0f);
                    }
                }
            }
        }
#endif

        bool isMoving = (Mathf.Abs(moveH) > 0.01f || Mathf.Abs(moveV) > 0.01f);
        bool isRunning = isMoving && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        float speed = isRunning ? runSpeed : walkSpeed;

        if (isMoving)
        {
            // Arah jalan relatif terhadap badan/pandangan player
            Transform refTrans = (vrCamera != null && vrCamera != transform) ? vrCamera : transform;
            Vector3 fwd = refTrans.forward;
            Vector3 rgt = refTrans.right;
            fwd.y = 0f;
            rgt.y = 0f;
            fwd.Normalize();
            rgt.Normalize();

            Vector3 moveDir = (fwd * moveV + rgt * moveH).normalized;
            Vector3 moveDistance = moveDir * speed * Time.deltaTime;

            if (controller != null && controller.enabled)
            {
                controller.Move(moveDistance);
            }
            else
            {
                transform.position += moveDistance;
            }

            // Head Bobbing efek jalan manusia (Hanya di mode Desktop biasa, matikan saat VR aktif)
            if (enableHeadBob && !isXRActive && vrCamera != null && vrCamera != transform)
            {
                bobTimer += Time.deltaTime * (isRunning ? bobFrequency * 1.4f : bobFrequency);
                float headBobY = eyeLevelHeight + Mathf.Sin(bobTimer) * (isRunning ? bobAmount * 1.5f : bobAmount);
                vrCamera.localPosition = new Vector3(0, headBobY, 0);
            }
        }
        else
        {
            // Reset posisi kamera ke tinggi mata normal saat berhenti (Hanya jika non-VR)
            if (!isXRActive && vrCamera != null && vrCamera != transform)
            {
                bobTimer = 0f;
                Vector3 camPos = vrCamera.localPosition;
                camPos.y = Mathf.Lerp(camPos.y, eyeLevelHeight, Time.deltaTime * 8f);
                vrCamera.localPosition = camPos;
            }
        }

        // 4. Gravitasi Bumi & Lompat
        if (controller != null && controller.enabled)
        {
            if (controller.isGrounded)
            {
                if (velocity.y < 0) velocity.y = -2f;

                // Lompat dengan Spasi
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }

            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }

        // 5. Pengunci Ketinggian Lantai Terendah
        Vector3 pos = transform.position;
        bool standalone = (transform.parent == null && (vrCamera == null || vrCamera == transform));
        float floorY = GetEnvFloorY();
        float minY = standalone ? (floorY + eyeLevelHeight) : floorY;

        if (pos.y < minY)
        {
            pos.y = minY;
            if (controller != null && controller.enabled)
            {
                controller.enabled = false;
                transform.position = pos;
                controller.enabled = true;
            }
            else
            {
                transform.position = pos;
            }
            velocity.y = 0f;
        }
    }

    void OnGUI()
    {
        if (ViewModeManager.Instance != null && ViewModeManager.Instance.currentMode != CameraViewMode.VRView)
            return;

        bool isXRActive = UnityEngine.XR.XRSettings.isDeviceActive;
        bool isLocked = (Cursor.lockState == CursorLockMode.Locked);

        // --- POSISI 1: SUDUT KIRI BAWAH (Default & Rekomendasi) ---
        if (hintPosition == ControlHintPosition.BottomLeft)
        {
            float boxW = 350f;
            float boxH = 52f;
            float boxX = 20f;
            float boxY = Screen.height - boxH - 20f;

            // Background Box elegan semi-transparan
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.90f);
            GUI.Box(new Rect(boxX, boxY, boxW, boxH), "");

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 11;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleLeft;

            GUIStyle subStyle = new GUIStyle(GUI.skin.label);
            subStyle.fontSize = 11;
            subStyle.fontStyle = FontStyle.Normal;
            subStyle.alignment = TextAnchor.MiddleLeft;

            float padX = boxX + 10f;
            float innerW = boxW - 20f;

            if (isXRActive)
            {
                GUI.color = new Color(0.2f, 1f, 0.4f, 0.95f);
                GUI.Label(new Rect(padX, boxY + 5f, innerW, 20f), "🥽 VR Active (Headset):", titleStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(padX, boxY + 26f, innerW, 20f), "Tengok: Kepala | Jalan: Thumbstick VR / WASD", subStyle);
            }
            else
            {
                // Baris 1: Kontrol Gerak (W,A,S,D, Shift Lari, Space Lompat)
                GUI.color = new Color(1f, 0.92f, 0.4f);
                GUI.Label(new Rect(padX, boxY + 5f, innerW, 20f), "🚶 [W,A,S,D] Jalan  |  [Shift] Lari  |  [Space] Lompat", titleStyle);

                // Baris 2: Kontrol Kamera & Kursor
                if (isLocked)
                {
                    GUI.color = Color.white;
                    GUI.Label(new Rect(padX, boxY + 26f, innerW, 20f), "🖱️ [Mouse] Tengok Bebas  |  [ESC] Lepas Kursor", subStyle);
                }
                else
                {
                    GUI.color = new Color(0.85f, 0.92f, 1f);
                    GUI.Label(new Rect(padX, boxY + 26f, innerW, 20f), "🖱️ Klik Layar: Tengok Bebas  |  [ESC] Buka Menu", subStyle);
                }
            }

            GUI.color = Color.white;
            return;
        }

        // --- POSISI ALTERNATIF: TENGAH ATAS / TENGAH BAWAH ---
        float bannerWidth = 720f;
        float bannerHeight = 34f;
        float bannerX = (Screen.width - bannerWidth) / 2f;
        float bannerY = (hintPosition == ControlHintPosition.TopBelowButtons) 
            ? 88f 
            : (Screen.height - 126f);

        GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.88f);
        GUI.Box(new Rect(bannerX, bannerY, bannerWidth, bannerHeight), "");

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 12;
        style.fontStyle = FontStyle.Bold;

        if (isXRActive)
        {
            GUI.color = new Color(0.2f, 1f, 0.4f, 0.95f);
            GUI.Label(new Rect(bannerX, bannerY, bannerWidth, bannerHeight),
                "🥽 VR Active: Tengok bebas via Headset | Jalan & Belok via Thumbstick VR Controller / WASD", style);
        }
        else
        {
            if (isLocked)
            {
                GUI.color = Color.white;
                GUI.Label(new Rect(bannerX, bannerY, bannerWidth, bannerHeight),
                    "🚶 Walk View: [W,A,S,D] Jalan | [Shift] Lari | [Space] Lompat | [Mouse] Tengok Bebas | [ESC] Lepas Kursor", style);
            }
            else
            {
                GUI.color = new Color(1f, 0.92f, 0.4f);
                GUI.Label(new Rect(bannerX, bannerY, bannerWidth, bannerHeight),
                    "🚶 Walk View: [W,A,S,D] Jalan | [Shift] Lari | [Space] Lompat | Klik Layar untuk Tengok Bebas | [ESC] Buka Menu", style);
            }
        }

        GUI.color = Color.white;
    }
}