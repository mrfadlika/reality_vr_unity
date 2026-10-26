using UnityEngine;
using System.Collections.Generic;

public enum CameraViewMode
{
    BirdView3D,
    TopDown2D,
    VRView
}

public class ViewModeManager : MonoBehaviour
{
    public static ViewModeManager Instance { get; private set; }

    [Header("Camera References")]
    public Camera mainCamera;
    public Camera topDownCamera;
    public Camera vrCamera;
    public GameObject vrPlayerRig;

    [Header("Current Mode")]
    public CameraViewMode currentMode = CameraViewMode.BirdView3D;

    [Header("Top-Down 2D Camera Settings")]
    public float topDownHeight = 80f;
    public float topDownOrthoSize = 40f;
    public float minOrthoSize = 10f;
    public float maxOrthoSize = 100f;

    [Header("Custom UI Button Images (Tarik file PNG ke sini)")]
    [Tooltip("Tarik gambar PNG untuk tombol 3D View")]
    public Texture2D icon3DView;
    [Tooltip("Tarik gambar PNG untuk tombol 2D View")]
    public Texture2D icon2DView;
    [Tooltip("Tarik gambar PNG untuk tombol Walk View")]
    public Texture2D iconWalkView;

    [Header("uGUI Canvas Buttons (Jika menggunakan Canvas UI Button)")]
    [Tooltip("Tarik objek Button 3D View dari Hierarchy/Canvas ke sini")]
    public UnityEngine.UI.Button button3DView;
    [Tooltip("Tarik objek Button 2D View dari Hierarchy/Canvas ke sini")]
    public UnityEngine.UI.Button button2DView;
    [Tooltip("Tarik objek Button Walk View dari Hierarchy/Canvas ke sini")]
    public UnityEngine.UI.Button buttonWalkView;

    [Header("UI Display Settings")]
    [Tooltip("Tampilkan tombol OnGUI bawaan jika belum ada tombol kustom")]
    public bool showLegacyOnGUI = true;
    public Vector2 buttonSize = new Vector2(180f, 60f);
    public float buttonSpacing = 16f;
    public float topMargin = 20f;

    private Vector3 topDownTargetPos;
    private OrbitCamera orbitCam;
    private VRPlayerController vrController;

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void EnterVRIfNotActive();
#endif

