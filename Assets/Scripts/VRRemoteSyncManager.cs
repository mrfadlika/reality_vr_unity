using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Runtime.InteropServices;

// ============================================================
// VRRemoteSyncManager.cs
// Sistem sinkronisasi WebRTC:
// 1. Laptop (Host/Operator): Mengendalikan jalan (WASD) & memunculkan QR Code
// 2. HP Android (Viewer/VR Case): Menampilkan 3D Cardboard Stereo + Gyroscope 360°
// Tanpa SteamVR, tanpa RiftCat, tanpa instal aplikasi apapun di HP!
// ============================================================

public class VRRemoteSyncManager : MonoBehaviour
{
    public static VRRemoteSyncManager Instance { get; private set; }

    [Header("Role & State")]
    public bool isHost = false;
    public bool isViewer = false;
    public string roomCode = "";
    public bool isConnected = false;
    public string connectedPeerId = "";
    public int sentPackets = 0;
    public int receivedPackets = 0;

    [Header("Frontend (FE) Settings")]
    [Tooltip("URL Frontend (FE) WebGL Anda. Di WebGL build otomatis mengambil domain browser saat ini.")]
    public string frontendBaseUrl = "";

    [Header("UI Host Display")]
    public bool showHostModal = false;
    public bool isHostPeerReady = false;
    public Texture2D qrCodeTexture;
    public string fullViewerUrl = "";
    private bool isFetchingQr = false;

    [Header("Custom UI Button Images (Tarik file PNG ke sini)")]
    [Tooltip("Tarik gambar PNG untuk tombol Hubungkan HP VR")]
    public Texture2D buttonConnectImage;
    [Tooltip("Tarik gambar PNG untuk tombol saat HP VR Terhubung (Opsional)")]
    public Texture2D buttonConnectedImage;

    [Header("Sync Target References")]
    public Transform playerRigTransform;

    [Header("Editor Testing")]
    [Tooltip("Centang untuk simulasi sebagai HP Klien (Viewer) saat tes di Unity Editor")]
    public bool simulateAsViewerInEditor = false;

    // Packet lerping untuk viewer
    private Vector3 targetRigPosition;
    private float targetRigYaw;
    private bool hasReceivedFirstPacket = false;
    private float lastSendTime = 0f;
    private const float SendInterval = 0.033f; // 30 FPS sync

    // Sinkronisasi Orientasi Kepala (Head Rotation) dari HP VR ke Laptop
    [HideInInspector] public bool hasReceivedHeadPacket = false;
    [HideInInspector] public float targetHeadYaw = 0f;
    [HideInInspector] public float targetHeadPitch = 0f;
    [HideInInspector] public float targetHeadRoll = 0f;
    [HideInInspector] public int receivedHeadPackets = 0;
    private float lastViewerSendTime = 0f;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void VRRemote_StartHost(string roomCode);

    [DllImport("__Internal")]
    private static extern void VRRemote_ConnectViewer(string roomCode);

    [DllImport("__Internal")]
    private static extern void VRRemote_SendMovement(string dataStr);

    [DllImport("__Internal")]
    private static extern string VRRemote_GetUrlQuery();

    [DllImport("__Internal")]
    private static extern string VRRemote_GetAbsoluteUrl();
#endif

    void Awake()
    {
        gameObject.name = "VRRemoteSyncManager"; // Wajib persis agar SendMessage dari JS WebGL selalu sampai

        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        DontDestroyOnLoad(gameObject);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("VRRemoteSyncManager");
            go.AddComponent<VRRemoteSyncManager>();
        }
    }

    void AutoLoadConnectButtonImage()
    {
        if (buttonConnectImage != null) return;
#if UNITY_EDITOR
        buttonConnectImage = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/button/ConnectPhone.png");
#endif
        if (buttonConnectImage == null && System.IO.File.Exists("Assets/button/ConnectPhone.png"))
        {
            try
            {
                byte[] bytes = System.IO.File.ReadAllBytes("Assets/button/ConnectPhone.png");
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(bytes))
                {
                    buttonConnectImage = tex;
                }
            }
            catch {}
        }
    }

    void Start()
    {
        AutoLoadConnectButtonImage();

        if (buttonConnectImage == null && ViewModeManager.Instance != null && ViewModeManager.Instance.iconConnectVR != null)
        {
            buttonConnectImage = ViewModeManager.Instance.iconConnectVR;
        }

        DetectRoleFromUrl();

        if (isViewer)
        {
            StartCoroutine(SetupViewerModeRoutine());
        }
    }

    public void InitFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        Debug.Log("[VRRemote] InitFromUrl called with: " + url);

        if (url.Contains("role=viewer"))
        {
            isViewer = true;
            isHost = false;
        }

        int roomIdx = url.IndexOf("room=");
        if (roomIdx >= 0)
        {
            string sub = url.Substring(roomIdx + 5);
            int endIdx = sub.IndexOf('&');
            roomCode = endIdx >= 0 ? sub.Substring(0, endIdx) : sub;
        }

        if (isViewer)
        {
            StopAllCoroutines();
            StartCoroutine(SetupViewerModeRoutine());
        }
    }

    void DetectRoleFromUrl()
    {
        string query = "";
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            query = VRRemote_GetUrlQuery();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[VRRemote] GetUrlQuery error: " + ex.Message);
        }