    // --- Public methods for UI Button OnClick Events ---
    public void SwitchTo3DView() { SetViewMode(CameraViewMode.BirdView3D); }
    public void SwitchTo2DView() { SetViewMode(CameraViewMode.TopDown2D); }
    public void SwitchToWalkView() 
    { 
        SetViewMode(CameraViewMode.VRView); 
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            EnterVRIfNotActive();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[ViewModeManager] EnterVRIfNotActive error: " + ex.Message);
        }
#endif
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Pastikan ukuran tombol cukup besar dan proporsional untuk file PNG
        if (buttonSize.x < 150f || buttonSize.y < 50f)
        {
            buttonSize = new Vector2(180f, 60f);
        }
    }

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null) orbitCam = mainCamera.GetComponent<OrbitCamera>();
        
        if (vrPlayerRig == null)
        {
            vrPlayerRig = GameObject.Find("XRRig");
            if (vrPlayerRig == null) vrPlayerRig = GameObject.Find("XR Rig");
        }
        if (vrPlayerRig != null) vrController = vrPlayerRig.GetComponent<VRPlayerController>();

        // Setup uGUI Button Click Listeners jika ditarik ke Inspector
        if (button3DView != null) button3DView.onClick.AddListener(SwitchTo3DView);
        if (button2DView != null) button2DView.onClick.AddListener(SwitchTo2DView);
        if (buttonWalkView != null) buttonWalkView.onClick.AddListener(SwitchToWalkView);

        // Ensure secondary cameras are properly configured
        SetupCameras();

        // Switch to default mode
        SetViewMode(currentMode);
    }

    void SetupCameras()
    {
        // 1. Top Down Camera
        if (topDownCamera == null)
        {
            GameObject tdCamObj = new GameObject("TopDownCamera");
            topDownCamera = tdCamObj.AddComponent<Camera>();
        }

        topDownCamera.orthographic = true;
        topDownCamera.orthographicSize = topDownOrthoSize;
        topDownCamera.transform.rotation = Quaternion.Euler(90, 0, 0); // Point straight down
        topDownTargetPos = new Vector3(0, topDownHeight, 0);
        topDownCamera.transform.position = topDownTargetPos;

        topDownCamera.farClipPlane = 400f;

        // Copy background/clear flags from main camera
        if (mainCamera != null)
        {
            topDownCamera.clearFlags = mainCamera.clearFlags;
            topDownCamera.backgroundColor = mainCamera.backgroundColor;
            mainCamera.farClipPlane = 400f;
        }

        // 2. VR Camera
        if (vrCamera == null && vrPlayerRig != null)
        {
            vrCamera = vrPlayerRig.GetComponentInChildren<Camera>();
        }
        if (vrCamera != null)
        {
            vrCamera.farClipPlane = 400f;
            vrCamera.nearClipPlane = 0.08f;
        }
    }

    public Vector3 GetYardsWorldCenter(out float recommendedDistance)
    {
        recommendedDistance = 120f;
        List<Renderer> allYardRends = new List<Renderer>();

        YardBoundary[] yards = Object.FindObjectsByType<YardBoundary>(FindObjectsSortMode.None);
        if (yards != null && yards.Length > 0)
        {
            foreach (var y in yards)
            {
                Renderer[] rends = y.GetComponentsInChildren<Renderer>();
                foreach (var r in rends)
                {
                    if (!r.gameObject.name.Contains("ft)")) // Exclude containers
                    {
                        allYardRends.Add(r);
                    }
                }
            }
        }

        // Fallback: Jika tidak ada YardBoundary, ambil seluruh MeshRenderer di scene (kontainer, tanah, dll)
        if (allYardRends.Count == 0)
        {
            MeshRenderer[] rends = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            foreach (var r in rends)
            {
                string rName = r.gameObject.name;
                if (!rName.Contains("Indicator") && !rName.Contains("Pointer") && !rName.Contains("Tag"))
                {
                    allYardRends.Add(r);
                }
            }
        }

        if (allYardRends.Count > 0)
        {
            Bounds combined = allYardRends[0].bounds;
            for (int i = 1; i < allYardRends.Count; i++)
            {
                combined.Encapsulate(allYardRends[i].bounds);
            }

            // Hitung jarak kamera yang pas secara otomatis berdasarkan ukuran area yard!
            recommendedDistance = Mathf.Clamp(combined.size.magnitude * 0.85f, 60f, 350f);
            return combined.center;
        }

        return Vector3.zero;
    }

    public void SetViewMode(CameraViewMode mode)
    {
        currentMode = mode;

        if (mainCamera == null) mainCamera = Camera.main;

        // Lepaskan kunci kursor jika berganti ke mode selain Walk View agar bisa klik tombol UI bebas
        if (mode != CameraViewMode.VRView)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // MATIKAN GYRO CAMERA JIKA TIDAK DI VR MODE (PENTING: Agar tidak mengunci kamera di tanah!)
        GyroCamera gyroCam = mainCamera != null ? mainCamera.GetComponent<GyroCamera>() : null;
        if (gyroCam != null)
        {
            gyroCam.enabled = (mode == CameraViewMode.VRView);
        }

        // MATIKAN XR HEAD MOVEMENT JIKA TIDAK DI VR MODE
        XRHeadMovement xrHead = mainCamera != null ? mainCamera.GetComponent<XRHeadMovement>() : null;
        if (xrHead != null)
        {
            xrHead.enabled = (mode == CameraViewMode.VRView);
        }

        // Matikan komponen OrbitCamera (skrip terbang 3D) di semua kamera jika tidak di mode 3D
        if (mode != CameraViewMode.BirdView3D)
        {
            if (orbitCam == null && mainCamera != null) orbitCam = mainCamera.GetComponent<OrbitCamera>();
            if (orbitCam != null) orbitCam.enabled = false;

            if (vrCamera != null)
            {
                OrbitCamera vrOrbit = vrCamera.GetComponent<OrbitCamera>();
                if (vrOrbit != null) vrOrbit.enabled = false;
            }
        }

        // Matikan VRPlayerController & CharacterController di semua object jika tidak di Walk View
        if (mode != CameraViewMode.VRView)
        {
            if (mainCamera != null)
            {
                VRPlayerController vpcMain = mainCamera.GetComponent<VRPlayerController>();
                if (vpcMain != null) vpcMain.enabled = false;

                CharacterController ccMain = mainCamera.GetComponent<CharacterController>();
                if (ccMain != null) ccMain.enabled = false;
            }

            if (vrCamera != null)
            {
                VRPlayerController vpcCam = vrCamera.GetComponent<VRPlayerController>();
                if (vpcCam != null) vpcCam.enabled = false;

                CharacterController ccCam = vrCamera.GetComponent<CharacterController>();
                if (ccCam != null) ccCam.enabled = false;
            }

            if (vrPlayerRig != null)
            {
                VRPlayerController vpcRig = vrPlayerRig.GetComponent<VRPlayerController>();
                if (vpcRig != null) vpcRig.enabled = false;

                CharacterController ccRig = vrPlayerRig.GetComponent<CharacterController>();
                if (ccRig != null) ccRig.enabled = false;
            }
        }

        // Reset semua camera ke non-aktif dulu secara aman
        if (mainCamera != null) mainCamera.gameObject.SetActive(false);
        if (topDownCamera != null) topDownCamera.gameObject.SetActive(false);
        if (vrCamera != null) vrCamera.gameObject.SetActive(false);
        if (vrPlayerRig != null) vrPlayerRig.SetActive(false);

        float recommendedDist;
        Vector3 yardCenter = GetYardsWorldCenter(out recommendedDist);

        switch (mode)
        {
            case CameraViewMode.BirdView3D:
                if (mainCamera != null)
                {
                    // Pastikan CharacterController & VRPlayerController 100% mati di mode 3D
                    CharacterController ccMain = mainCamera.GetComponent<CharacterController>();
                    if (ccMain != null) ccMain.enabled = false;

                    VRPlayerController vpcMain = mainCamera.GetComponent<VRPlayerController>();
                    if (vpcMain != null) vpcMain.enabled = false;

                    mainCamera.transform.SetParent(null); // Lepas dari VR Rig
                    mainCamera.gameObject.SetActive(true);
                    mainCamera.farClipPlane = 2000f;

                    // Pastikan OrbitCamera terpasang dan aktif di mainCamera
                    if (orbitCam == null) orbitCam = mainCamera.GetComponent<OrbitCamera>();
                    if (orbitCam == null) orbitCam = mainCamera.gameObject.AddComponent<OrbitCamera>();

                    orbitCam.enabled = true;

                    float pitch = 35f;
                    float yaw = 45f;
                    orbitCam.SetCameraAngle(pitch, yaw, recommendedDist, yardCenter);

                    // Posisikan langsung mainCamera di udara melayang melihat seluruh yard
                    Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
                    Vector3 pos = yardCenter - (rot * Vector3.forward * recommendedDist);
                    mainCamera.transform.rotation = rot;
                    mainCamera.transform.position = pos;
                }
                break;

            case CameraViewMode.TopDown2D:
                if (topDownCamera != null)
                {
                    topDownCamera.gameObject.SetActive(true);
                    topDownCamera.farClipPlane = 2000f;
                    FocusTarget2D(yardCenter);
                }
                else if (mainCamera != null)
                {
                    mainCamera.gameObject.SetActive(true);
                }
                break;

            case CameraViewMode.VRView:
                // Tentukan kamera mana yang dipakai untuk VR (vrCamera atau mainCamera)
                Camera activeVRCam = vrCamera != null ? vrCamera : mainCamera;

                // Matikan skrip terbang OrbitCamera di SEMUA kamera!
                if (orbitCam != null) orbitCam.enabled = false;
                if (activeVRCam != null)
                {
                    OrbitCamera activeOrbit = activeVRCam.GetComponent<OrbitCamera>();
                    if (activeOrbit != null) activeOrbit.enabled = false;
                }

                if (vrPlayerRig != null)
                {
                    vrPlayerRig.SetActive(true);

                    VRPlayerController vpc = vrPlayerRig.GetComponent<VRPlayerController>();
                    if (vpc != null)
                    {
                        vpc.enabled = true;
                        vpc.vrCamera = activeVRCam != null ? activeVRCam.transform : null;
                        vpc.SnapToFloor();
                    }
                    else
                    {
                        CharacterController cc = vrPlayerRig.GetComponent<CharacterController>();
                        if (cc != null) cc.enabled = false;
                        Vector3 rigPos = vrPlayerRig.transform.position;
                        rigPos.y = 19.0f;
                        vrPlayerRig.transform.position = rigPos;
                        if (cc != null) cc.enabled = true;
                    }
                }

                if (activeVRCam != null)
                {
                    activeVRCam.gameObject.SetActive(true);
                    activeVRCam.farClipPlane = 2000f;
                    activeVRCam.nearClipPlane = 0.08f; // Cegah clipping kamera saat berdiri sangat dekat dengan dinding kontainer

                    // Pastikan VRPlayerController terpasang dan AKTIF untuk mengontrol WASD & Mouse!
                    VRPlayerController camVpc = activeVRCam.GetComponent<VRPlayerController>();
                    if (camVpc == null && (vrPlayerRig == null || vrPlayerRig.GetComponent<VRPlayerController>() == null))
                    {
                        camVpc = activeVRCam.gameObject.AddComponent<VRPlayerController>();
                    }

                    if (camVpc != null)
                    {
                        camVpc.enabled = true;
                        camVpc.vrCamera = activeVRCam.transform;
                        camVpc.SnapToFloor();
                    }

                    if (vrPlayerRig != null)
                    {
                        Transform camParent = vrPlayerRig.transform.Find("Camera Offset");
                        if (camParent == null) camParent = vrPlayerRig.transform;
                        activeVRCam.transform.SetParent(camParent);
                        activeVRCam.transform.localPosition = new Vector3(0, 1.7f, 0);
                        activeVRCam.transform.localRotation = Quaternion.identity;
                    }
                    else
                    {
                        activeVRCam.transform.SetParent(null);
                        Vector3 camPos = activeVRCam.transform.position;
                        camPos.y = 20.7f;
                        activeVRCam.transform.position = camPos;
                    }
                }
                break;
        }

        // Pengaman: Jika tidak ada kamera yang aktif, paksa mainCamera aktif!
        if ((mainCamera == null || !mainCamera.gameObject.activeSelf) &&
            (topDownCamera == null || !topDownCamera.gameObject.activeSelf) &&
            (vrCamera == null || !vrCamera.gameObject.activeSelf))
        {
            if (mainCamera != null) mainCamera.gameObject.SetActive(true);
        }

        Debug.Log("View Mode Switched To: " + mode.ToString());
    }

    void Update()
    {
        // Controls for 2D Top-Down Camera Pan & Zoom
        if (currentMode == CameraViewMode.TopDown2D && topDownCamera != null)
        {
            HandleTopDownControls();
        }
    }

    void HandleTopDownControls()
    {
        // 2D Pan with Left Drag or WASD
        if (Input.GetMouseButton(0) || Input.GetMouseButton(2))
        {
            float panX = -Input.GetAxis("Mouse X") * topDownCamera.orthographicSize * 0.05f;
            float panZ = -Input.GetAxis("Mouse Y") * topDownCamera.orthographicSize * 0.05f;

            topDownTargetPos += new Vector3(panX, 0, panZ);
        }

        // WASD / Arrow keys
        float moveX = Input.GetAxis("Horizontal") * topDownCamera.orthographicSize * 0.8f * Time.deltaTime;
        float moveZ = Input.GetAxis("Vertical") * topDownCamera.orthographicSize * 0.8f * Time.deltaTime;
        topDownTargetPos += new Vector3(moveX, 0, moveZ);

        // Scroll Zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            topDownCamera.orthographicSize = Mathf.Clamp(topDownCamera.orthographicSize - scroll * 15f, minOrthoSize, maxOrthoSize);
        }

        // Update Y height
        topDownTargetPos.y = topDownHeight;
        topDownCamera.transform.position = Vector3.Lerp(topDownCamera.transform.position, topDownTargetPos, Time.deltaTime * 10f);
    }

    public void FocusTarget2D(Vector3 targetPos)
    {
        topDownHeight = targetPos.y + 100f;
        topDownTargetPos = new Vector3(targetPos.x, topDownHeight, targetPos.z);
        topDownCamera.orthographicSize = 35f; // Zoom in on search target
    }

    public Camera GetActiveCamera()
    {
        switch (currentMode)
        {
            case CameraViewMode.TopDown2D:
                return topDownCamera;
            case CameraViewMode.VRView:
                return vrCamera != null ? vrCamera : mainCamera;
            default:
                return mainCamera;
        }
    }

    void OnGUI()
    {
        // 1. Jika user sudah memasang UI Button dari Canvas, sembunyikan OnGUI sepenuhnya
        if (button3DView != null || button2DView != null || buttonWalkView != null)
            return;

        // 2. Jika user memasukkan Texture2D (PNG) di Inspector ViewModeManager
        bool hasCustomIcons = (icon3DView != null || icon2DView != null || iconWalkView != null);

        if (hasCustomIcons)
        {
            float w = buttonSize.x;
            float h = buttonSize.y;
            float totalWidth = (3 * w) + (2 * buttonSpacing);
            float startX = (Screen.width - totalWidth) / 2f;
            float startY = topMargin;

            // Render masing-masing tombol PNG secara bersih & penuh tanpa kotak abu-abu bawaan
            DrawCustomViewButton(new Rect(startX, startY, w, h), icon3DView, "3D View", CameraViewMode.BirdView3D, SwitchTo3DView);
            DrawCustomViewButton(new Rect(startX + w + buttonSpacing, startY, w, h), icon2DView, "2D View", CameraViewMode.TopDown2D, SwitchTo2DView);
            DrawCustomViewButton(new Rect(startX + (w + buttonSpacing) * 2, startY, w, h), iconWalkView, "Walk View", CameraViewMode.VRView, SwitchToWalkView);

            return;
        }

        if (!showLegacyOnGUI) return;

        // 3. Fallback Tombol OnGUI Bawaan (Kotak Teks Standar)
        float btnWidth = 125f;
        float btnHeight = 35f;
        float spacing = 10f;
        float legacyStartX = (Screen.width - (3 * btnWidth + 2 * spacing)) / 2f;
        float legacyStartY = 15f;

        GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        GUI.Box(new Rect(legacyStartX - 10, legacyStartY - 5, (3 * btnWidth + 2 * spacing) + 20, btnHeight + 10), "");

        // 3D View Button
        GUI.color = currentMode == CameraViewMode.BirdView3D ? Color.yellow : Color.white;
        if (GUI.Button(new Rect(legacyStartX, legacyStartY, btnWidth, btnHeight), "🌐 3D View"))
        {
            SwitchTo3DView();
        }

        // 2D View Button
        GUI.color = currentMode == CameraViewMode.TopDown2D ? Color.yellow : Color.white;
        if (GUI.Button(new Rect(legacyStartX + btnWidth + spacing, legacyStartY, btnWidth, btnHeight), "🗺️ 2D View"))
        {
            SwitchTo2DView();
        }

        // Walk View Button
        GUI.color = currentMode == CameraViewMode.VRView ? Color.yellow : Color.white;
        if (GUI.Button(new Rect(legacyStartX + (btnWidth + spacing) * 2, legacyStartY, btnWidth, btnHeight), "🚶 Walk View"))
        {
            SwitchToWalkView();
        }

        GUI.color = Color.white;
    }

    void DrawCustomViewButton(Rect rect, Texture2D icon, string fallbackText, CameraViewMode mode, System.Action onClick)
    {
        bool isActive = (currentMode == mode);
        bool isHovered = rect.Contains(Event.current.mousePosition);

        // Sedikit membesar saat hover atau aktif agar terasa responsif
        Rect drawRect = rect;
        if (isActive)
        {
            drawRect = new Rect(rect.x - 3, rect.y - 3, rect.width + 6, rect.height + 6);
        }
        else if (isHovered)
        {
            drawRect = new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2);
        }

        // Atur transparansi: mode aktif / hover = 100% terang, mode tidak aktif = sedikit lembut
        Color prevColor = GUI.color;
        if (isActive)
        {
            GUI.color = Color.white;
        }
        else if (isHovered)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.95f);
        }
        else
        {
            GUI.color = new Color(0.88f, 0.88f, 0.88f, 0.80f);
        }

        // Gambar tekstur PNG tombol secara penuh & proporsional
        if (icon != null)
        {
            GUI.DrawTexture(drawRect, icon, ScaleMode.ScaleToFit, true);
        }
        else
        {
            // Fallback jika salah satu icon kosong
            GUI.Button(drawRect, fallbackText);
        }

        // Tombol klik transparan di atas gambar (tanpa frame kotak abu-abu bawaan Unity)
        if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
        {
            onClick?.Invoke();
        }

        GUI.color = prevColor;
    }
}