#endif

        if (string.IsNullOrEmpty(query))
        {
            query = Application.absoluteURL;
        }

        // Cek URL params: ?room=1234&role=viewer
        if (!string.IsNullOrEmpty(query))
        {
            if (query.Contains("role=viewer"))
            {
                isViewer = true;
                isHost = false;
            }
            else if (query.Contains("role=host"))
            {
                isHost = true;
                isViewer = false;
            }

            int roomIdx = query.IndexOf("room=");
            if (roomIdx >= 0)
            {
                string sub = query.Substring(roomIdx + 5);
                int endIdx = sub.IndexOf('&');
                roomCode = endIdx >= 0 ? sub.Substring(0, endIdx) : sub;
            }
        }

        // Fallback untuk simulasi di Unity Editor
        if (Application.isEditor && simulateAsViewerInEditor)
        {
            isViewer = true;
            isHost = false;
            if (string.IsNullOrEmpty(roomCode)) roomCode = "58219";
        }
        else if (!isViewer)
        {
            isHost = true;
        }

        Debug.Log("[VRRemote] Detected Role: " + (isViewer ? "VIEWER (Mobile HP)" : "HOST (Laptop)") + " | Room: " + roomCode);
    }

    IEnumerator SetupViewerModeRoutine()
    {
        yield return new WaitForSeconds(0.3f);

        // 1. Masuk ke Walk View otomatis di HP
        if (ViewModeManager.Instance != null)
        {
            ViewModeManager.Instance.SwitchToWalkView();
        }

        yield return new WaitForSeconds(0.2f);

        // 2. KUNCI PENTING: Matikan CharacterController di HP agar tidak mengunci / memblokir posisi dari Laptop!
        Transform rig = GetPlayerRig();
        if (rig != null)
        {
            CharacterController cc = rig.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            VRPlayerController vpc = rig.GetComponent<VRPlayerController>();
            if (vpc != null) vpc.enabled = false;
        }

        // Matikan semua CharacterController & VRPlayerController di scene HP agar tidak menimpa posisi
        foreach (CharacterController c in FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
        {
            c.enabled = false;
        }
        foreach (VRPlayerController v in FindObjectsByType<VRPlayerController>(FindObjectsSortMode.None))
        {
            v.enabled = false;
        }

        // 3. PENTING: Hancurkan GyroCamera jika ada karena WebXR Cardboard sudah memiliki tracking internal!
        // GyroCamera merusak hirarki kamera (unparent dari XRRig) dan memutar kamera 90 derajat miring!
        foreach (GyroCamera g in FindObjectsByType<GyroCamera>(FindObjectsSortMode.None))
        {
            Destroy(g);
        }

        // Pastikan kamera utama tetap terhubung di bawah XRRig / Camera Offset
        Camera activeCam = Camera.main;
        if (rig != null && activeCam != null)
        {
            Transform camOffset = rig.Find("Camera Offset");
            if (camOffset != null && activeCam.transform.parent != camOffset)
            {
                activeCam.transform.SetParent(camOffset);
                activeCam.transform.localPosition = Vector3.zero;
                activeCam.transform.localRotation = Quaternion.identity;
            }
            else if (camOffset == null && activeCam.transform.parent != rig)
            {
                activeCam.transform.SetParent(rig);
                activeCam.transform.localPosition = new Vector3(0, 1.7f, 0);
                activeCam.transform.localRotation = Quaternion.identity;
            }
        }

        // 4. JANGAN gunakan split-screen manual VRModeManager karena WebXR Cardboard sudah me-render stereo 2 mata sendiri!
        VRModeManager vmm = FindFirstObjectByType<VRModeManager>();
        if (vmm != null)
        {
            vmm.SetSplitScreenVR(false); // Kembalikan ke full 1 layar bersih agar WebXR Cardboard tidak terpotong 4
        }

        // 5. Hubungkan ke Host WebRTC
        if (!string.IsNullOrEmpty(roomCode))
        {
            ConnectAsViewer(roomCode);
        }
    }

    // --- Host Functions ---

    public void SetFrontendUrl(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            frontendBaseUrl = url;
            Debug.Log("[VRRemote] Frontend URL updated from WebGL browser: " + url);
        }
    }

    public string GetResolvedFrontendBaseUrl()
    {
        if (!string.IsNullOrEmpty(frontendBaseUrl))
        {
            return frontendBaseUrl;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            string abs = VRRemote_GetAbsoluteUrl();
            if (!string.IsNullOrEmpty(abs)) return abs;
        }
        catch {}
#endif

        if (!string.IsNullOrEmpty(Application.absoluteURL))
        {
            return Application.absoluteURL;
        }

        return "https://frolicking-tartufo-a2eb0a.netlify.app/";
    }

    public void OpenHostModal()
    {
        showHostModal = true;

        if (string.IsNullOrEmpty(roomCode))
        {
            // ID unik berbasis waktu (hex) + 4 digit acak agar tidak pernah terjadi ID-TAKEN
            string ticksHex = System.DateTime.UtcNow.Ticks.ToString("x");
            string shortHex = ticksHex.Length > 8 ? ticksHex.Substring(ticksHex.Length - 8) : ticksHex;
            roomCode = "eagle-" + shortHex + "-" + Random.Range(1000, 9999);
        }

        string baseUrl = GetResolvedFrontendBaseUrl();
        fullViewerUrl = baseUrl + (baseUrl.Contains("?") ? "&" : "?") + "room=" + roomCode + "&role=viewer";

        // Di laptop: otomatis beralih ke Walk View agar tombol WASD langsung menggerakkan avatar
        if (ViewModeManager.Instance != null && ViewModeManager.Instance.currentMode != CameraViewMode.VRView)
        {
            ViewModeManager.Instance.SwitchToWalkView();
        }

        // Langsung unduh QR Code agar langsung muncul di layar tanpa menunggu
        if (qrCodeTexture == null && !isFetchingQr)
        {
            StartCoroutine(FetchQrCodeRoutine(fullViewerUrl));
        }

        StartHost(roomCode);
    }

    public void CloseHostModal()
    {
        showHostModal = false;
    }

    public void StartHost(string code = "")
    {
        isHost = true;
        isViewer = false;
        isConnected = false;

        if (!string.IsNullOrEmpty(code))
        {
            roomCode = code;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            VRRemote_StartHost(roomCode);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[VRRemote] StartHost error: " + ex.Message);
        }
#else
        // Simulasi di Unity Editor
        isHostPeerReady = true;
#endif
        Debug.Log("[VRRemote] Started Host with Room Code: " + roomCode);
    }

    public void OnHostPeerReady(string realRoomId)
    {
        roomCode = realRoomId;
        isHostPeerReady = true;
        string baseUrl = GetResolvedFrontendBaseUrl();
        fullViewerUrl = baseUrl + (baseUrl.Contains("?") ? "&" : "?") + "room=" + realRoomId + "&role=viewer";
        Debug.Log("[VRRemote] Host Peer SUKSES Terdaftar di Server! Room: " + realRoomId + " | URL: " + fullViewerUrl);
        
        if (qrCodeTexture == null && !isFetchingQr)
        {
            StartCoroutine(FetchQrCodeRoutine(fullViewerUrl));
        }
    }

    IEnumerator FetchQrCodeRoutine(string urlToEncode)
    {
        isFetchingQr = true;
        string apiUrl = "https://api.qrserver.com/v1/create-qr-code/?size=260x260&margin=10&data=" + UnityWebRequest.EscapeURL(urlToEncode);

        using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(apiUrl))
        {
            yield return uwr.SendWebRequest();

            if (uwr.result == UnityWebRequest.Result.Success)
            {
                qrCodeTexture = DownloadHandlerTexture.GetContent(uwr);
            }
            else
            {
                Debug.LogWarning("[VRRemote] QR Code download failed: " + uwr.error);
            }
        }
        isFetchingQr = false;
    }

    // --- Viewer Functions ---

    public void ConnectAsViewer(string code)
    {
        roomCode = code;
        isViewer = true;
        isHost = false;

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            VRRemote_ConnectViewer(code);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[VRRemote] ConnectViewer error: " + ex.Message);
        }
#endif
        Debug.Log("[VRRemote] Connecting to Host Room: " + code);
    }

    // --- Update Loop (Sending & Receiving) ---

    void Update()
    {
        // 1. Host (Laptop): Kirim posisi WASD ke HP Klien & Ikuti Tengokan Kepala HP
        if (isHost && isConnected)
        {
            if (Time.time - lastSendTime >= SendInterval)
            {
                lastSendTime = Time.time;
                SendCurrentPosition();
            }

            // Jika HP VR sudah mengirim orientasi kepala, putar kamera & badan player di laptop
            if (hasReceivedHeadPacket)
            {
                ApplyRemoteHeadRotationToHost();
            }
        }

        // 2. Viewer (HP Android): Kirim tengokan kepala ke Laptop & Haluskan posisi jalan dari laptop
        if (isViewer && isConnected)
        {
            if (Time.time - lastViewerSendTime >= SendInterval)
            {
                lastViewerSendTime = Time.time;
                SendViewerHeadRotation();
            }

            if (hasReceivedFirstPacket)
            {
                Transform rig = GetPlayerRig();
                if (rig != null)
                {
                    rig.position = Vector3.Lerp(rig.position, targetRigPosition, Time.deltaTime * 20f);
                }
            }
        }
    }

    void SendViewerHeadRotation()
    {
        Camera cam = Camera.main;
        if (cam == null && ViewModeManager.Instance != null) cam = ViewModeManager.Instance.vrCamera;
        if (cam == null) return;

        // Ambil rotasi sudut kamera (head orientation) di HP Android
        Vector3 euler = cam.transform.rotation.eulerAngles;
        float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        float yaw = euler.y;
        float roll = euler.z > 180f ? euler.z - 360f : euler.z;

        string packet = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "H:{0:F1},{1:F1},{2:F1}", pitch, yaw, roll);

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            VRRemote_SendMovement(packet);
        }
        catch {}
#endif
    }

    void ApplyRemoteHeadRotationToHost()
    {
        Transform rig = GetPlayerRig();
        if (rig == null) return;

        // Putar badan avatar secara horizontal (Yaw) mengikuti arah tengokan HP
        float curYaw = rig.eulerAngles.y;
        float newYaw = Mathf.LerpAngle(curYaw, targetHeadYaw, Time.deltaTime * 25f);
        rig.rotation = Quaternion.Euler(0f, newYaw, 0f);

        // Putar kamera laptop secara vertikal (Pitch) & Roll
        Camera hostCam = Camera.main;
        if (hostCam == null && ViewModeManager.Instance != null) hostCam = ViewModeManager.Instance.vrCamera;
        if (hostCam != null)
        {
            float curPitch = hostCam.transform.localEulerAngles.x;
            if (curPitch > 180f) curPitch -= 360f;
            float newPitch = Mathf.Lerp(curPitch, targetHeadPitch, Time.deltaTime * 25f);

            float curRoll = hostCam.transform.localEulerAngles.z;
            if (curRoll > 180f) curRoll -= 360f;
            float newRoll = Mathf.Lerp(curRoll, targetHeadRoll, Time.deltaTime * 25f);

            hostCam.transform.localRotation = Quaternion.Euler(newPitch, 0f, newRoll);

            // Sinkronkan ke VRPlayerController agar mouse tidak snapping
            VRPlayerController vpc = rig.GetComponent<VRPlayerController>();
            if (vpc != null)
            {
                vpc.SetExternalLookAngles(newPitch, newYaw);
            }
        }
    }

    void SendCurrentPosition()
    {
        Transform rig = GetPlayerRig();
        if (rig == null) return;

        Vector3 pos = rig.position;
        float yaw = rig.eulerAngles.y;

        string packet = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:F2},{1:F2},{2:F2},{3:F1}", pos.x, pos.y, pos.z, yaw);

        sentPackets++;

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            VRRemote_SendMovement(packet);
        }
        catch {}
#endif
    }

    Transform GetPlayerRig()
    {
        if (playerRigTransform != null && playerRigTransform.gameObject.activeInHierarchy)
            return playerRigTransform;

        if (ViewModeManager.Instance != null && ViewModeManager.Instance.vrPlayerRig != null)
        {
            playerRigTransform = ViewModeManager.Instance.vrPlayerRig.transform;
            return playerRigTransform;
        }

        GameObject rig = GameObject.Find("XRRig");
        if (rig == null) rig = GameObject.Find("XR Rig");
        if (rig != null)
        {
            playerRigTransform = rig.transform;
            return playerRigTransform;
        }

        if (Camera.main != null) return Camera.main.transform;
        return null;
    }

    // --- JS Callbacks (Dipanggil dari JavaScript index.html) ---

    public void OnClientConnected(string peerId)
    {
        isConnected = true;
        connectedPeerId = peerId;
        Debug.Log("[VRRemote] OnClientConnected: " + peerId);

        // Otomatis kirim sinkronisasi layout yard dan gedung ke HP VR
        StopCoroutine("SyncLayoutRoutine");
        StartCoroutine(SyncLayoutRoutine());

        // Otomatis tutup modal setelah 1.5 detik agar operator di laptop bisa langsung gerak pakai WASD
        StopCoroutine("AutoCloseModalRoutine");
        StartCoroutine(AutoCloseModalRoutine());
    }

    IEnumerator SyncLayoutRoutine()
    {
        yield return new WaitForSeconds(0.4f);
        SyncLayoutToViewer();
        yield return new WaitForSeconds(1.2f);
        SyncLayoutToViewer();
    }

    public void SyncLayoutToViewer()
    {
        if (!isHost || !isConnected) return;
        string layoutJson = YardEditorDataUtil.GetSavedLayoutJson();
        if (!string.IsNullOrEmpty(layoutJson))
        {
            Debug.Log("[VRRemote] Mengirim Sinkronisasi Layout ke HP VR (" + layoutJson.Length + " bytes)...");
            string packet = "LAYOUT:" + layoutJson;
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                VRRemote_SendMovement(packet);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[VRRemote] Send layout error: " + ex.Message);
            }
#endif
        }
    }

    IEnumerator AutoCloseModalRoutine()
    {
        yield return new WaitForSeconds(1.5f);
        if (isConnected)
        {
            CloseHostModal();
        }
    }

    public void OnClientDisconnected(string reason)
    {
        isConnected = false;
        connectedPeerId = "";
        Debug.Log("[VRRemote] OnClientDisconnected: " + reason);
    }

    public void OnConnectedToHost(string hostPeerId)
    {
        isConnected = true;
        connectedPeerId = hostPeerId;
        Debug.Log("[VRRemote] OnConnectedToHost: " + hostPeerId);

        // Minta layout terbaru dari laptop
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            VRRemote_SendMovement("REQ_LAYOUT");
        }
        catch {}
#endif
    }

    public void OnDisconnectedFromHost(string reason)
    {
        isConnected = false;
        connectedPeerId = "";
        Debug.Log("[VRRemote] OnDisconnectedFromHost: " + reason);
    }

    public void OnMovementPacketReceived(string packet)
    {
        if (string.IsNullOrEmpty(packet)) return;

        // 1. Permintaan layout dari Viewer ke Host
        if (packet == "REQ_LAYOUT" && isHost)
        {
            SyncLayoutToViewer();
            return;
        }

        // 2. Paket Sinkronisasi Layout Yard & Gedung dari Laptop ke HP VR
        if (packet.StartsWith("LAYOUT:"))
        {
            string layoutJson = packet.Substring(7);
            Debug.Log("[VRRemote] Menerima Sinkronisasi Layout dari Laptop (" + layoutJson.Length + " bytes)!");
            if (YardEditorManager.Instance != null)
            {
                YardEditorManager.Instance.ApplyLayoutJson(layoutJson);
            }
            // Simpan juga ke cache HP agar tersimpan di HP
            YardEditorDataUtil.SaveLayoutJsonDirectly(layoutJson);
            return;
        }

        // 3. Jika paket berisi data orientasi kepala (dari Viewer ke Host)
        if (packet.StartsWith("H:"))
        {
            OnHeadPacketReceived(packet);
            return;
        }

        string[] parts = packet.Split(',');
        if (parts.Length >= 4)
        {
            float x, y, z, yaw;
            if (float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) &&
                float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y) &&
                float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z) &&
                float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out yaw))
            {
                targetRigPosition = new Vector3(x, y, z);
                targetRigYaw = yaw;
                receivedPackets++;

                Transform rig = GetPlayerRig();
                if (rig != null)
                {
                    // Pastikan CharacterController non-aktif agar tidak menolak posisi
                    CharacterController cc = rig.GetComponent<CharacterController>();
                    if (cc != null && cc.enabled) cc.enabled = false;

                    if (!hasReceivedFirstPacket)
                    {
                        rig.position = targetRigPosition;
                        hasReceivedFirstPacket = true;
                    }
                }
            }
        }
    }

    public void OnHeadPacketReceived(string packet)
    {
        if (string.IsNullOrEmpty(packet)) return;
        if (!packet.StartsWith("H:")) return;

        string content = packet.Substring(2);
        string[] parts = content.Split(',');
        if (parts.Length >= 2)
        {
            float pitch, yaw, roll = 0f;
            if (float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out pitch) &&
                float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out yaw))
            {
                if (parts.Length >= 3)
                {
                    float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out roll);
                }

                targetHeadPitch = pitch;
                targetHeadYaw = yaw;
                targetHeadRoll = roll;
                hasReceivedHeadPacket = true;
                receivedHeadPackets++;
            }
        }
    }

    // --- GUI: Tombol Buka QR & Modal Dialog di Laptop ---

    void OnGUI()
    {
        if (!Application.isPlaying) return;

        // Jika di HP (Viewer Mode): Tampilkan status kecil di atas bahwa HP sudah terhubung
        if (isViewer)
        {
            GUI.color = isConnected ? new Color(0.2f, 0.95f, 0.45f, 0.95f) : new Color(1f, 0.75f, 0.2f, 0.95f);
            GUI.skin.label.alignment = TextAnchor.UpperLeft;
            GUI.skin.label.fontSize = 12;
            string statusStr = isConnected
                ? "🟢 Terhubung ke Laptop | Paket: " + receivedPackets
                : ("⏳ Menghubungkan ke Laptop...\nRoom: " + (roomCode.Length > 24 ? roomCode.Substring(0, 24) + "..." : roomCode));
            GUI.Label(new Rect(12, 12, 380, 40), statusStr);
            GUI.color = Color.white;
            return;
        }

        // 1. Tombol Hubungkan HP VR (Di Pojok Kiri Atas Laptop)
        AutoLoadConnectButtonImage();
        if (buttonConnectImage == null && ViewModeManager.Instance != null && ViewModeManager.Instance.iconConnectVR != null)
        {
            buttonConnectImage = ViewModeManager.Instance.iconConnectVR;
        }

        if (buttonConnectImage != null)
        {
            float imgW = 180f;
            float imgH = 60f;
            Rect btnRect = new Rect(20f, 20f, imgW, imgH);
            bool isHover = btnRect.Contains(Event.current.mousePosition);
            Rect drawRect = isHover ? new Rect(btnRect.x - 1, btnRect.y - 1, btnRect.width + 2, btnRect.height + 2) : btnRect;

            Texture2D texToDraw = (isConnected && buttonConnectedImage != null) ? buttonConnectedImage : buttonConnectImage;

            Color prevCol = GUI.color;
            if (isConnected) GUI.color = Color.white;
            else if (isHover) GUI.color = new Color(1f, 1f, 1f, 0.98f);
            else GUI.color = new Color(0.92f, 0.92f, 0.92f, 0.85f);

            GUI.DrawTexture(drawRect, texToDraw, ScaleMode.ScaleToFit, true);

            // Tampilkan badge status kecil di sudut jika sudah terhubung
            if (isConnected)
            {
                GUIStyle badgeStyle = new GUIStyle(GUI.skin.label);
                badgeStyle.fontSize = 10;
                badgeStyle.fontStyle = FontStyle.Bold;
                badgeStyle.alignment = TextAnchor.MiddleCenter;
                badgeStyle.normal.textColor = Color.white;

                GUI.color = new Color(0.12f, 0.82f, 0.38f, 0.92f);
                Rect badgeRect = new Rect(drawRect.x + drawRect.width - 64f, drawRect.y + drawRect.height - 18f, 62f, 16f);
                GUI.Box(badgeRect, "");
                GUI.Label(badgeRect, "↑" + sentPackets + " ↓" + receivedHeadPackets, badgeStyle);
            }

            GUI.color = prevCol;

            if (GUI.Button(btnRect, GUIContent.none, GUIStyle.none))
            {
                if (showHostModal) CloseHostModal();
                else OpenHostModal();
            }
        }
        else
        {
            // Default OnGUI Button jika belum ada file PNG
            float btnW = 210f;
            float btnH = 36f;
            Rect toggleBtnRect = new Rect(20f, 20f, btnW, btnH);

            Color prevColor = GUI.color;
            GUI.color = isConnected ? new Color(0.15f, 0.85f, 0.45f, 1f) : new Color(0.2f, 0.7f, 1f, 1f);

            string btnLabel = isConnected ? ("🟢 HP VR Terhubung (↑" + sentPackets + " ↓" + receivedHeadPackets + ")") : "📱 Hubungkan HP VR";
            if (GUI.Button(toggleBtnRect, btnLabel))
            {
                if (showHostModal) CloseHostModal();
                else OpenHostModal();
            }

            GUI.color = prevColor;
        }

        // 2. Modal Dialog QR Code Pop-up
        if (showHostModal)
        {
            DrawQrModalWindow();
        }
    }

    void DrawQrModalWindow()
    {
        float modalW = 380f;
        float modalH = 490f;
        float modalX = (Screen.width - modalW) / 2f;
        float modalY = (Screen.height - modalH) / 2f;

        Rect modalRect = new Rect(modalX, modalY, modalW, modalH);

        // Background gelap
        GUI.color = new Color(0.08f, 0.09f, 0.14f, 0.96f);
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), ""); // Dim overlay
        GUI.Box(modalRect, "");

        // Header Title
        GUI.color = Color.white;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.skin.label.fontStyle = FontStyle.Bold;
        GUI.skin.label.fontSize = 15;
        GUI.Label(new Rect(modalX + 10, modalY + 15, modalW - 20, 24), "HUBUNGKAN HP KE VR CASE");

        // Status Koneksi
        GUI.skin.label.fontSize = 12;
        if (isConnected)
        {
            GUI.color = new Color(0.25f, 0.95f, 0.45f, 1f);
            GUI.Label(new Rect(modalX + 10, modalY + 42, modalW - 20, 20), "🟢 Klien Terhubung! (Kirim Paket: " + sentPackets + ")");
        }
        else if (isHostPeerReady)
        {
            GUI.color = new Color(1f, 0.82f, 0.25f, 1f);
            GUI.Label(new Rect(modalX + 10, modalY + 42, modalW - 20, 20), "⏳ Menunggu Klien Scan QR Code...");
        }
        else
        {
            GUI.color = new Color(0.35f, 0.80f, 1f, 1f);
            GUI.Label(new Rect(modalX + 10, modalY + 42, modalW - 20, 20), "🔄 Mendaftarkan Sesi ke Server Signaling...");
        }

        // Gambar QR Code (Muncul segera setelah QR Code berhasil diambil)
        Rect qrRect = new Rect(modalX + (modalW - 210f) / 2f, modalY + 68f, 210f, 210f);
        GUI.color = Color.white;

        if (qrCodeTexture != null)
        {
            GUI.DrawTexture(qrRect, qrCodeTexture, ScaleMode.ScaleToFit);
        }
        else
        {
            GUI.Box(qrRect, isFetchingQr ? "Mengambil QR Code..." : "Menyiapkan QR Code...");
        }

        // Room Code Badge
        GUI.color = new Color(0.75f, 0.85f, 1f, 1f);
        GUI.skin.label.fontSize = 13;
        GUI.Label(new Rect(modalX + 10, modalY + 286f, modalW - 20, 20), "Kode Room: " + roomCode);

        // Field URL Frontend (FE)
        GUI.color = new Color(0.85f, 0.90f, 0.98f, 1f);
        GUI.skin.label.alignment = TextAnchor.MiddleLeft;
        GUI.skin.label.fontStyle = FontStyle.Normal;
        GUI.skin.label.fontSize = 11;
        GUI.Label(new Rect(modalX + 20, modalY + 312f, modalW - 40, 18), "Domain Frontend (FE):");

        string currentBase = GetResolvedFrontendBaseUrl();
        string editedBase = GUI.TextField(new Rect(modalX + 20, modalY + 332f, modalW - 110, 24), currentBase);
        if (editedBase != currentBase)
        {
            frontendBaseUrl = editedBase;
        }

        if (GUI.Button(new Rect(modalX + modalW - 84, modalY + 332f, 64, 24), "Update"))
        {
            string bUrl = GetResolvedFrontendBaseUrl();
            fullViewerUrl = bUrl + (bUrl.Contains("?") ? "&" : "?") + "room=" + roomCode + "&role=viewer";
            StartCoroutine(FetchQrCodeRoutine(fullViewerUrl));
        }

        // Petunjuk Singkat
        GUI.color = new Color(0.80f, 0.84f, 0.90f, 0.85f);
        GUI.skin.label.fontSize = 11;
        GUI.Label(new Rect(modalX + 20, modalY + 365f, modalW - 40, 60),
            "1. Arahkan kamera HP Android & scan QR di atas.\n" +
            "2. Pasang HP ke kacamata VR Case.\n" +
            "3. Kontrol jalan dari laptop menggunakan WASD.");

        // Tombol Salin URL & Tutup
        GUI.color = Color.white;
        if (GUI.Button(new Rect(modalX + 20, modalY + 440f, 130f, 32f), "Salin Link (Copy)"))
        {
            GUIUtility.systemCopyBuffer = fullViewerUrl;
        }

        if (GUI.Button(new Rect(modalX + modalW - 130 - 20, modalY + 440f, 130f, 32f), "Tutup (Esc)"))
        {
            CloseHostModal();
        }

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            CloseHostModal();
        }
    }
}
