using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

// ============================================================
// YardEditorManager.cs
// Orchestrator utama: fetch API, toggle mode (2D View otomatis),
// manage state, handle drag-drop interaction, save/load layout,
// spawn kontainer beserta nomor/ISO/TARE di pintu setelah edit,
// serta spawn aset aksesoris (Gedung, Reach Stacker, Top Loader, Gate, Pagar).
// ============================================================

[ExecuteAlways]
public class YardEditorManager : MonoBehaviour
{
    public static YardEditorManager Instance { get; private set; }

    [Header("API")]
    public string masterBlockApiUrl = "https://apps.globalhalalhub.co.id/global-halal-api/api/vr/getMasterBlock";
    public string containerLocApiUrl = "https://apps.globalhalalhub.co.id/global-halal-api/api/vr/getCtrLoc";

    // Layout denah awal yang dibaca dari folder data/denah.json
    private YardLayout loadedFolderDenah = null;

    [Header("Editor State")]
    public bool isEditorActive = false;

    [Header("Settings")]
    [Tooltip("Auto-load saved layout saat scene start / reload website")]
    public bool autoLoadOnStart = true;

    [Header("Yard Generation (Real-World Meters)")]
    [Tooltip("Jarak total antar slot (meter). Standar = 6.1")]
    public float slotSpacing = 6.1f;
    [Tooltip("Jarak total antar baris (meter). Standar = 2.8")]
    public float rowSpacing = 2.8f;
    [Tooltip("Padding ekstra dari tepi yard (meter) untuk ruang marka, nomor slot/row & logo")]
    public float yardPadding = 5.0f;
    [Tooltip("Padding sisi barat / kiri (meter). Standar = 8.8f (seimbang kiri & kanan agar tiang High Mast kedua sisi masuk)")]
    public float yardMarginLeft = 8.8f;
    [Tooltip("Padding sisi timur / kanan (meter). Standar = 8.8f (seimbang kiri & kanan agar tiang High Mast kedua sisi masuk)")]
    public float yardMarginRight = 8.8f;
    [Tooltip("Ketebalan yard block (tinggi Y)")]
    public float yardThickness = 0.05f;

    [Header("Yard Visual")]
    [Tooltip("Material untuk yard (opsional, jika kosong akan dibuat otomatis)")]
    public Material yardMaterial;

    [Header("Yard Prefabs")]
    [Tooltip("Master Prefab Yard jika menggunakan 1 prefab utama")]
    public GameObject masterYardPrefab;
    [Tooltip("Daftar Prefab Yard spesifik per blok (opsional)")]
    public GameObject[] yardPrefabs;

    [Header("1. Road Prefab & Material (Jalan)")]
    [Tooltip("Drag & Drop Prefab Jalan di sini (opsional). Jika diisi, jalan akan memunculkan prefab ini.")]
    public GameObject roadPrefab;
    [Tooltip("Material permukaan aspal jalan (opsional).")]
    public Material roadMaterial;

    [Header("2. Wall / Pagar Prefab (Dinding / Pagar Pembatas)")]
    [Tooltip("Drag & Drop Prefab Wall / Pagar di sini.")]
    public GameObject wallPrefab;
    [Tooltip("Drag & Drop FBX / Prefab Pagar di sini (alias lama)")]
    public GameObject pagarPrefab;

    [Header("3. Accessories Prefabs (Bangunan, Gerbang & Alat Berat)")]
    [Tooltip("Drag & Drop Prefab Building / Gedung di sini.")]
    public GameObject buildingPrefab;
    [Tooltip("Drag & Drop FBX / Prefab Gedung di sini (alias lama)")]
    public GameObject gedungPrefab;

    [Tooltip("Drag & Drop FBX / Prefab Gate (Gerbang) di sini")]
    public GameObject gatePrefab;

    [Tooltip("Drag & Drop FBX / Prefab Reach Stacker di sini")]
    public GameObject reachStackerPrefab;

    [Tooltip("Drag & Drop FBX / Prefab Top Loader di sini")]
    public GameObject topLoaderPrefab;

    [Header("Skala Ukuran Tombol (Scale)")]
    [Tooltip("Skala global untuk seluruh tombol di Yard Editor (1.0 = ukuran normal/default)")]
    [Range(0.2f, 3.0f)]
    public float globalButtonScale = 1.0f;

    [Tooltip("Skala tombol utama 'Yard Editor' di pojok kanan atas (mengikuti ukuran pixel asli PNG x skala)")]
    [Range(0.1f, 2.0f)]
    public float toggleButtonScale = 0.5f;

    [Tooltip("Skala untuk seluruh tombol di Sidebar (1.0 = normal proporsional)")]
    [Range(0.3f, 2.5f)]
    public float sidebarButtonScale = 1.0f;

    [Header("Desain PNG Tombol Yard Editor (Drag & Drop PNG ke sini)")]
    [Tooltip("Otomatis sembunyikan teks bawaan tombol saat tombol tersebut sudah diberi desain PNG")]
    public bool hideButtonTextWhenPngAssigned = true;
    [Tooltip("Pertahankan rasio aspek asli gambar PNG agar tombol tidak gepeng")]
    public bool preserveButtonPngAspect = true;
    [HideInInspector]
    public Vector2 toggleButtonSize = new Vector2(180f, 60f);
    [Tooltip("PNG untuk tombol utama pembuka 'Yard Editor' di pojok kanan atas (efek hover & select dibuat otomatis secara procedur)")]
    public Texture2D pngToggleYardEditor;
    [Tooltip("PNG opsional untuk background panel sidebar Yard Editor (default bgg.png)")]
    public Texture2D pngSidebarBackground;

    [Header("Pita Judul Section (Ribbon Headers)")]
    public Texture2D pngRibbonYard;
    public Texture2D pngRibbonRoads;
    public Texture2D pngRibbonWalls;
    public Texture2D pngRibbonAccessories;
    public Texture2D pngRibbonTools;
    public Texture2D pngRibbonFile;

    [Header("Mode Yard Blocks (Dinamis / Procedural)")]
    [Tooltip("Gunakan tombol dinamis yang rapi & adaptif (bisa muat puluhan yard dari API) alih-alih PNG statis")]
    public bool useProceduralYardButtons = true;

    [Header("PNG Tombol Yard Blocks (Jika Mode Dinamis Dimatikan)")]
    public Texture2D pngYardB;
    public Texture2D pngYardA;
    public Texture2D pngYardC;
    public Texture2D pngYardD;
    [Tooltip("PNG fallback untuk blok yard lainnya jika ada tambahan dari API")]
    public Texture2D pngYardDefault;

    [Header("PNG Tombol Roads")]
    public Texture2D pngAddRoad;
    public Texture2D pngRoadWidthMinus;
    public Texture2D pngRoadWidthPlus;
    public Texture2D pngRoadLengthMinus;
    public Texture2D pngRoadLengthPlus;

    [Header("PNG Tombol Pagar")]
    public Texture2D pngAddPagar;
    public Texture2D pngPagarLengthMinus;
    public Texture2D pngPagarLengthPlus;

    [Header("PNG Tombol Aksesoris")]
    public Texture2D pngAddGedung;
    public Texture2D pngAddGate;
    public Texture2D pngAddReachStacker;
    public Texture2D pngAddTopLoader;
    public Texture2D pngScaleMinus;
    public Texture2D pngScalePlus;

    [Header("PNG Tombol Tools & File")]
    public Texture2D pngRotate;
    public Texture2D pngDeleteSelected;
    public Texture2D pngSaveLayout;
    public Texture2D pngLoadLayout;
    public Texture2D pngClearAll;

    [Header("UI Info Kontainer (Popup Card)")]
    [Tooltip("Gunakan tata letak modern bergaya terminal logistik profesional (plat nomor kontainer, chip spesifikasi, dan grid lokasi)")]
    public bool useModernCardLayout = true;

    [Tooltip("Tema teks: Auto (otomatis teks gelap untuk kartu kuning/terang), DarkText (paksa teks gelap), LightText (paksa teks terang)")]
    public CardTextTheme cardTextTheme = CardTextTheme.Auto;

    [Tooltip("PNG Background custom untuk kartu Info Kontainer (Drag & Drop gambar PNG Anda ke sini).")]
    public Texture2D pngInfoKontainerBg;
    [Tooltip("PNG tombol close (X) custom untuk menutup kartu Info Kontainer.")]
    public Texture2D pngInfoKontainerClose;
    [Tooltip("Pilihan posisi kartu di layar. Default: UnderConnectPhone (di bawah tombol Connect Phone, rapi & tidak nabrak).")]
    public InfoKontainerPosition infoPosition = InfoKontainerPosition.UnderConnectPhone;
    [Tooltip("Posisi manual (X, Y) kartu jika Anda memilih posisi 'CustomPosition'.")]
    public Vector2 customInfoPos = new Vector2(20, 90);
    [Tooltip("Ukuran dasar kartu Info Kontainer (lebar x tinggi).")]
    public Vector2 infoCardSize = new Vector2(270, 240);
    [Range(0.5f, 2f)]
    public float infoCardScale = 1.0f;
    public Vector2 cardOffset = Vector2.zero;
    public bool autoSizeFromPng = false;
    public bool showHeaderTitle = true;
    public string headerTitle = "Info Kontainer";
    public Color infoTextColor = Color.white;
    public int infoFontSize = 13;
    public Vector2 contentPadding = new Vector2(16, 12);
    public Vector2 closeButtonSize = new Vector2(24, 24);
    public float closeButtonMarginRight = 18f;
    public float closeButtonMarginTop = 16f;
    public Vector2 closeButtonOffset = Vector2.zero;

    public Rect GetInfoCardRect()
    {
        Texture2D bg = pngInfoKontainerBg;
        if (bg == null && ContainerManager.Instance != null && ContainerManager.Instance.pngInfoKontainerBg != null)
            bg = ContainerManager.Instance.pngInfoKontainerBg;

        float scale = infoCardScale;
        Vector2 size = infoCardSize;
        Vector2 offset = cardOffset;
        InfoKontainerPosition pos = infoPosition;
        Vector2 customPos = customInfoPos;

        if (ContainerManager.Instance != null && pngInfoKontainerBg == null && ContainerManager.Instance.pngInfoKontainerBg != null)
        {
            scale = ContainerManager.Instance.infoCardScale;
            size = ContainerManager.Instance.infoCardSize;
            offset = ContainerManager.Instance.cardOffset;
            pos = ContainerManager.Instance.infoPosition;
            customPos = ContainerManager.Instance.customInfoPos;
        }

        float w = size.x * scale;
        float h = size.y * scale;

        if (autoSizeFromPng && bg != null)
        {
            w = bg.width * scale;
            h = bg.height * scale;
        }

        float x = 20f + offset.x;
        float y = 90f + offset.y;

        switch (pos)
        {
            case InfoKontainerPosition.UnderConnectPhone:
                x = 20f + offset.x;
                y = 90f + offset.y;
                break;
            case InfoKontainerPosition.BottomLeft:
                x = 20f + offset.x;
                y = Screen.height - h - 20f + offset.y;
                break;
            case InfoKontainerPosition.BottomRight:
                x = Screen.width - w - 20f + offset.x;
                y = Screen.height - h - 20f + offset.y;
                break;
            case InfoKontainerPosition.TopRight:
                x = Screen.width - w - 20f + offset.x;
                y = 90f + offset.y;
                break;
            case InfoKontainerPosition.CustomPosition:
                x = customPos.x + offset.x;
                y = customPos.y + offset.y;
                break;
        }

        return new Rect(x, y, w, h);
    }

    [Header("Door Text Settings (After Yard Editor)")]
    public float doorTextZOffset = -0.02f;
    public float doorTextXOffset = 0f;
    public float doorTextFontSize = 0.75f;

    // API data — DYNAMIC: bisa 1 yard, bisa 100 yard
    private List<MasterBlockData> masterBlocks = new List<MasterBlockData>();
    private List<ContainerLocData> containerLocList = new List<ContainerLocData>();
    private bool apiLoaded = false;
    private string pendingLayoutJson = null;

    // Active objects in scene
    private List<DraggableYard> activeYards = new List<DraggableYard>();
    private List<DraggableRoad> activeRoads = new List<DraggableRoad>();
    private List<DraggableAccessory> activeAccessories = new List<DraggableAccessory>();

    // Selection
    private DraggableYard selectedYard = null;
    private DraggableRoad selectedRoad = null;
    private DraggableAccessory selectedAccessory = null;

    // Container database for click info when outside Editor Mode
    private Dictionary<GameObject, ContainerLocData> spawnedContainerInfo = new Dictionary<GameObject, ContainerLocData>();
    private ContainerLocData clickedContainerData = null;

    // Drag & 2D Camera state
    private bool isDraggingAnything = false;
    private bool isPanning2DWithLeftMouse = false;
    public float groundY = 19.81f;
    private float editor2DOrthoSize = 85f;
    private Vector3 editor2DCamTargetPos;

    // ID counters
    private int nextRoadId = 1;
    private int nextAccessoryId = 1;

    // UI reference
    private YardEditorUI editorUI;

    // Generated material cache
    private Dictionary<string, Material> yardMaterials = new Dictionary<string, Material>();
    private readonly HashSet<Material> instancedMaterials = new HashSet<Material>();
    private static Material sharedWhiteBorderMat;

    private static readonly HashSet<string> AllowedRootNamesInCopy = new HashSet<string>
    {
        "vr-env",
        "VR-ENV",
        "Environment",
        "[YardEditor]",
        "YardEditor",
        "XRRig",
        "ViewModeManager",
        "ContainerManager",
        "ContainerSearchManager",
        "EventSystem",
        "Directional Light"
    };

    [SerializeField, HideInInspector] private bool initializedPngButtonDefaults = false;

    void ResetAutoAssignedSlotsOnce()
    {
        if (!initializedPngButtonDefaults)
        {
            preserveButtonPngAspect = true;
            globalButtonScale = 1.0f;
            toggleButtonScale = 0.5f;
            sidebarButtonScale = 1.0f;
            initializedPngButtonDefaults = true;
        }
        if (toggleButtonScale <= 0.01f) toggleButtonScale = 0.5f;
        if (globalButtonScale <= 0.01f) globalButtonScale = 1.0f;
        if (sidebarButtonScale <= 0.01f) sidebarButtonScale = 1.0f;

        AutoAssignPngsFromButtonCutFolder();
    }

    public void AutoAssignPngsFromButtonCutFolder()
    {
        string dir = "Assets/button/Button Cut/Button Cut/";
        string ribbonDir = "Assets/button/ribbons/";
#if UNITY_EDITOR
        // Pastikan seluruh PNG tombol tidak di-resize ke Power of Two (NPOTScale.None) agar rasio 100% presisi alami
        try
        {
            if (System.IO.Directory.Exists(dir))
            {
                string[] pngFiles = System.IO.Directory.GetFiles(dir, "*.png");
                foreach (string file in pngFiles)
                {
                    string unityPath = file.Replace('\\', '/');
                    UnityEditor.TextureImporter imp = UnityEditor.AssetImporter.GetAtPath(unityPath) as UnityEditor.TextureImporter;
                    if (imp != null)
                    {
                        bool dirty = false;
                        if (imp.npotScale != UnityEditor.TextureImporterNPOTScale.None)
                        {
                            imp.npotScale = UnityEditor.TextureImporterNPOTScale.None;
                            dirty = true;
                        }
                        if (!imp.alphaIsTransparency)
                        {
                            imp.alphaIsTransparency = true;
                            dirty = true;
                        }
                        if (dirty) imp.SaveAndReimport();
                    }
                }
            }
        }
        catch { }

        if (pngToggleYardEditor == null) pngToggleYardEditor = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "11.png");
        
        // Utamakan bgg.png (background kuning bersih tanpa pita statis)
        if (pngSidebarBackground == null || (pngSidebarBackground != null && pngSidebarBackground.name == "37"))
        {
            Texture2D bggTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "bgg.png");
            if (bggTex != null) pngSidebarBackground = bggTex;
        }

        // Pita Judul Ribbon Section
        System.Func<Texture2D, bool> shouldReassign = (tex) =>
        {
            if (tex == null) return true;
            string p = UnityEditor.AssetDatabase.GetAssetPath(tex);
            return string.IsNullOrEmpty(p) || p.Contains("/yE/");
        };

        if (pngRibbonYard == null) pngRibbonYard = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(ribbonDir + "ribbon_yard_blocks.png");
        if (pngRibbonRoads == null) pngRibbonRoads = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(ribbonDir + "ribbon_roads.png");
        if (pngRibbonWalls == null) pngRibbonWalls = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(ribbonDir + "ribbon_walls.png");
        if (pngRibbonAccessories == null) pngRibbonAccessories = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(ribbonDir + "ribbon_accessories.png");
        if (pngRibbonTools == null) pngRibbonTools = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(ribbonDir + "ribbon_tools.png");
        if (pngRibbonFile == null) pngRibbonFile = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(ribbonDir + "ribbon_file.png");

        if (shouldReassign(pngYardA)) pngYardA = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "12.png");
        if (shouldReassign(pngYardB)) pngYardB = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "13.png");
        if (shouldReassign(pngYardC)) pngYardC = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "14.png");
        if (shouldReassign(pngYardD)) pngYardD = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "15.png");

        if (shouldReassign(pngAddRoad)) pngAddRoad = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "16.png");
        if (shouldReassign(pngRoadWidthMinus)) pngRoadWidthMinus = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "17.png");
        if (shouldReassign(pngRoadWidthPlus)) pngRoadWidthPlus = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "18.png");
        if (shouldReassign(pngRoadLengthMinus)) pngRoadLengthMinus = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "19.png");
        if (shouldReassign(pngRoadLengthPlus)) pngRoadLengthPlus = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "20.png");

        if (shouldReassign(pngAddPagar)) pngAddPagar = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "21.png");
        if (shouldReassign(pngPagarLengthMinus)) pngPagarLengthMinus = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "22.png");
        if (shouldReassign(pngPagarLengthPlus)) pngPagarLengthPlus = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "23.png");

        if (shouldReassign(pngAddGedung)) pngAddGedung = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "24.png");
        if (shouldReassign(pngAddGate)) pngAddGate = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "25.png");
        if (shouldReassign(pngAddReachStacker)) pngAddReachStacker = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "26.png");
        if (shouldReassign(pngAddTopLoader)) pngAddTopLoader = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "27.png");
        if (shouldReassign(pngScaleMinus)) pngScaleMinus = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "29.png");
        if (shouldReassign(pngScalePlus)) pngScalePlus = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "30.png");

        if (shouldReassign(pngRotate)) pngRotate = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "31.png");
        if (shouldReassign(pngDeleteSelected)) pngDeleteSelected = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "32.png");
        if (shouldReassign(pngSaveLayout)) pngSaveLayout = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "33.png");
        if (shouldReassign(pngLoadLayout)) pngLoadLayout = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "34.png");
        if (shouldReassign(pngClearAll)) pngClearAll = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "35.png");
#endif
    }

    void OnValidate()
    {
        if (buildingPrefab == null && gedungPrefab != null) buildingPrefab = gedungPrefab;
        if (gedungPrefab == null && buildingPrefab != null) gedungPrefab = buildingPrefab;
        if (wallPrefab == null && pagarPrefab != null) wallPrefab = pagarPrefab;
        if (pagarPrefab == null && wallPrefab != null) pagarPrefab = wallPrefab;

        AutoAssignPngsFromButtonCutFolder();
    }

    void Awake()
    {
        if (gameObject.scene.name == "SampleScene") return;

        Instance = this;
        ResetAutoAssignedSlotsOnce();
        if (groundY < 19.5f || groundY > 19.95f) groundY = 19.81f;

        if (!Application.isPlaying) return;
    }

    void OnEnable()
    {
        if (gameObject.scene.name == "SampleScene") return;

        Instance = this;
        ResetAutoAssignedSlotsOnce();
        if (groundY < 19.5f || groundY > 19.95f) groundY = 19.81f;

        if (!Application.isPlaying) return;
    }

    void Start()
    {
        if (gameObject.scene.name == "SampleScene") return;
        if (!Application.isPlaying) return;

        CleanupOrphanBingkaiKuning();
        FindGroundY();

        // Bersihkan seluruh objek yard/kontainer/pagar/gedung/penomoran bawaan scene di awal agar hanya tersisa VR-env
        PurgeAllYardObjects();

        EnsureOrbitCameraInstalled();
        StartCoroutine(FetchAllApiData());

        // Auto-setup UI
        editorUI = GetComponent<YardEditorUI>();
        if (editorUI == null) editorUI = gameObject.AddComponent<YardEditorUI>();

        // Pastikan ContainerSearchManager selalu aktif di scene Copy
        ContainerSearchManager csm = FindFirstObjectByType<ContainerSearchManager>(FindObjectsInactive.Include);
        if (csm != null)
        {
            if (!csm.gameObject.activeSelf) csm.gameObject.SetActive(true);
            if (!csm.enabled) csm.enabled = true;
        }

        // Posisikan kamera ke vr-env
        Vector3 envCenter = GetEnvironmentCenter();
        editor2DCamTargetPos = new Vector3(envCenter.x, groundY + 180f, envCenter.z);

        Camera cam = GetActiveCamera();
        if (cam != null && (cam.transform.position == Vector3.zero || Vector3.Distance(cam.transform.position, envCenter) > 600f))
        {
            FocusCameraOn(envCenter);
        }

        // Jika isEditorActive sudah dicentang dari awal di Inspector, otomatis masuk ke 2D View
        if (isEditorActive)
        {
            ApplyEditorModeState(true);
        }
    }

    /// <summary>
    /// Membersihkan scene Copy pada Edit Mode agar hanya menyisakan vr-env (dan objek manager sistem).
    /// Menghapus YardA/B/C, gedung, pagar (Cube / ContainerPagarBesi), markaJalan, serta kontainer.
    /// Tidak pernah menyentuh SampleScene.
    /// </summary>
    public void CleanCopySceneInEditMode()
    {
        if (gameObject.scene.name != "Copy" || !gameObject.scene.isLoaded) return;

        bool changed = false;

        // 1. Matikan ContainerManager & ContainerSearchManager di scene Copy agar tidak auto-spawn di Edit Mode
        GameObject[] roots = gameObject.scene.GetRootGameObjects();
        foreach (GameObject rootObj in roots)
        {
            if (rootObj == null) continue;
            ContainerManager cm = rootObj.GetComponent<ContainerManager>();
            if (cm != null && cm.enabled)
            {
                cm.enabled = false;
                changed = true;
            }
        }

        // 2. Hapus Canvas runtime di bawah [YardEditor] jika ada sisa di Edit Mode
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name.Contains("Canvas"))
            {
                DestroyImmediate(child.gameObject);
                changed = true;
            }
        }

#if UNITY_EDITOR
        if (changed && !Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    public void CleanupOrphanBingkaiKuning()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isLoaded)
        {
            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject rootObj in rootObjects)
            {
                if (rootObj != null && rootObj.name == "BingkaiKuning")
                {
                    Destroy(rootObj);
                }
            }
        }
    }

    IEnumerator DelayedLoad()
    {
        while (!apiLoaded) yield return null;
        LoadLayout();
    }

    public Vector3 GetEnvironmentCenter()
    {
        GameObject vrEnv = GameObject.Find("vr-env");
        if (vrEnv == null) vrEnv = GameObject.Find("VR-ENV");
        if (vrEnv == null) vrEnv = GameObject.Find("Environment");

        Vector3 pos = new Vector3(111f, 19f, -455f);
        if (vrEnv != null)
        {
            pos = vrEnv.transform.position;
            Renderer[] rends = vrEnv.GetComponentsInChildren<Renderer>();
            if (rends != null && rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                bool hasValid = false;
                for (int i = 0; i < rends.Length; i++)
                {
                    if (rends[i] != null)
                    {
                        string rn = rends[i].gameObject.name.ToLower();
                        if (!rn.Contains("plane") && !rn.Contains("laut") && !rn.Contains("sea") && !rn.Contains("water"))
                        {
                            if (!hasValid) { b = rends[i].bounds; hasValid = true; }
                            else { b.Encapsulate(rends[i].bounds); }
                        }
                    }
                }
                if (hasValid && b.size.x > 10f && b.size.z > 10f)
                {
                    pos = new Vector3(b.center.x, b.min.y, b.center.z);
                }
            }
        }

        RaycastHit hit;
        if (Physics.Raycast(new Vector3(pos.x, pos.y + 1000f, pos.z), Vector3.down, out hit, 3000f))
        {
            pos.y = hit.point.y;
        }

        return pos;
    }

    void FindGroundY()
    {
        Vector3 envPos = GetEnvironmentCenter();
        groundY = 19.81f; // Presisi lantai beton aspal pelataran terminal 1Eagle (vr-env)
        Debug.Log("[YardEditor] Ground Y diatur ke: " + groundY + " (Posisi vr-env: " + envPos + ")");
    }

    // --- API Fetch & Data Folder Initialization ---

    IEnumerator LoadDataFolderCoroutine()
    {
        Debug.Log("[YardEditor] [App Start] Membaca konfigurasi dari folder data/ (.env dan denah.json)...");
        yield return StartCoroutine(LoadEnvFileCoroutine());
        yield return StartCoroutine(LoadDenahJsonCoroutine());
    }

    IEnumerator LoadEnvFileCoroutine()
    {
        // 1. Coba baca file fisik jika berjalan di Unity Editor atau Desktop build
        bool loaded = false;
        string[] filePaths = new string[]
        {
            System.IO.Path.Combine(Application.dataPath, "..", "data", ".env"),
            System.IO.Path.Combine(Application.dataPath, "..", "data", "app.properties"),
            System.IO.Path.Combine(Application.streamingAssetsPath, "data", ".env"),
            System.IO.Path.Combine(Application.streamingAssetsPath, "data", "app.properties")
        };

        foreach (string path in filePaths)
        {
            try
            {
                if (System.IO.File.Exists(path))
                {
                    string text = System.IO.File.ReadAllText(path);
                    if (!string.IsNullOrEmpty(text) && !text.Trim().StartsWith("<"))
                    {
                        ParseEnvContent(text, path);
                        loaded = true;
                        break;
                    }
                }
            }
            catch {}
        }

        if (loaded) yield break;

        // 2. Di WebGL / Server, ambil melalui WebRequest
        List<string> candidateUrls = new List<string>
        {
            "data/.env",
            "data/app.properties"
        };

        if (!string.IsNullOrEmpty(Application.streamingAssetsPath))
        {
            candidateUrls.Add(Application.streamingAssetsPath.TrimEnd('/') + "/data/.env");
            candidateUrls.Add(Application.streamingAssetsPath.TrimEnd('/') + "/data/app.properties");
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        if (!string.IsNullOrEmpty(Application.absoluteURL))
        {
            try
            {
                string abs = Application.absoluteURL;
                int lastSlash = abs.LastIndexOf('/');
                if (lastSlash >= 0)
                {
                    string baseDir = abs.Substring(0, lastSlash + 1);
                    candidateUrls.Add(baseDir + "data/.env");
                    candidateUrls.Add(baseDir + "data/app.properties");
                }
            }
            catch {}
        }
#endif

        foreach (string url in candidateUrls)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 4;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(req.downloadHandler.text))
                {
                    string text = req.downloadHandler.text.Trim();
                    // Pencegahan SPA 404: Jika web server merespon dengan fallback HTML, abaikan
                    if (!text.StartsWith("<") && !text.StartsWith("<!DOCTYPE", System.StringComparison.OrdinalIgnoreCase))
                    {
                        ParseEnvContent(text, url);
                        break;
                    }
                }
            }
        }
    }

    void ParseEnvContent(string content, string source)
    {
        if (string.IsNullOrEmpty(content)) return;

        string[] lines = content.Split(new char[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";")) continue;

            int eqIndex = line.IndexOf('=');
            if (eqIndex <= 0) continue;

            string key = line.Substring(0, eqIndex).Trim().ToUpperInvariant();
            string val = line.Substring(eqIndex + 1).Trim();

            // Hapus tanda kutip penutup jika ada
            if ((val.StartsWith("\"") && val.EndsWith("\"")) || (val.StartsWith("'") && val.EndsWith("'")))
            {
                if (val.Length >= 2) val = val.Substring(1, val.Length - 2).Trim();
            }

            if (key == "API_MASTER_BLOCK" || key == "MASTER_BLOCK_URL" || key == "MASTERBLOCK_URL")
            {
                if (!string.IsNullOrEmpty(val))
                {
                    masterBlockApiUrl = val;
                    Debug.Log($"[YardEditor] API_MASTER_BLOCK disetel dari {source}: {masterBlockApiUrl}");
                }
            }
            else if (key == "API_CONTAINER_LOC" || key == "CONTAINER_LOC_URL" || key == "API_CTR_LOC" || key == "CTR_LOC_URL")
            {
                if (!string.IsNullOrEmpty(val))
                {
                    containerLocApiUrl = val;
                    Debug.Log($"[YardEditor] API_CONTAINER_LOC disetel dari {source}: {containerLocApiUrl}");
                    if (ContainerManager.Instance != null)
                    {
                        ContainerManager.Instance.ctrLocApiUrl = containerLocApiUrl;
                    }
                }
            }
        }
    }

    IEnumerator LoadDenahJsonCoroutine()
    {
        // 1. Coba baca file fisik jika berjalan di Unity Editor atau Desktop build
        bool loaded = false;
        string[] filePaths = new string[]
        {
            System.IO.Path.Combine(Application.dataPath, "..", "data", "denah.json"),
            System.IO.Path.Combine(Application.streamingAssetsPath, "data", "denah.json")
        };

        foreach (string path in filePaths)
        {
            try
            {
                if (System.IO.File.Exists(path))
                {
                    string text = System.IO.File.ReadAllText(path);
                    if (ParseDenahJson(text, path))
                    {
                        loaded = true;
                        break;
                    }
                }
            }
            catch {}
        }

        if (loaded) yield break;

        // 2. Di WebGL / Server, ambil melalui WebRequest
        List<string> candidateUrls = new List<string>
        {
            "data/denah.json"
        };

        if (!string.IsNullOrEmpty(Application.streamingAssetsPath))
        {
            candidateUrls.Add(Application.streamingAssetsPath.TrimEnd('/') + "/data/denah.json");
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        if (!string.IsNullOrEmpty(Application.absoluteURL))
        {
            try
            {
                string abs = Application.absoluteURL;
                int lastSlash = abs.LastIndexOf('/');
                if (lastSlash >= 0)
                {
                    string baseDir = abs.Substring(0, lastSlash + 1);
                    candidateUrls.Add(baseDir + "data/denah.json");
                }
            }
            catch {}
        }
#endif

        foreach (string url in candidateUrls)
        {
            string fetchUrl = url + (url.Contains("?") ? "&" : "?") + "t=" + System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            using (UnityWebRequest req = UnityWebRequest.Get(fetchUrl))
            {
                req.timeout = 5;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(req.downloadHandler.text))
                {
                    string text = req.downloadHandler.text.Trim();
                    if (!text.StartsWith("<") && !text.StartsWith("<!DOCTYPE", System.StringComparison.OrdinalIgnoreCase))
                    {
                        string baseline = YardEditorDataUtil.GetServerDenahBaseline();
                        bool serverFileChanged = string.IsNullOrEmpty(baseline) || baseline.Trim() != text;

                        if (serverFileChanged)
                        {
                            // File fisik data/denah.json di folder server baru atau diubah manual -> gunakan isi file dari folder data/
                            YardEditorDataUtil.SetServerDenahBaseline(text);
                            YardEditorDataUtil.SaveLayoutJsonDirectly(text);
                            if (ParseDenahJson(text, url))
                            {
                                break;
                            }
                        }
                        else if (YardEditorDataUtil.HasSavedLayout())
                        {
                            // Gunakan hasil autosave terbaru dari tombol Save Layout di Yard Editor
                            string autosavedJson = YardEditorDataUtil.GetSavedLayoutJson();
                            if (!string.IsNullOrEmpty(autosavedJson) && ParseDenahJson(autosavedJson, "data/denah.json (Autosaved)"))
                            {
                                break;
                            }
                        }

                        if (ParseDenahJson(text, url))
                        {
                            break;
                        }
                    }
                }
            }
        }
    }

    bool ParseDenahJson(string json, string source)
    {
        if (string.IsNullOrEmpty(json)) return false;
        json = json.Trim();
        if (json.StartsWith("<")) return false;

        try
        {
            YardLayout layout = JsonUtility.FromJson<YardLayout>(json);
            if (layout != null && ((layout.yards != null && layout.yards.Count > 0) ||
                                  (layout.accessories != null && layout.accessories.Count > 0) ||
                                  (layout.roads != null && layout.roads.Count > 0)))
            {
                loadedFolderDenah = layout;
                YardEditorDataUtil.loadedDenahFromFile = layout;
                YardEditorDataUtil.loadedDenahJsonFromFile = json;
                Debug.Log($"[YardEditor] Berhasil memuat denah.json dari {source} (Yards: {layout.yards?.Count ?? 0}, Roads: {layout.roads?.Count ?? 0}, Aksesoris: {layout.accessories?.Count ?? 0})");
                return true;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[YardEditor] Gagal parse denah.json dari {source}: {ex.Message}");
        }
        return false;
    }

    IEnumerator FetchAllApiData()
    {
        // 1. Baca folder data/ (.env dan denah.json) sebelum request API dipanggil
        yield return StartCoroutine(LoadDataFolderCoroutine());

        // 2. Request data API ke endpoint yang telah ditentukan di .env
        yield return StartCoroutine(FetchMasterBlocksCoroutine());
        yield return StartCoroutine(FetchContainerLocationsCoroutine());

        // Jika masterBlocks kosong karena API timeout, ekstrak blok dari containerLocList atau buat default A, B, C, D
        if (masterBlocks.Count == 0)
        {
            BuildFallbackMasterBlocks();
        }

        apiLoaded = true;

        if (editorUI != null) editorUI.RebuildYardButtons();

        // 3. Terapkan Layout pada App Open
        // Prioritas 1: Jika ada layout yang disinkronkan dari Host WebRTC
        if (!string.IsNullOrEmpty(pendingLayoutJson))
        {
            Debug.Log("[YardEditor] Menerapkan layout yang disinkronkan dari Host setelah API siap...");
            ApplyPendingLayout();
        }
        // Prioritas 2: Terapkan denah.json dari folder data/ (sesuai diagram alur sistem: Folder data: denah.json -> App start -> App Open)
        else if (loadedFolderDenah != null && (
            (loadedFolderDenah.yards != null && loadedFolderDenah.yards.Count > 0) ||
            (loadedFolderDenah.roads != null && loadedFolderDenah.roads.Count > 0) ||
            (loadedFolderDenah.accessories != null && loadedFolderDenah.accessories.Count > 0)))
        {
            Debug.Log("[YardEditor] [App Open] Menerapkan layout dari folder data/denah.json...");
            ApplyLayout(loadedFolderDenah);
        }
        // Prioritas 3: Otomatis muat layout yang sudah disimpan jika ada
        else if (YardEditorDataUtil.HasSavedLayout())
        {
            Debug.Log("[YardEditor] Layout tersimpan terdeteksi. Memuat layout otomatis...");
            LoadLayout();
        }
        else if (gameObject.scene.name == "pre1eagle")
        {
            Debug.Log("[YardEditor] Scene pre1eagle terdeteksi tanpa custom layout. Memuat layout default 1Eagle (Gambar 1: Blok A, B, C)...");
            LoadDefault1EagleLayout();
        }
        else if (autoLoadOnStart)
        {
            LoadLayout();
        }
        else if (!isEditorActive && activeYards.Count > 0)
        {
            RefreshAllYardContainersVisibility();
        }

        Debug.Log("[YardEditor] API loaded: " + masterBlocks.Count + " yards, " + containerLocList.Count + " container locations");
    }

    public static List<string> BuildCandidateApiUrls(string configuredUrl, string defaultRelativePath)
    {
        List<string> urls = new List<string>();
        string cleanUrl = string.IsNullOrEmpty(configuredUrl) ? "" : configuredUrl.Trim();

#if !UNITY_WEBGL || UNITY_EDITOR
        // Di Unity Editor / Desktop Standalone: resolve relative path ke backend API
        if (cleanUrl.StartsWith("/"))
        {
            urls.Add("https://apps.globalhalalhub.co.id/global-halal-api" + cleanUrl);
        }
        else if (!string.IsNullOrEmpty(cleanUrl))
        {
            urls.Add(cleanUrl);
        }
        else
        {
            urls.Add("https://apps.globalhalalhub.co.id/global-halal-api" + defaultRelativePath);
        }
#else
        // Di WebGL:
        // 1. Jika URL berupa relative path (/api/...), ini pilihan terbaik karena bebas CORS di Netlify/Vercel
        if (cleanUrl.StartsWith("/"))
        {
            urls.Add(cleanUrl);
            urls.Add("https://corsproxy.io/?" + UnityWebRequest.EscapeURL("https://apps.globalhalalhub.co.id/global-halal-api" + cleanUrl));
        }
        else if (!string.IsNullOrEmpty(cleanUrl))
        {
            // Primary: URL dari .env
            urls.Add(cleanUrl);

            // Proxy internal di hosting (Netlify _redirects / Vercel rewrites)
            if (cleanUrl.Contains("apps.globalhalalhub.co.id"))
            {
                urls.Add(defaultRelativePath);
            }

            // Fallback CORS Proxy publik jika di-host di server lokal tanpa reverse proxy
            urls.Add("https://corsproxy.io/?" + UnityWebRequest.EscapeURL(cleanUrl));
            urls.Add("https://api.allorigins.win/raw?url=" + UnityWebRequest.EscapeURL(cleanUrl));
        }
        else
        {
            urls.Add(defaultRelativePath);
            urls.Add("https://apps.globalhalalhub.co.id/global-halal-api" + defaultRelativePath);
        }
#endif
        return urls;
    }

    IEnumerator FetchMasterBlocksCoroutine()
    {
        List<string> candidateUrls = BuildCandidateApiUrls(masterBlockApiUrl, "/api/getMasterBlock");
        bool success = false;

        foreach (string url in candidateUrls)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                if (url.Contains("pinggy"))
                {
                    req.SetRequestHeader("X-Pinggy-No-Screen", "1");
                }
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(req.downloadHandler.text))
                {
                    string json = req.downloadHandler.text.Trim();
                    if (json.StartsWith("[") && json.EndsWith("]"))
                    {
                        ParseMasterBlocks(json);
                        success = true;
                        Debug.Log($"[YardEditor] Berhasil memuat master block dari {url}");
                        break;
                    }
                }
                else
                {
                    Debug.LogWarning($"[YardEditor] Percobaan fetch getMasterBlock ke {url} gagal: {req.error}. Mencoba kandidat berikutnya...");
                }
            }
        }

        if (!success)
        {
            Debug.LogWarning("[YardEditor] Seluruh endpoint getMasterBlock gagal atau terhalang CORS. Menggunakan fallback master blocks.");
            BuildFallbackMasterBlocks();
        }
    }

    IEnumerator FetchContainerLocationsCoroutine()
    {
        List<string> candidateUrls = BuildCandidateApiUrls(containerLocApiUrl, "/api/getCtrLoc");
        bool loaded = false;

        foreach (string url in candidateUrls)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 12;
                if (url.Contains("pinggy"))
                {
                    req.SetRequestHeader("X-Pinggy-No-Screen", "1");
                }
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(req.downloadHandler.text))
                {
                    try
                    {
                        string json = req.downloadHandler.text.Trim();
                        if (json.StartsWith("[") && json.EndsWith("]"))
                        {
                            string wrapped = "{\"data\":" + json + "}";
                            ContainerLocResponse resp = JsonUtility.FromJson<ContainerLocResponse>(wrapped);
                            if (resp != null && resp.data != null && resp.data.Length > 0)
                            {
                                containerLocList = new List<ContainerLocData>(resp.data);
                                loaded = true;
                                Debug.Log($"[YardEditor] Loaded {containerLocList.Count} container locations from {url}");
                                break;
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[YardEditor] getCtrLoc parse error dari {url}: {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[YardEditor] Percobaan fetch getCtrLoc ke {url} gagal: {req.error}. Mencoba kandidat berikutnya...");
                }
            }
        }

        // Fallback otomatis ke Resources/containers_fallback.json jika API timeout / offline
        if (!loaded)
        {
            TextAsset fallbackAsset = Resources.Load<TextAsset>("containers_fallback");
            if (fallbackAsset != null && !string.IsNullOrEmpty(fallbackAsset.text))
            {
                try
                {
                    string wrapped = "{\"data\":" + fallbackAsset.text.Trim() + "}";
                    ContainerLocResponse resp = JsonUtility.FromJson<ContainerLocResponse>(wrapped);
                    if (resp != null && resp.data != null)
                    {
                        containerLocList = new List<ContainerLocData>(resp.data);
                        Debug.Log("[YardEditor] Loaded " + containerLocList.Count + " containers from local fallback cache (containers_fallback.json)");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[YardEditor] Fallback parse error: " + ex.Message);
                }
            }
        }
    }

    void ParseMasterBlocks(string json)
    {
        try
        {
            string wrapped = "{\"data\":" + json + "}";
            MasterBlockResponse response = JsonUtility.FromJson<MasterBlockResponse>(wrapped);

            if (response != null && response.data != null)
            {
                masterBlocks.Clear();
                foreach (var block in response.data)
                {
                    masterBlocks.Add(block);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[YardEditor] ParseMasterBlocks error: " + ex.Message);
        }
    }

    void BuildFallbackMasterBlocks()
    {
        string[] defaultBlocks = new string[] { "A", "B", "C", "D" };
        foreach (string b in defaultBlocks)
        {
            MasterBlockData mb = new MasterBlockData
            {
                YARD_BLOCK = b,
                YARD_BLOCK_NAME = b,
                YARD_ROW = "6",
                YARD_SLOT = "45",
                YARD_TIER = "7"
            };
            masterBlocks.Add(mb);
        }
    }

    // --- Public API for UI & Mode Switching ---

    public List<MasterBlockData> GetMasterBlocks()
    {
        return masterBlocks;
    }

    public bool IsApiLoaded() { return apiLoaded; }
    public bool IsEditorActive() { return isEditorActive; }

    public ContainerLocData GetClickedContainerData() { return clickedContainerData; }
    public void ClearClickedContainerData() { clickedContainerData = null; }
    public void SelectContainerForInfo(GameObject go, ContainerLocData data) { clickedContainerData = data; }
    public Dictionary<GameObject, ContainerLocData> GetSpawnedContainerInfo() { return spawnedContainerInfo; }

    public void ToggleEditor()
    {
        SetEditorActive(!isEditorActive);
    }

    public void SetEditorActive(bool active)
    {
        isEditorActive = active;
        ApplyEditorModeState(active);
        Debug.Log("[YardEditor] Editor mode: " + (isEditorActive ? "ACTIVE (2D View Only, No Containers)" : "INACTIVE (3D Containers & Markings Active)"));
    }

    private void ApplyEditorModeState(bool active)
    {
        DeselectAll();
        clickedContainerData = null;
        isDraggingAnything = false;
        isPanning2DWithLeftMouse = false;

        // 1. Sembunyikan kontainer saat editing yard, dan munculkan kontainer (beserta nomor, ISO, TARE) setelah keluar dari yard editor
        RefreshAllYardContainersVisibility();

        // 2. Atur ViewModeManager: Saat editing yard, otomatis masuk ke 2D View & hanya ada 2D View
        ViewModeManager vmm = ViewModeManager.Instance ?? Object.FindFirstObjectByType<ViewModeManager>();
        if (active)
        {
            if (vmm != null)
            {
                // Pindahkan ke 2D Top-Down View secara otomatis
                vmm.SetViewMode(CameraViewMode.TopDown2D);

                // Sembunyikan seluruh tombol ViewMode saat Yard Editor dibuka
                if (vmm.button3DView != null) vmm.button3DView.gameObject.SetActive(false);
                if (vmm.button2DView != null) vmm.button2DView.gameObject.SetActive(false);
                if (vmm.buttonWalkView != null) vmm.buttonWalkView.gameObject.SetActive(false);

                // Matikan komponen ViewModeManager sementara agar tombol OnGUI tidak digambar
                // dan Left-drag di 2D View tidak menggeser kamera saat user sedang drag Yard / Road / Aksesoris
                vmm.enabled = false;
            }

            SetupEditor2DCamera();
        }
        else
        {
            if (vmm != null)
            {
                vmm.enabled = true;
                if (vmm.button3DView != null) vmm.button3DView.gameObject.SetActive(true);
                if (vmm.button2DView != null) vmm.button2DView.gameObject.SetActive(true);
                if (vmm.buttonWalkView != null) vmm.buttonWalkView.gameObject.SetActive(true);

                // Kembalikan ke 3D View setelah selesai edit agar kontainer & nomor pintu langsung terlihat
                vmm.SetViewMode(CameraViewMode.BirdView3D);
            }
        }
    }

    private void SetupEditor2DCamera()
    {
        Camera cam = GetActiveCamera();
        Vector3 focusCenter = GetYardsOrEnvCenter();

        editor2DCamTargetPos = new Vector3(focusCenter.x, groundY + 180f, focusCenter.z);

        if (cam != null)
        {
            cam.gameObject.SetActive(true);
            cam.orthographic = true;
            editor2DOrthoSize = Mathf.Clamp(editor2DOrthoSize, 30f, 350f);
            cam.orthographicSize = editor2DOrthoSize;
            cam.farClipPlane = 2000f;
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.transform.position = editor2DCamTargetPos;

            OrbitCamera orbit = cam.GetComponent<OrbitCamera>();
            if (orbit != null) orbit.enabled = false;
        }
    }

    private Vector3 GetYardsOrEnvCenter()
    {
        if (activeYards.Count > 0)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var y in activeYards)
            {
                if (y != null)
                {
                    sum += y.transform.position;
                    count++;
                }
            }
            if (count > 0) return sum / count;
        }
        return GetEnvironmentCenter();
    }

    /// <summary>
    /// Revisi 1 & 3:
    /// - Saat isEditorActive == true: Semua kontainer disembunyikan (tidak ada kontainer yang muncul saat editing yard).
    /// - Saat isEditorActive == false: Kontainer di-spawn / ditampilkan di atas setiap yard lengkap dengan Nomor Kontainer, ISO, dan TARE di pintu.
    /// </summary>
    public void RefreshAllYardContainersVisibility()
    {
        foreach (var yard in activeYards)
        {
            if (yard == null) continue;

            Transform containerGroup = yard.transform.Find("ContainersFromAPI");

            if (isEditorActive)
            {
                // Sembunyikan kontainer selama mode editing yard
                if (containerGroup != null)
                {
                    containerGroup.gameObject.SetActive(false);
                }
            }
            else
            {
                // Setelah keluar dari mode yard editor, pastikan kontainer sudah di-spawn dan ditampilkan
                if (containerGroup == null)
                {
                    Vector2 size = CalculateYardSizeFromName(yard.blockName);
                    Transform plat = yard.transform.Find("YardPlatform");
                    float slabHeight = 0.25f;
                    if (plat != null)
                    {
                        size = new Vector2(plat.localScale.x, plat.localScale.z);
                        slabHeight = plat.localScale.y;
                    }
                    GenerateContainerStacksFromApi(yard.transform, yard.blockName, size, slabHeight);
                    containerGroup = yard.transform.Find("ContainersFromAPI");
                }

                if (containerGroup != null)
                {
                    containerGroup.gameObject.SetActive(true);
                }
            }
        }
    }

    // --- Yard Dimension Calculation ---

    /// <summary>
    /// Hitung koordinat awal (startX, startZ) untuk grid kontainer.
    /// Memberikan offset ruang aspal (11.0m di depan untuk nomor row & logo, 7.0m di kiri untuk nomor slot).
    /// </summary>
    public Vector3 GetGridOriginOffset(int maxRow, int maxSlot, Vector2 yardSize)
    {
        if (maxRow > 6) maxRow = 6;
        if (maxRow < 1) maxRow = 6;
        if (maxSlot < 1) maxSlot = 45;

        float halfL = yardSize.y / 2.0f;

        // Pusatkan barisan kontainer (Row 1 s/d Row 6) tepat di tengah platform (X = 0):
        float startX = -((maxRow - 1) * rowSpacing * 0.5f);

        // Garis batas depan (min Z) kontainer Slot 1 (11.0m dari sisi selatan platform):
        float startZ = -halfL + 11.0f;

        return new Vector3(startX, 0, startZ);
    }

    /// <summary>
    /// Hitung ukuran yard (lebar x panjang) proporsional dan presisi dari data master block.
    /// </summary>
    public Vector2 CalculateYardSize(MasterBlockData block)
    {
        int maxRow = 6;
        int maxSlot = 45;
        if (block != null)
        {
            int.TryParse(block.YARD_ROW, out maxRow);
            int.TryParse(block.YARD_SLOT, out maxSlot);
        }

        if (maxRow > 6) maxRow = 6;
        if (maxRow < 1) maxRow = 6;
        if (maxSlot < 1) maxSlot = 45;

        int numSlotPositions = (maxSlot + 1) / 2;
        float mLeft = yardMarginLeft > 0.1f ? yardMarginLeft : 8.8f;
        float mRight = yardMarginRight > 0.1f ? yardMarginRight : 8.8f;
        float width = ((maxRow - 1) * rowSpacing) + 2.44f + mLeft + mRight;
        float length = 11.0f + ((numSlotPositions - 1) * slotSpacing) + 12.2f + 6.0f;

        return new Vector2(width, length);
    }

    public Vector2 CalculateYardSizeFromName(string blockName)
    {
        foreach (var mb in masterBlocks)
        {
            if (mb.YARD_BLOCK == blockName || mb.YARD_BLOCK_NAME == blockName)
            {
                return CalculateYardSize(mb);
            }
        }
        return CalculateYardSize(null);
    }

    public MasterBlockData GetMasterBlock(string blockName)
    {
        foreach (var mb in masterBlocks)
        {
            if (mb.YARD_BLOCK == blockName || mb.YARD_BLOCK_NAME == blockName)
                return mb;
        }
        return null;
    }

    // --- Accessory Prefab Resolver (100% ditentukan oleh User di Inspector) ---

    public GameObject GetAccessoryPrefab(string accessoryType)
    {
        string clean = (accessoryType ?? "").Replace(" ", "").Trim();
        switch (clean)
        {
            case "Gedung":
            case "Building":
                return buildingPrefab != null ? buildingPrefab : gedungPrefab;
            case "ReachStacker":
                return reachStackerPrefab;
            case "TopLoader":
                return topLoaderPrefab;
            case "Gate":
                return gatePrefab;
            case "Pagar":
            case "Wall":
                return wallPrefab != null ? wallPrefab : pagarPrefab;
            default:
                return null;
        }
    }

    public Texture2D GetYardButtonPng(string blockName)
    {
        // Jika mode procedural aktif, jangan gunakan PNG teks statis agar bisa muat puluhan yard dinamis dari API
        if (useProceduralYardButtons) return null;

        if (string.IsNullOrEmpty(blockName)) return pngYardDefault;
        string clean = blockName.Replace("Yard", "").Replace("YARD", "").Trim().ToUpper();
        switch (clean)
        {
            case "A": return pngYardA != null ? pngYardA : pngYardDefault;
            case "B": return pngYardB != null ? pngYardB : pngYardDefault;
            case "C": return pngYardC != null ? pngYardC : pngYardDefault;
            case "D": return pngYardD != null ? pngYardD : pngYardDefault;
            default: return pngYardDefault;
        }
    }

    // --- YARD PROCEDURAL GENERATION ---

    GameObject GenerateYardProcedural(string blockName, Vector2 size)
    {
        return GenerateYardPureProcedural(blockName, size);
    }

    GameObject GenerateYardPureProcedural(string blockName, Vector2 size)
    {
        // Root object
        GameObject yardGo = new GameObject("Yard" + blockName + "_Editor");

        // 1. Platform Base Slab ("Bawahnya Yard" - Pelat lantai beton tebal 0.25m agar kokoh, jelas, dan berdiri nyata di atas paving seperti pada Gedung)
        float slabThickness = 0.25f;
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "YardPlatform";
        platform.transform.SetParent(yardGo.transform);
        platform.transform.localPosition = new Vector3(0, slabThickness / 2f, 0);
        platform.transform.localRotation = Quaternion.identity;
        platform.transform.localScale = new Vector3(size.x, slabThickness, size.y);

        Material mat = GetOrCreateYardMaterial(blockName);
        Renderer rend = platform.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = mat;

        // 2. Marka Jalan (Yellow Industrial Safety Border Frame)
        AddYardMarkingBorder(yardGo.transform, size, slabThickness);

        // 3. Penomoran Slot (001, 003...), Row (R1..R6), dan Kotak Nama Blok (Ditengahkan)
        AddYardFloorNumberingAndLabels(yardGo.transform, blockName, size, slabThickness);

        // 4. Kontainer:
        // Revisi 1: Pada saat editing yard (isEditorActive == true), TIDAK ADA kontainer yang muncul!
        // Kontainer hanya di-generate/ditampilkan saat keluar dari mode Yard Editor (isEditorActive == false).
        if (!isEditorActive)
        {
            GenerateContainerStacksFromApi(yardGo.transform, blockName, size, slabThickness);
        }

        // 5. Tambahkan BoxCollider utama di root object agar raycast click 100% kena (isTrigger agar tidak memblokir pergerakan Walk View)
        BoxCollider rootCol = yardGo.AddComponent<BoxCollider>();
        rootCol.center = new Vector3(0, 1.5f, 0);
        rootCol.size = new Vector3(size.x, 4f, size.y);
        rootCol.isTrigger = true;

        return yardGo;
    }

    void AddYardFloorNumberingAndLabels(Transform parent, string blockName, Vector2 yardSize, float heightY)
    {
        MasterBlockData mb = GetMasterBlock(blockName);
        int maxRow = 6, maxSlot = 45;
        if (mb != null)
        {
            int.TryParse(mb.YARD_ROW, out maxRow);
            int.TryParse(mb.YARD_SLOT, out maxSlot);
        }
        if (maxRow > 6) maxRow = 6;
        if (maxRow < 1) maxRow = 6;
        if (maxSlot < 1) maxSlot = 45;

        GameObject numGroup = new GameObject("YardNumberingAndLabels");
        numGroup.transform.SetParent(parent);
        numGroup.transform.localPosition = Vector3.zero;
        numGroup.transform.localRotation = Quaternion.identity;

        float textY = heightY + 0.02f; // Melayang presisi 2 cm di atas permukaan aspal beton

        Vector3 gridOrigin = GetGridOriginOffset(maxRow, maxSlot, yardSize);
        float startX = gridOrigin.x;
        float startZ = gridOrigin.z;

        float actualRowSpacing = rowSpacing;
        float actualSlotSpacing = slotSpacing;

        // 1. SLOT NUMBERS (001, 003, 005, ... 045)
        float xPosSlotLine = startX - 4.2f;

        for (int s = 1; s <= maxSlot; s += 2)
        {
            float slotIndex = (s - 1) / 2.0f;
            float zPos = startZ + (slotIndex * actualSlotSpacing);
            // Posisikan teks tepat di titik tengah (center) bay kontainer 20ft (identik dengan ContainerManager:1819)
            float zCenter = zPos + (actualSlotSpacing * 0.5f);

            Vector3 localPos = new Vector3(xPosSlotLine, textY, zCenter);
            Quaternion rot = Quaternion.Euler(90, 90, 0);
            Create3DFloorText(numGroup.transform, localPos, rot, s.ToString("000"), 10f, Color.white);
        }

        // 2. ROW NUMBERS (R1, R2, R3, R4, R5, R6)
        // Ditempatkan di area driveway depan (3.5m di depan garis Slot 1)
        float zPosRowLine = startZ - 3.5f;

        for (int r = 1; r <= maxRow; r++)
        {
            float rowIndex = r - 1;
            float posX = startX + (rowIndex * actualRowSpacing);

            Vector3 localPos = new Vector3(posX, textY, zPosRowLine);
            Quaternion rot = Quaternion.Euler(90, 0, 0);
            Create3DFloorText(numGroup.transform, localPos, rot, "R" + r, 11f, Color.white);
        }

        // 3. KOTAK KUNING NAMA BLOK ([ A ], [ B ], [ C ], dst) - DITENGAHKAN (X = 0)
        string letter = blockName.Replace("Yard", "").Replace("YARD", "").Trim().ToUpper();
        if (string.IsNullOrEmpty(letter)) letter = blockName.ToUpper();

        float halfLength = yardSize.y / 2f;

        // Kotak kuning nama blok ([ A ], [ B ], [ C ], dst) - DEAD CENTER (X = 0)
        Vector3 labelBoxPosSouth = new Vector3(0f, textY, -halfLength + 3.2f);
        CreateYellowBlockIconBox(numGroup.transform, labelBoxPosSouth, letter);

        Vector3 labelBoxPosNorth = new Vector3(0f, textY, halfLength - 3.2f);
        CreateYellowBlockIconBox(numGroup.transform, labelBoxPosNorth, letter);
    }

    void Create3DFloorText(Transform parent, Vector3 localPos, Quaternion localRot, string textContent, float fontSize, Color color)
    {
        GameObject txtObj = new GameObject("Txt_" + textContent);
        txtObj.transform.SetParent(parent);
        txtObj.transform.localPosition = localPos;
        txtObj.transform.localRotation = localRot;

        TMPro.TextMeshPro tmp = txtObj.AddComponent<TMPro.TextMeshPro>();
        tmp.text = textContent;
        tmp.fontSize = fontSize;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = color;
        if (Application.isPlaying)
        {
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = Color.black;
        }
        tmp.rectTransform.sizeDelta = new Vector2(8f, 3f);
    }

    private Material cachedYellowBorderMat;
    private Material GetYellowBorderMaterial()
    {
        if (cachedYellowBorderMat == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Standard");

            cachedYellowBorderMat = new Material(s);
            Color brightYellow = new Color(1.0f, 0.88f, 0.0f, 1.0f);
            cachedYellowBorderMat.color = brightYellow;
            if (cachedYellowBorderMat.HasProperty("_BaseColor")) cachedYellowBorderMat.SetColor("_BaseColor", brightYellow);
            if (cachedYellowBorderMat.HasProperty("_Color")) cachedYellowBorderMat.SetColor("_Color", brightYellow);
            cachedYellowBorderMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 25;
        }
        return cachedYellowBorderMat;
    }

    void CreateYellowBlockIconBox(Transform parent, Vector3 localPos, string letter)
    {
        GameObject iconBoxObj = new GameObject("BlockIconBox_" + letter);
        iconBoxObj.transform.SetParent(parent);
        iconBoxObj.transform.localPosition = localPos;
        iconBoxObj.transform.localRotation = Quaternion.identity;

        float boxSize = 2.8f;
        float lineThick = 0.25f;
        float half = boxSize / 2.0f;

        Material yellowMat = GetYellowBorderMaterial();

        CreateBorderBar(iconBoxObj.transform, new Vector3(0, 0.01f, half), new Vector3(boxSize + lineThick, 0.02f, lineThick), yellowMat);
        CreateBorderBar(iconBoxObj.transform, new Vector3(0, 0.01f, -half), new Vector3(boxSize + lineThick, 0.02f, lineThick), yellowMat);
        CreateBorderBar(iconBoxObj.transform, new Vector3(-half, 0.01f, 0), new Vector3(lineThick, 0.02f, boxSize), yellowMat);
        CreateBorderBar(iconBoxObj.transform, new Vector3(half, 0.01f, 0), new Vector3(lineThick, 0.02f, boxSize), yellowMat);

        GameObject textObj = new GameObject("Letter_" + letter);
        textObj.transform.SetParent(iconBoxObj.transform);
        textObj.transform.localPosition = new Vector3(0, 0.02f, 0);
        textObj.transform.localRotation = Quaternion.Euler(90, 0, 0);

        TMPro.TextMeshPro tmp = textObj.AddComponent<TMPro.TextMeshPro>();
        tmp.text = letter;
        tmp.fontSize = 15f;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = new Color(1.0f, 0.88f, 0.0f, 1.0f);
        if (Application.isPlaying)
        {
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = Color.black;
        }
        tmp.rectTransform.sizeDelta = new Vector2(6f, 6f);
    }

    void AddYardMarkingBorder(Transform parent, Vector2 yardSize, float heightY)
    {
        GameObject borderGroup = new GameObject("MarkaJalanBorder");
        borderGroup.transform.SetParent(parent);
        borderGroup.transform.localPosition = Vector3.zero;

        Material yellowMat = GetYellowBorderMaterial();

        float borderThick = 0.6f;
        float borderHeight = 0.02f;
        float posY = heightY + (borderHeight / 2f) + 0.002f;
        float halfX = yardSize.x / 2f;
        float halfZ = yardSize.y / 2f;

        CreateBorderBar(borderGroup.transform, new Vector3(0, posY, halfZ - (borderThick / 2f)), new Vector3(yardSize.x, borderHeight, borderThick), yellowMat);
        CreateBorderBar(borderGroup.transform, new Vector3(0, posY, -halfZ + (borderThick / 2f)), new Vector3(yardSize.x, borderHeight, borderThick), yellowMat);
        CreateBorderBar(borderGroup.transform, new Vector3(-halfX + (borderThick / 2f), posY, 0), new Vector3(borderThick, borderHeight, yardSize.y - (borderThick * 2f)), yellowMat);
        CreateBorderBar(borderGroup.transform, new Vector3(halfX - (borderThick / 2f), posY, 0), new Vector3(borderThick, borderHeight, yardSize.y - (borderThick * 2f)), yellowMat);
    }

    void CreateBorderBar(Transform parent, Vector3 localPos, Vector3 scale, Material mat)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = "YellowBorderBar";
        bar.transform.SetParent(parent);
        bar.transform.localPosition = localPos;
        bar.transform.localScale = scale;

        Renderer r = bar.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;

        Collider c = bar.GetComponent<Collider>();
        if (c != null) DestroyImmediate(c);
    }

    // =========================================================================
    // CONTAINER SPAWNING & DOOR MARKINGS (CTR_NO, ISO_CODE, TARE)
    // =========================================================================

    void GenerateContainerStacksFromApi(Transform parent, string blockName, Vector2 yardSize, float startY)
    {
        Transform existingGroup = parent.Find("ContainersFromAPI");
        if (existingGroup != null)
        {
            if (Application.isPlaying) Destroy(existingGroup.gameObject);
            else DestroyImmediate(existingGroup.gameObject);
        }

        List<ContainerLocData> containersForBlock = new List<ContainerLocData>();
        string cleanBlock = blockName.Replace("Yard", "").Trim().ToUpper();

        foreach (var c in containerLocList)
        {
            if (c != null)
            {
                string targetBlock = (c.YARD_BLOCK ?? "").Trim().ToUpper();
                if (targetBlock == cleanBlock || targetBlock == blockName.ToUpper())
                {
                    containersForBlock.Add(c);
                }
            }
        }

        MasterBlockData mb = GetMasterBlock(blockName);
        int maxRow = 6, maxSlot = 45;
        if (mb != null)
        {
            int.TryParse(mb.YARD_ROW, out maxRow);
            int.TryParse(mb.YARD_SLOT, out maxSlot);
        }
        if (maxRow > 6) maxRow = 6;
        if (maxRow < 1) maxRow = 6;
        if (maxSlot < 1) maxSlot = 45;

        GameObject containerGroup = new GameObject("ContainersFromAPI");
        containerGroup.transform.SetParent(parent, false);
        containerGroup.transform.localPosition = Vector3.zero;
        containerGroup.transform.localRotation = Quaternion.identity;

        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Standard");

        if (containersForBlock.Count > 0)
        {
            Debug.Log("[YardEditor] Spawning " + containersForBlock.Count + " kontainer beserta nomor pintu/ISO/TARE untuk Yard " + blockName);

            Vector3 gridOrigin = GetGridOriginOffset(maxRow, maxSlot, yardSize);
            float startX = gridOrigin.x;
            float startZ = gridOrigin.z;

            foreach (var c in containersForBlock)
            {
                int r = 1, sl = 1, t = 1;
                int.TryParse(c.YARD_ROW, out r);
                int.TryParse(c.YARD_SLOT, out sl);
                int.TryParse(c.YARD_TIER, out t);

                if (r > 6 || r < 1) continue;

                SpawnSingleContainerInYard(containerGroup.transform, c, r, sl, t, startX, startY, startZ, s);
            }
        }
        else
        {
            GenerateContainerStacksFallback(containerGroup.transform, cleanBlock, maxRow, maxSlot, startY, s, yardSize);
        }

        // Pastikan visibilitas sesuai state editor
        containerGroup.SetActive(!isEditorActive);
    }

    void SpawnSingleContainerInYard(Transform containerGroup, ContainerLocData c, int r, int sl, int t, float startX, float startY, float startZ, Shader s)
    {
        float slotIndex = (sl - 1) / 2.0f;
        float rowIndex = r - 1;
        float tierIndex = t - 1;

        bool is40ft = (c.CTR_SIZE == "40");
        float containerLength = is40ft ? 12.19f : 6.06f;
        float containerWidth = 2.44f;
        float containerHeight = 2.59f;

        float posX = startX + (rowIndex * rowSpacing);
        float posZ = startZ + (slotIndex * slotSpacing);
        float targetBottomY = startY + (tierIndex * 2.6f);

        string ctrNo = string.IsNullOrEmpty(c.CTR_NO) ? "MSCU2398400" : c.CTR_NO.Trim().ToUpper();
        string sizeStr = is40ft ? "40" : "20";

        GameObject prefab = GetContainerPrefab(c.AGENT, c.CTR_MERK, is40ft);
        GameObject cBox;

        if (prefab != null)
        {
            // Instantiate di titik (0,0,0) dengan rotasi Identity terlebih dahulu agar kalkulasi Bounds & Door Markings 100% akurat
            cBox = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            cBox.name = ctrNo + " (" + sizeStr + "ft)";

            Vector3 standardSize = new Vector3(containerWidth, containerHeight, containerLength);
            MeshRenderer[] mrs = cBox.GetComponentsInChildren<MeshRenderer>();
            if (mrs.Length > 0)
            {
                MeshRenderer mainBody = mrs[0];
                float maxVol = 0;
                foreach (var mr in mrs)
                {
                    Vector3 sz = mr.bounds.size;
                    float vol = sz.x * sz.y * sz.z;
                    if (vol > maxVol) { maxVol = vol; mainBody = mr; }
                }

                Vector3 currentSize = mainBody.bounds.size;
                if (currentSize.x > 0.1f && currentSize.y > 0.1f && currentSize.z > 0.1f)
                {
                    float scaleX = standardSize.x / (currentSize.x > currentSize.z ? currentSize.z : currentSize.x);
                    float scaleY = standardSize.y / currentSize.y;
                    float scaleZ = standardSize.z / (currentSize.x > currentSize.z ? currentSize.x : currentSize.z);

                    if (currentSize.x > currentSize.z)
                    {
                        float temp = scaleX;
                        scaleX = scaleZ;
                        scaleZ = temp;
                    }

                    cBox.transform.localScale = new Vector3(
                        cBox.transform.localScale.x * scaleX,
                        cBox.transform.localScale.y * scaleY,
                        cBox.transform.localScale.z * scaleZ
                    );
                }
            }

            // Bersihkan kamera/light bawaan prefab
            foreach (Camera cam in cBox.GetComponentsInChildren<Camera>(true))
            {
                if (Application.isPlaying) Destroy(cam.gameObject);
                else DestroyImmediate(cam.gameObject);
            }
            foreach (Light light in cBox.GetComponentsInChildren<Light>(true))
            {
                if (Application.isPlaying) Destroy(light.gameObject);
                else DestroyImmediate(light.gameObject);
            }
        }
        else
        {
            cBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cBox.name = ctrNo + " (" + sizeStr + "ft)";
            cBox.transform.position = Vector3.zero;
            cBox.transform.rotation = Quaternion.identity;
            cBox.transform.localScale = new Vector3(containerWidth, containerHeight, containerLength);

            Color cColor = GetAgentColor(c.AGENT, c.CTR_MERK);
            Material cMat = new Material(s);
            cMat.color = cColor;
            if (cMat.HasProperty("_BaseColor")) cMat.SetColor("_BaseColor", cColor);
            if (cMat.HasProperty("_Color")) cMat.SetColor("_Color", cColor);

            Renderer cRend = cBox.GetComponent<Renderer>();
            if (cRend != null) cRend.sharedMaterial = cMat;
        }

        // Ukur Bounds kontainer saat masih di (0,0,0) & rotasi (0,0,0)
        Renderer[] renderers = cBox.GetComponentsInChildren<Renderer>();
        Bounds localBounds = new Bounds(Vector3.zero, new Vector3(containerWidth, containerHeight, containerLength));
        if (renderers.Length > 0)
        {
            localBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null) localBounds.Encapsulate(renderers[i].bounds);
            }
        }

        // Pasang BoxCollider solid pada root kontainer
        BoxCollider col = cBox.GetComponent<BoxCollider>();
        if (col == null) col = cBox.AddComponent<BoxCollider>();
        Vector3 lossy = cBox.transform.lossyScale;
        col.center = cBox.transform.InverseTransformPoint(localBounds.center);
        col.size = new Vector3(
            localBounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
            localBounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)),
            localBounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(lossy.z))
        );

        // Optimasi shadow & GPU instancing
        foreach (Renderer rComp in renderers)
        {
            if (rComp == null) continue;
            rComp.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rComp.receiveShadows = false;
            foreach (Material m in rComp.sharedMaterials)
            {
                if (m != null && !m.enableInstancing && instancedMaterials.Add(m))
                {
                    m.enableInstancing = true;
                }
            }
        }

        // Revisi 3: Tambahkan tulisan Nomor Kontainer, ISO Code, dan TARE di pintu belakang kontainer!
        AddContainerMarkingsOnDoor(cBox, localBounds, ctrNo, c.ISO_CODE, c.TARE, is40ft);

        // Revisi 4: Tambahkan tulisan Nomor Kontainer (vertikal), ISO Code, dan TARE di samping kanan & kiri kontainer!
        AddContainerSideMarkings(cBox, localBounds, ctrNo, c.ISO_CODE, c.TARE, is40ft);

        // Hitung offset pivot agar kontainer duduk presisi di atas lantai yard, center pada row (X),
        // dan sisi depan kontainer (min Z) presisi di batas slotnya (Z) identik dengan ContainerManager (penomoran statis)
        Vector3 pivotToCenter = localBounds.center - cBox.transform.position;
        float pivotToBottomY = localBounds.min.y - cBox.transform.position.y;
        float pivotToMinZ = localBounds.min.z - cBox.transform.position.z;

        // Parent ke containerGroup pada yard
        cBox.transform.SetParent(containerGroup, false);
        cBox.transform.localRotation = Quaternion.identity;
        cBox.transform.localPosition = new Vector3(
            posX - pivotToCenter.x,
            targetBottomY - pivotToBottomY,
            posZ - pivotToMinZ
        );

        // Refresh URP DecalProjector jika ada pada prefab kontainer
        foreach (Behaviour comp in cBox.GetComponentsInChildren<Behaviour>())
        {
            if (comp != null && comp.GetType().Name == "DecalProjector")
            {
                bool prevState = comp.enabled;
                comp.enabled = false;
                comp.enabled = true;
                comp.enabled = prevState;
            }
        }

        // Simpan ke dictionary untuk klik Info Kontainer
        spawnedContainerInfo[cBox] = c;
    }

    /// <summary>
    /// Menambahkan Nomor Kontainer (Prefix + 6 Digit + Check Digit berbingkai kotak),
    /// ISO Code (misal 40HC / 20GP), dan berat TARE (misal TARE 3,810 KG) di pintu belakang kontainer.
    /// </summary>
    public void AddContainerMarkingsOnDoor(GameObject containerGo, Bounds localBounds, string ctrNo, string isoCode, string tareWeight, bool is40ft)
    {
        for (int i = containerGo.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = containerGo.transform.GetChild(i);
            if (child.name == "ContainerMarkings" || child.name == "LeftCol" || child.name == "RightCol" || child.name.StartsWith("SideMarking"))
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        string formattedCtr = (ctrNo ?? "").Split(' ')[0].ToUpper().Trim();
        if (string.IsNullOrEmpty(formattedCtr)) formattedCtr = is40ft ? "MSCU6461024" : "CKSU2212799";

        string prefix = "";
        string digits = formattedCtr;
        string checkDigit = "";

        if (formattedCtr.Length >= 11)
        {
            prefix = formattedCtr.Substring(0, 4);
            digits = formattedCtr.Substring(4, 6);
            checkDigit = formattedCtr.Substring(10, 1);
        }
        else if (formattedCtr.Length == 10)
        {
            prefix = formattedCtr.Substring(0, 4);
            digits = formattedCtr.Substring(4, 6);
            checkDigit = ContainerManager.CalculateCheckDigit(prefix + digits).ToString();
        }
        else if (formattedCtr.Length > 4)
        {
            prefix = formattedCtr.Substring(0, 4);
            digits = formattedCtr.Substring(4);
        }

        string isoText = string.IsNullOrEmpty(isoCode) ? "" : isoCode.ToUpper().Trim();
        if (isoText == "-" || string.IsNullOrEmpty(isoText))
        {
            isoText = is40ft ? "40HC" : "20GP";
        }

        string tareDisplay = "";
        if (!string.IsNullOrEmpty(tareWeight))
        {
            string cleanTare = tareWeight.Trim();
            if (float.TryParse(cleanTare, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float tVal) && tVal > 0)
            {
                tareDisplay = $"{tVal:N0} KG";
            }
            else if (cleanTare != "-" && cleanTare != "0" && cleanTare != "")
            {
                tareDisplay = cleanTare.ToUpper();
                if (!tareDisplay.EndsWith("KG") && !tareDisplay.EndsWith("KGS")) tareDisplay += " KG";
            }
        }
        if (string.IsNullOrEmpty(tareDisplay))
        {
            tareDisplay = is40ft ? "3,810 KG" : "2,150 KG";
        }

        GameObject textObj = new GameObject("ContainerMarkings");
        textObj.transform.SetParent(containerGo.transform, false);

        Vector3 localScale = containerGo.transform.localScale;
        float sX = Mathf.Approximately(localScale.x, 0f) ? 0.001f : localScale.x;
        float sY = Mathf.Approximately(localScale.y, 0f) ? 0.001f : localScale.y;
        float sZ = Mathf.Approximately(localScale.z, 0f) ? 0.001f : localScale.z;

        // Posisi di panel pintu kanan belakang kontainer (identik dengan SampleScene / ContainerManager)
        float localX = (localBounds.center.x - (localBounds.size.x * 0.33f) + doorTextXOffset) / sX;
        float localY = (localBounds.center.y + (localBounds.size.y * 0.28f)) / sY;
        float localZ = (localBounds.max.z + doorTextZOffset) / sZ;

        textObj.transform.localPosition = new Vector3(localX, localY, localZ);
        textObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        textObj.transform.localScale = new Vector3((1f / sX) * 0.65f, 1f / sY, 1f / sZ);

        string line1;
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!string.IsNullOrEmpty(checkDigit))
        {
            line1 = string.IsNullOrEmpty(prefix)
                ? $"<b>{digits} <space=0.35em>[{checkDigit}]</b>"
                : $"<b>{prefix}<pos=48%>{digits} <space=0.35em>[{checkDigit}]</b>";
        }
        else
        {
            line1 = string.IsNullOrEmpty(prefix)
                ? $"<b>{digits}</b>"
                : $"<b>{prefix}<pos=48%>{digits}</b>";
        }
#else
        if (!string.IsNullOrEmpty(checkDigit))
        {
            line1 = string.IsNullOrEmpty(prefix)
                ? $"<b>{digits} <space=0.35em>{checkDigit}</b>"
                : $"<b>{prefix}<pos=48%>{digits} <space=0.35em>{checkDigit}</b>";
        }
        else
        {
            line1 = string.IsNullOrEmpty(prefix)
                ? $"<b>{digits}</b>"
                : $"<b>{prefix}<pos=48%>{digits}</b>";
        }
#endif

        string line2 = $"<pos=48%><b>{isoText}</b>";
        string line3 = $"TARE<pos=48%>{tareDisplay}";

        string contentText = line1 + "\n" + line2 + "\n\n<size=80%>" + line3 + "</size>";

        TMPro.TextMeshPro tmp = textObj.AddComponent<TMPro.TextMeshPro>();
        tmp.text = contentText;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.3f;
        tmp.fontSizeMax = (doorTextFontSize > 0.1f) ? Mathf.Max(1.3f, doorTextFontSize) : 1.3f;
        tmp.alignment = TMPro.TextAlignmentOptions.Left;
        tmp.color = new Color(0.92f, 0.92f, 0.92f, 0.95f);
        tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(1.10f, 0.75f);

#if !UNITY_WEBGL
        if (Application.isPlaying)
        {
            tmp.outlineWidth = 0.15f;
            tmp.outlineColor = Color.black;
        }
#endif

        tmp.ForceMeshUpdate();

#if !UNITY_WEBGL || UNITY_EDITOR
        if (!string.IsNullOrEmpty(checkDigit) && tmp.textInfo != null && tmp.textInfo.characterCount > 0)
        {
            int checkCharIdx = -1;
            if (tmp.textInfo.lineCount > 0)
            {
                TMPro.TMP_LineInfo line0 = tmp.textInfo.lineInfo[0];
                checkCharIdx = line0.lastVisibleCharacterIndex;
            }

            if (checkCharIdx >= 0 && checkCharIdx < tmp.textInfo.characterCount)
            {
                TMPro.TMP_CharacterInfo cInfo = tmp.textInfo.characterInfo[checkCharIdx];
                if (cInfo.isVisible)
                {
                    float digitBottomY = cInfo.bottomLeft.y;
                    float digitTopY = cInfo.topRight.y;
                    float digitLeftX = cInfo.bottomLeft.x;
                    float digitRightX = cInfo.topRight.x;

                    float digitWidth = digitRightX - digitLeftX;
                    float digitHeight = digitTopY - digitBottomY;
                    float digitCenterX = (digitLeftX + digitRightX) * 0.5f;
                    float digitCenterY = (digitBottomY + digitTopY) * 0.5f;

                    float thick = Mathf.Max(0.007f, digitHeight * 0.075f);
                    float padX = Mathf.Max(0.020f, digitWidth * 0.35f);
                    float padY = Mathf.Max(0.015f, digitHeight * 0.18f);

                    float boxW = digitWidth + (padX * 2f) + (thick * 2f);
                    float boxH = digitHeight + (padY * 2f) + (thick * 2f);

                    GameObject boxObj = new GameObject("CheckDigitBox");
                    boxObj.transform.SetParent(textObj.transform, false);
                    boxObj.transform.localPosition = new Vector3(digitCenterX, digitCenterY, -0.001f);
                    boxObj.transform.localRotation = Quaternion.identity;
                    boxObj.transform.localScale = Vector3.one;

                    Material borderMat = GetWhiteBorderMaterial();

                    CreateMarkingBorderQuad(boxObj.transform, "Border_Top", new Vector3(0f, (boxH - thick) * 0.5f, 0f), new Vector3(boxW, thick, 0.001f), borderMat);
                    CreateMarkingBorderQuad(boxObj.transform, "Border_Bottom", new Vector3(0f, -(boxH - thick) * 0.5f, 0f), new Vector3(boxW, thick, 0.001f), borderMat);

                    float innerH = boxH - (thick * 2f);
                    CreateMarkingBorderQuad(boxObj.transform, "Border_Left", new Vector3(-(boxW - thick) * 0.5f, 0f, 0f), new Vector3(thick, innerH, 0.001f), borderMat);
                    CreateMarkingBorderQuad(boxObj.transform, "Border_Right", new Vector3((boxW - thick) * 0.5f, 0f, 0f), new Vector3(thick, innerH, 0.001f), borderMat);
                }
            }
        }
#endif
    }

    private static Material GetWhiteBorderMaterial()
    {
        if (sharedWhiteBorderMat == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Standard");

            sharedWhiteBorderMat = new Material(s);
            sharedWhiteBorderMat.name = "ContainerMarking_WhiteBorder";
            Color col = new Color(0.92f, 0.92f, 0.92f, 0.95f);
            sharedWhiteBorderMat.color = col;
            if (sharedWhiteBorderMat.HasProperty("_BaseColor")) sharedWhiteBorderMat.SetColor("_BaseColor", col);
            if (sharedWhiteBorderMat.HasProperty("_Color")) sharedWhiteBorderMat.SetColor("_Color", col);
        }
        return sharedWhiteBorderMat;
    }

    private void CreateMarkingBorderQuad(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        quad.name = name;
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = localPos;
        quad.transform.localRotation = Quaternion.identity;
        quad.transform.localScale = size;

        Collider col = quad.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }

        Renderer r = quad.GetComponent<Renderer>();
        if (r != null)
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    /// <summary>
    /// Revisi 4: Tambahkan tulisan Nomor Kontainer (vertikal), ISO Code, dan TARE di samping kanan & kiri kontainer
    /// sesuai dengan standar ISO 6346 dan foto referensi stensil sudut pintu kontainer.
    /// </summary>
    public void AddContainerSideMarkings(GameObject containerGo, Bounds localBounds, string ctrNo, string isoCode, string tareWeight, bool is40ft)
    {
        for (int i = containerGo.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = containerGo.transform.GetChild(i);
            if (child.name.StartsWith("SideMarking"))
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        string formattedCtr = (ctrNo ?? "").Split(' ')[0].ToUpper().Trim();
        if (string.IsNullOrEmpty(formattedCtr)) formattedCtr = is40ft ? "MSCU6461024" : "CKSU2212799";

        string prefix = "";
        string digits = formattedCtr;
        string checkDigit = "";

        if (formattedCtr.Length >= 11)
        {
            prefix = formattedCtr.Substring(0, 4);
            digits = formattedCtr.Substring(4, 6);
            checkDigit = formattedCtr.Substring(10, 1);
        }
        else if (formattedCtr.Length == 10)
        {
            prefix = formattedCtr.Substring(0, 4);
            digits = formattedCtr.Substring(4, 6);
            checkDigit = ContainerManager.CalculateCheckDigit(prefix + digits).ToString();
        }
        else if (formattedCtr.Length > 4)
        {
            prefix = formattedCtr.Substring(0, 4);
            digits = formattedCtr.Substring(4);
        }

        string isoText = string.IsNullOrEmpty(isoCode) ? "" : isoCode.ToUpper().Trim();
        if (isoText == "-" || string.IsNullOrEmpty(isoText))
        {
            isoText = is40ft ? "40HC" : "20GP";
        }

        string tareDisplay = "";
        if (!string.IsNullOrEmpty(tareWeight))
        {
            string cleanTare = tareWeight.Trim();
            if (float.TryParse(cleanTare, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float tVal) && tVal > 0)
            {
                tareDisplay = $"{tVal:N0} KG";
            }
            else if (cleanTare != "-" && cleanTare != "0" && cleanTare != "")
            {
                tareDisplay = cleanTare.ToUpper();
                if (!tareDisplay.EndsWith("KG") && !tareDisplay.EndsWith("KGS")) tareDisplay += " KG";
            }
        }
        if (string.IsNullOrEmpty(tareDisplay))
        {
            tareDisplay = is40ft ? "3,810 KG" : "2,150 KG";
        }

        // Susun teks Kolom 1 (Nomor Kontainer vertikal sesuai foto stensil):
        // F
        // U
        // K
        // U
        // 
        // 2
        // 4
        // 3
        // 0
        // 3
        // 0
        //
        // 0 (di dalam rectangle kotak 3D, tanpa tanda kurung [ ])
        string vertPrefix = string.Join("\n", prefix.ToCharArray());
        string vertDigits = string.Join("\n", digits.ToCharArray());
        string col1Text = string.IsNullOrEmpty(checkDigit)
            ? $"<b>{vertPrefix}\n\n{vertDigits}</b>"
            : $"<b>{vertPrefix}\n\n{vertDigits}\n\n{checkDigit}</b>";

        // Susun teks Kolom 2 (ISO Code & TARE):
        // 2
        // 2
        // G
        // 1
        //
        // TARE
        // 2,150 KG
        string vertIso = string.Join("\n", isoText.ToCharArray());
        string col2Text = $"<b>{vertIso}\n\n<size=70%>TARE\n{tareDisplay}</size></b>";

        Vector3 localScale = containerGo.transform.localScale;
        float sX = Mathf.Approximately(localScale.x, 0f) ? 0.001f : localScale.x;
        float sY = Mathf.Approximately(localScale.y, 0f) ? 0.001f : localScale.y;
        float sZ = Mathf.Approximately(localScale.z, 0f) ? 0.001f : localScale.z;

        // Posisi Y: Mulai dari atas kontainer (tepat di bawah rail atas)
        float localY = (localBounds.max.y - 0.18f) / sY;
        // Posisi Z: Masuk ke dalam panel samping, jauh dari tiang pintu belakang
        float localZ = (localBounds.max.z - 0.65f) / sZ;

        // Sisi Kiri (rotasi 90 agar menghadap keluar -X ke arah kamera, tidak terbalik/mirrored)
        CreateSingleSideMarking(containerGo.transform, "SideMarking_Left",
            new Vector3((localBounds.min.x - 0.015f) / sX, localY, localZ),
            Quaternion.Euler(0f, 90f, 0f),
            new Vector3(1f / sZ, 1f / sY, 1f / sX),
            col1Text, col2Text, checkDigit, isLeft: true);

        // Sisi Kanan (rotasi -90 agar menghadap keluar +X ke arah kamera, tidak terbalik/mirrored)
        CreateSingleSideMarking(containerGo.transform, "SideMarking_Right",
            new Vector3((localBounds.max.x + 0.015f) / sX, localY, localZ),
            Quaternion.Euler(0f, -90f, 0f),
            new Vector3(1f / sZ, 1f / sY, 1f / sX),
            col1Text, col2Text, checkDigit, isLeft: false);
    }

    private void CreateSingleSideMarking(Transform parent, string goName, Vector3 localPos, Quaternion localRot, Vector3 localScale, string col1Text, string col2Text, string checkDigit, bool isLeft)
    {
        GameObject sideObj = new GameObject(goName);
        sideObj.transform.SetParent(parent, false);
        sideObj.transform.localPosition = localPos;
        sideObj.transform.localRotation = localRot;
        sideObj.transform.localScale = localScale;

        // Posisi kolom: Kolom 1 (Nomor) ke arah badan kontainer, Kolom 2 (ISO/TARE) ke arah tiang pintu belakang
        float col1X = isLeft ? 0.16f : -0.16f;
        float col2X = isLeft ? -0.16f : 0.16f;

        CreateTMPColumn(sideObj.transform, "Col1_CtrNo", new Vector3(col1X, 0f, 0f), col1Text, 1.05f, checkDigit);
        CreateTMPColumn(sideObj.transform, "Col2_IsoTare", new Vector3(col2X, 0f, 0f), col2Text, 1.05f, null);
    }

    private void CreateTMPColumn(Transform parent, string colName, Vector3 localPos, string textContent, float maxFontSize, string checkDigit)
    {
        GameObject colObj = new GameObject(colName);
        colObj.transform.SetParent(parent, false);
        colObj.transform.localPosition = localPos;
        colObj.transform.localRotation = Quaternion.identity;
        colObj.transform.localScale = Vector3.one;

        TMPro.TextMeshPro tmp = colObj.AddComponent<TMPro.TextMeshPro>();
        tmp.text = textContent;
        tmp.enableAutoSizing = false;
        tmp.fontSize = maxFontSize;
        tmp.alignment = TMPro.TextAlignmentOptions.Top;
        tmp.color = new Color(0.96f, 0.96f, 0.96f, 0.98f);
        tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        tmp.lineSpacing = -18f;

        RectTransform rt = tmp.rectTransform;
        rt.pivot = new Vector2(0.5f, 1f); // Pivot di atas
        rt.sizeDelta = new Vector2(0.50f, 2.50f);

#if !UNITY_WEBGL
        if (Application.isPlaying)
        {
            tmp.outlineWidth = 0.24f;
            tmp.outlineColor = Color.black;
        }
#endif
        tmp.ForceMeshUpdate();

        // Buat border kotak 3D nyata (rectangle) di sekeliling angka check digit jika ada
        if (!string.IsNullOrEmpty(checkDigit) && tmp.textInfo != null && tmp.textInfo.characterCount > 0)
        {
            int checkCharIdx = tmp.textInfo.characterCount - 1;
            while (checkCharIdx >= 0 && !tmp.textInfo.characterInfo[checkCharIdx].isVisible)
            {
                checkCharIdx--;
            }

            if (checkCharIdx >= 0)
            {
                TMPro.TMP_CharacterInfo cInfo = tmp.textInfo.characterInfo[checkCharIdx];
                float digitBottomY = cInfo.bottomLeft.y;
                float digitTopY = cInfo.topRight.y;
                float digitLeftX = cInfo.bottomLeft.x;
                float digitRightX = cInfo.topRight.x;

                float digitWidth = digitRightX - digitLeftX;
                float digitHeight = digitTopY - digitBottomY;
                float digitCenterX = (digitLeftX + digitRightX) * 0.5f;
                float digitCenterY = (digitBottomY + digitTopY) * 0.5f;

                float thick = Mathf.Max(0.012f, digitHeight * 0.08f);
                float padX = Mathf.Max(0.025f, digitWidth * 0.35f);
                float padY = Mathf.Max(0.020f, digitHeight * 0.20f);

                float boxW = digitWidth + (padX * 2f) + (thick * 2f);
                float boxH = digitHeight + (padY * 2f) + (thick * 2f);

                GameObject boxObj = new GameObject("CheckDigitBox");
                boxObj.transform.SetParent(colObj.transform, false);
                boxObj.transform.localPosition = new Vector3(digitCenterX, digitCenterY, -0.003f);
                boxObj.transform.localRotation = Quaternion.identity;
                boxObj.transform.localScale = Vector3.one;

                Material borderMat = GetWhiteBorderMaterial();

                CreateMarkingBorderQuad(boxObj.transform, "Border_Top", new Vector3(0f, (boxH - thick) * 0.5f, 0f), new Vector3(boxW, thick, 0.002f), borderMat);
                CreateMarkingBorderQuad(boxObj.transform, "Border_Bottom", new Vector3(0f, -(boxH - thick) * 0.5f, 0f), new Vector3(boxW, thick, 0.002f), borderMat);

                float innerH = boxH - (thick * 2f);
                CreateMarkingBorderQuad(boxObj.transform, "Border_Left", new Vector3(-(boxW - thick) * 0.5f, 0f, 0f), new Vector3(thick, innerH, 0.002f), borderMat);
                CreateMarkingBorderQuad(boxObj.transform, "Border_Right", new Vector3((boxW - thick) * 0.5f, 0f, 0f), new Vector3(thick, innerH, 0.002f), borderMat);
            }
        }
    }

    private static Dictionary<string, GameObject> cachedContainerPrefabs = new Dictionary<string, GameObject>();

    GameObject GetContainerPrefab(string agent, string merk, bool is40ft)
    {
        string a = (agent ?? "").ToLower().Trim();
        string m = (merk ?? "").ToLower().Trim();
        string combined = a + " " + m;

        // 1. Utamakan referensi prefab yang sudah terpasang di komponen ContainerManager di scene (jika ada)
        ContainerManager cm = ContainerManager.Instance ?? Object.FindFirstObjectByType<ContainerManager>();
        if (cm != null)
        {
            if (is40ft)
            {
                if (combined.Contains("msc") && cm.prefab40ftMSC != null) return cm.prefab40ftMSC;
                if (combined.Contains("ck") && cm.prefab40ftCK != null) return cm.prefab40ftCK;
                if (combined.Contains("sls") && cm.prefab40ftSLS != null) return cm.prefab40ftSLS;
                if (combined.Contains("sks") && cm.prefab40ftSKS != null) return cm.prefab40ftSKS;
                if (combined.Contains("sbs") && cm.prefab40ftSBS != null) return cm.prefab40ftSBS;
                if ((combined.Contains("whl") || combined.Contains("wan hai")) && cm.prefab40ftWHL != null) return cm.prefab40ftWHL;
            }
            else
            {
                if (combined.Contains("msc") && cm.prefab20ftMSC != null) return cm.prefab20ftMSC;
                if (combined.Contains("ck") && cm.prefab20ftCK != null) return cm.prefab20ftCK;
                if (combined.Contains("sls") && cm.prefab20ftSLS != null) return cm.prefab20ftSLS;
                if (combined.Contains("sks") && cm.prefab20ftSKS != null) return cm.prefab20ftSKS;
                if (combined.Contains("sbs") && cm.prefab20ftSBS != null) return cm.prefab20ftSBS;
                if ((combined.Contains("whl") || combined.Contains("wan hai")) && cm.prefab20ftWHL != null) return cm.prefab20ftWHL;
            }
        }

        string sizePrefix = is40ft ? "40feet" : "20feet";
        string targetName = "";
        if (combined.Contains("msc")) targetName = sizePrefix + "MSC";
        else if (combined.Contains("ck")) targetName = sizePrefix + "CKline";
        else if (combined.Contains("sls")) targetName = sizePrefix + "SLS";
        else if (combined.Contains("sks")) targetName = sizePrefix + "SKS";
        else if (combined.Contains("sbs")) targetName = sizePrefix + "SBS";
        else if (combined.Contains("whl") || combined.Contains("wan hai")) targetName = sizePrefix + "Whl";
        else if (combined.Contains("meratus")) targetName = sizePrefix + "Meratus";
        else if (combined.Contains("samudera")) targetName = sizePrefix + "Samudera";
        else if (combined.Contains("spil")) targetName = sizePrefix + "Spil";
        else if (combined.Contains("tanto")) targetName = sizePrefix + "Tanto";
        else if (combined.Contains("temas")) targetName = sizePrefix + "Temasline";
        else if (combined.Contains("evergreen")) targetName = sizePrefix + "Evergreen";
        else if (combined.Contains("maersk")) targetName = sizePrefix + "Maersk";
        else if (combined.Contains("cma")) targetName = sizePrefix + "CmaCgm";

        // JIKA AGENT TIDAK DIKENAL / BELUM ADA PREFABNYA:
        // Gunakan variasi branded (Temasline, Evergreen, Meratus, Samudera, Tanto, Spil, CKline, SLS, MSC, Whl)
        // PENTING: JANGAN PERNAH gunakan prefab polos putih!
        if (string.IsNullOrEmpty(targetName))
        {
            string[] fallbackBrands = { "Temasline", "Evergreen", "Meratus", "Samudera", "Tanto", "Spil", "CKline", "SLS", "MSC", "Whl", "CmaCgm" };
            int hash = Mathf.Abs(combined.GetHashCode());
            string pickedBrand = fallbackBrands[hash % fallbackBrands.Length];
            targetName = sizePrefix + pickedBrand;
        }

        // 2. Load dari Resources (berjalan sempurna di Editor dan WebGL build runtime)
        if (!string.IsNullOrEmpty(targetName))
        {
            if (cachedContainerPrefabs.TryGetValue(targetName, out GameObject cached) && cached != null)
            {
                return cached;
            }
            GameObject resPrefab = Resources.Load<GameObject>("PrefabContainer/" + targetName);
            if (resPrefab != null)
            {
                cachedContainerPrefabs[targetName] = resPrefab;
                return resPrefab;
            }
        }

        // 3. Fallback darurat jika targetName gagal: langsung load Temasline / MSC (prefab 3D asli lengkap logo & tekstur)
        string safeFallback = sizePrefix + "Temasline";
        if (cachedContainerPrefabs.TryGetValue(safeFallback, out GameObject cachedSafe) && cachedSafe != null)
        {
            return cachedSafe;
        }
        GameObject resSafe = Resources.Load<GameObject>("PrefabContainer/" + safeFallback);
        if (resSafe != null)
        {
            cachedContainerPrefabs[safeFallback] = resSafe;
            return resSafe;
        }

        string secondaryFallback = sizePrefix + "MSC";
        if (cachedContainerPrefabs.TryGetValue(secondaryFallback, out GameObject cachedMsc) && cachedMsc != null)
        {
            return cachedMsc;
        }
        GameObject resMsc = Resources.Load<GameObject>("PrefabContainer/" + secondaryFallback);
        if (resMsc != null)
        {
            cachedContainerPrefabs[secondaryFallback] = resMsc;
            return resMsc;
        }

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(targetName))
        {
            string path = "Assets/PrefabContainer/" + targetName + ".prefab";
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;
        }
#endif

        return null;
    }

    Color GetAgentColor(string agent, string merk)
    {
        string a = (agent ?? "").ToLower().Trim();
        string m = (merk ?? "").ToLower().Trim();

        if (a.Contains("msc") || m.Contains("msc")) return new Color(0.82f, 0.62f, 0.15f, 1f);
        if (a.Contains("ck") || m.Contains("ck")) return new Color(0.85f, 0.22f, 0.2f, 1f);
        if (a.Contains("sls") || m.Contains("sls")) return new Color(0.18f, 0.45f, 0.85f, 1f);
        if (a.Contains("sks") || m.Contains("sks")) return new Color(0.2f, 0.68f, 0.3f, 1f);
        if (a.Contains("sbs") || m.Contains("sbs")) return new Color(0.92f, 0.52f, 0.12f, 1f);
        if (a.Contains("whl") || m.Contains("whl")) return new Color(0.12f, 0.25f, 0.52f, 1f);

        return new Color(0.35f, 0.55f, 0.75f, 1f);
    }

    void GenerateContainerStacksFallback(Transform containerGroup, string blockName, int maxRow, int maxSlot, float startY, Shader s, Vector2 yardSize)
    {
        maxRow = Mathf.Clamp(maxRow, 2, 6);
        maxSlot = Mathf.Clamp(maxSlot, 4, 16);

        Vector3 gridOrigin = GetGridOriginOffset(maxRow, maxSlot, yardSize);
        float startX = gridOrigin.x;
        float startZ = gridOrigin.z;

        string[] agents = new string[] { "MSC", "CK", "SLS", "SKS", "SBS", "WHL" };
        string[] prefixes = new string[] { "MSCU", "CKSU", "SLVU", "SKSU", "SBSU", "WHLU" };

        int seq = 100000;
        for (int r = 1; r <= maxRow; r++)
        {
            for (int sl = 1; sl <= maxSlot; sl += 2)
            {
                int stackHeight = ((r + sl) % 3) + 1;
                for (int t = 1; t <= stackHeight; t++)
                {
                    int idx = (r + sl + t) % agents.Length;
                    bool is40 = ((r + sl) % 2 == 0);
                    string num10 = prefixes[idx] + (seq + r * 100 + sl * 10 + t).ToString("000000");
                    int chk = ContainerManager.CalculateCheckDigit(num10);

                    ContainerLocData dummy = new ContainerLocData
                    {
                        YARD_BLOCK = blockName,
                        YARD_ROW = r.ToString(),
                        YARD_SLOT = sl.ToString(),
                        YARD_TIER = t.ToString(),
                        AGENT = agents[idx],
                        CTR_MERK = agents[idx],
                        CTR_SIZE = is40 ? "40" : "20",
                        ISO_CODE = is40 ? "40HC" : "20GP",
                        TARE = is40 ? "3840" : "2150",
                        CTR_NO = num10 + chk.ToString()
                    };

                    SpawnSingleContainerInYard(containerGroup, dummy, r, sl, t, startX, startY, startZ, s);
                }
            }
        }
    }

    Material GetOrCreateYardMaterial(string blockName)
    {
        if (yardMaterial != null) return yardMaterial;

        if (yardMaterials.ContainsKey(blockName) && yardMaterials[blockName] != null)
            return yardMaterials[blockName];

#if UNITY_EDITOR
        Material defaultMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Beton_Pelataran.002.mat");
        if (defaultMat != null)
        {
            yardMaterials[blockName] = defaultMat;
            return defaultMat;
        }
#endif

        // Abu-abu semen beton pelataran terang yang kontras dan kokoh di atas paving vr-env (identik dengan platform Gedung)
        Color concreteGrey = new Color(0.72f, 0.74f, 0.76f, 1f);

        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");

        Material mat = new Material(s);
        mat.color = concreteGrey;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", concreteGrey);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", concreteGrey);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
        mat.name = "YardMat_" + blockName;

        yardMaterials[blockName] = mat;
        return mat;
    }

    Color GenerateBlockColor(string blockName)
    {
        // Abu-abu beton pelataran terang
        return new Color(0.72f, 0.74f, 0.76f, 1f);
    }

    // --- Spawn Yard ---

    public void SpawnYard(string blockName)
    {
        DraggableYard existing = activeYards.Find(y => y != null && y.blockName == blockName);
        if (existing != null)
        {
            DeselectAll();
            selectedYard = existing;
            selectedYard.Select();
            FocusCameraOn(existing.transform.position);
            Debug.Log("[YardEditor] Yard " + blockName + " sudah ada. Kamera difokuskan ke posisinya.");
            if (editorUI != null) editorUI.ShowToastNotification("Yard " + blockName + " terpilih! Geser dengan mouse atau klik Delete.");
            return;
        }

        Vector2 size = CalculateYardSizeFromName(blockName);
        Vector3 spawnPos = GetCameraLookAtGroundPoint();

        GameObject yardGo = GenerateYardProcedural(blockName, size);
        yardGo.transform.position = spawnPos;

        DraggableYard draggable = yardGo.AddComponent<DraggableYard>();
        draggable.blockName = blockName;

        MasterBlockData mb = GetMasterBlock(blockName);
        if (mb != null)
        {
            draggable.maxRow = int.TryParse(mb.YARD_ROW, out int r) ? r : 6;
            draggable.maxSlot = int.TryParse(mb.YARD_SLOT, out int s) ? s : 45;
            draggable.maxTier = int.TryParse(mb.YARD_TIER, out int t) ? t : 7;
        }

        activeYards.Add(draggable);

        DeselectAll();
        selectedYard = draggable;
        selectedYard.Select();
        FocusCameraOn(spawnPos);

        Debug.Log("[YardEditor] Yard " + blockName + " spawned at " + spawnPos + "! Size: " + size.x + " x " + size.y + " meters");
    }

    public void SpawnYardAtPosition(string blockName, Vector3 position, bool isHorizontal, float customWidth = 0, float customLength = 0)
    {
        foreach (var y in activeYards)
        {
            if (y != null && y.blockName == blockName) return;
        }

        Vector2 size;
        if (customWidth > 0 && customLength > 0)
            size = new Vector2(customWidth, customLength);
        else
            size = CalculateYardSizeFromName(blockName);

        GameObject yardGo = GenerateYardProcedural(blockName, size);
        yardGo.transform.position = position;

        DraggableYard draggable = yardGo.AddComponent<DraggableYard>();
        draggable.blockName = blockName;

        MasterBlockData mb = GetMasterBlock(blockName);
        if (mb != null)
        {
            draggable.maxRow = int.TryParse(mb.YARD_ROW, out int r) ? r : 6;
            draggable.maxSlot = int.TryParse(mb.YARD_SLOT, out int s) ? s : 45;
            draggable.maxTier = int.TryParse(mb.YARD_TIER, out int t) ? t : 7;
        }

        if (isHorizontal)
        {
            draggable.SetOrientation(true);
        }

        activeYards.Add(draggable);
    }

    // --- Spawn Road ---

    public void SpawnRoad()
    {
        FindGroundY();
        Vector3 spawnPos = GetCameraLookAtGroundPoint();
        spawnPos.y = groundY;

        GameObject roadGo = new GameObject("Road_" + nextRoadId);
        roadGo.transform.position = spawnPos;

        DraggableRoad road = roadGo.AddComponent<DraggableRoad>();
        road.roadId = nextRoadId;
        road.BuildRoadVisual();

        activeRoads.Add(road);
        nextRoadId++;

        DeselectAll();
        selectedRoad = road;
        selectedRoad.Select();
        FocusCameraOn(spawnPos);

        Debug.Log("[YardEditor] Road " + road.roadId + " spawned at " + spawnPos + "!");
    }

    public void SpawnRoadAtPosition(int id, Vector3 position, float rotY, float sX, float sZ)
    {
        FindGroundY();
        if (position.y < 19.5f || position.y > 19.95f) position.y = groundY;

        GameObject roadGo = new GameObject("Road_" + id);
        roadGo.transform.position = position;

        DraggableRoad road = roadGo.AddComponent<DraggableRoad>();
        road.roadId = id;
        road.roadWidth = sX;
        road.roadLength = sZ;
        road.SetDirection(rotY);
        road.BuildRoadVisual();

        activeRoads.Add(road);
        if (id >= nextRoadId) nextRoadId = id + 1;
    }

    // --- Spawn Accessory (Gedung, ReachStacker, TopLoader, Gate, Pagar) ---

    public void SpawnAccessory(string accessoryType)
    {
        Vector3 spawnPos = GetCameraLookAtGroundPoint();

        GameObject accGo = new GameObject("Accessory_" + accessoryType + "_" + nextAccessoryId);
        accGo.transform.position = spawnPos;

        DraggableAccessory acc = accGo.AddComponent<DraggableAccessory>();
        acc.accessoryId = nextAccessoryId;
        acc.accessoryType = accessoryType;
        acc.scaleMultiplier = 1f;
        acc.fenceLength = 40f;

        try
        {
            acc.BuildAccessoryVisual();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[YardEditor] Error building visual for " + accessoryType + ": " + ex);
        }

        activeAccessories.Add(acc);
        nextAccessoryId++;

        DeselectAll();
        selectedAccessory = acc;
        selectedAccessory.Select();
        FocusCameraOn(spawnPos);

        Debug.Log("[YardEditor] Accessory " + accessoryType + " #" + acc.accessoryId + " spawned at " + spawnPos + "!");
    }

    public void SpawnAccessoryAtPosition(int id, string accessoryType, Vector3 position, float rotY, float scaleMultiplier = 1f, float fenceLength = 40f)
    {
        GameObject accGo = new GameObject("Accessory_" + accessoryType + "_" + id);
        accGo.transform.position = position;

        DraggableAccessory acc = accGo.AddComponent<DraggableAccessory>();
        acc.accessoryId = id;
        acc.accessoryType = string.IsNullOrEmpty(accessoryType) ? "Gedung" : accessoryType;
        acc.scaleMultiplier = scaleMultiplier <= 0.05f ? 1f : scaleMultiplier;
        acc.fenceLength = fenceLength <= 1f ? 40f : fenceLength;
        acc.directionAngle = rotY;

        try
        {
            acc.BuildAccessoryVisual();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[YardEditor] Error building visual for " + accessoryType + " at pos: " + ex);
        }

        acc.SetDirection(rotY);

        activeAccessories.Add(acc);
        if (id >= nextAccessoryId) nextAccessoryId = id + 1;
    }

    public void AdjustSelectedAccessoryScale(float delta)
    {
        if (selectedAccessory != null)
        {
            selectedAccessory.AdjustScale(delta);
        }
    }

    public void AdjustSelectedPagarLength(float delta)
    {
        if (selectedAccessory != null && selectedAccessory.accessoryType == "Pagar")
        {
            selectedAccessory.AdjustLength(delta);
        }
        else if (selectedRoad != null)
        {
            selectedRoad.AdjustLength(delta);
        }
    }

    // --- Delete ---

    public void DeleteSelected()
    {
        if (selectedYard != null)
        {
            string bName = selectedYard.blockName;
            activeYards.Remove(selectedYard);

            GameObject numObj = GameObject.Find("Numbering_" + bName);
            if (numObj != null)
            {
                if (Application.isPlaying) Destroy(numObj);
                else DestroyImmediate(numObj);
            }

            if (Application.isPlaying) Destroy(selectedYard.gameObject);
            else DestroyImmediate(selectedYard.gameObject);
            selectedYard = null;

            if (editorUI != null) editorUI.RebuildYardButtons();
            Debug.Log("[YardEditor] Yard " + bName + " beserta kontainer dan penomorannya berhasil dihapus!");
        }
        else if (selectedRoad != null)
        {
            activeRoads.Remove(selectedRoad);
            if (Application.isPlaying) Destroy(selectedRoad.gameObject);
            else DestroyImmediate(selectedRoad.gameObject);
            selectedRoad = null;
            Debug.Log("[YardEditor] Road berhasil dihapus!");
        }
        else if (selectedAccessory != null)
        {
            activeAccessories.Remove(selectedAccessory);
            if (Application.isPlaying) Destroy(selectedAccessory.gameObject);
            else DestroyImmediate(selectedAccessory.gameObject);
            selectedAccessory = null;
            Debug.Log("[YardEditor] Accessory berhasil dihapus!");
        }
    }

    // --- Clear All / Purge ---

    public void PurgeAllYardObjects()
    {
        foreach (var y in activeYards)
        {
            if (y != null)
            {
                if (Application.isPlaying) Destroy(y.gameObject);
                else DestroyImmediate(y.gameObject);
            }
        }
        activeYards.Clear();

        foreach (var r in activeRoads)
        {
            if (r != null)
            {
                if (Application.isPlaying) Destroy(r.gameObject);
                else DestroyImmediate(r.gameObject);
            }
        }
        activeRoads.Clear();

        foreach (var a in activeAccessories)
        {
            if (a != null)
            {
                if (Application.isPlaying) Destroy(a.gameObject);
                else DestroyImmediate(a.gameObject);
            }
        }
        activeAccessories.Clear();
        spawnedContainerInfo.Clear();
        clickedContainerData = null;

        // Nonaktifkan spawn otomatis ContainerManager agar tidak memunculkan kontainer liar di posisi lama
        ContainerManager cm = ContainerManager.Instance ?? Object.FindFirstObjectByType<ContainerManager>();
        if (cm != null)
        {
            cm.StopAllCoroutines();
            cm.enabled = false;
            cm.ClearContainersInEditMode();
        }

        // Objek lingkungan kustom (seperti Plane laut, Light, vr-env) dilindungi dan tidak akan pernah dihapus.
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        List<GameObject> toDestroy = new List<GameObject>();
        bool isPre1Eagle = (gameObject.scene.name == "pre1eagle");

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;
            string n = t.name;
            string nLower = n.ToLower();

            if (nLower.Contains("vr-env") || nLower.Contains("camera") || nLower.Contains("light") ||
                nLower.Contains("yardeditor") || nLower.Contains("eventsystem") || nLower.Contains("viewmode") ||
                nLower.Contains("xrrig") || nLower.Contains("player") || nLower.Contains("search") ||
                nLower.Contains("daynight") || nLower.Contains("event") || nLower.Contains("containermanager") ||
                nLower.Contains("plane") || nLower.Contains("laut") || nLower.Contains("sea") ||
                nLower.Contains("water") || nLower.Contains("ocean") || nLower.Contains("ground") ||
                nLower.Contains("terrain") || nLower.Contains("sky"))
                continue;

            bool isContainer = n.Contains("ft)") || n.Contains("ft_") || n.StartsWith("Container_") ||
                               n.StartsWith("20feet") || n.StartsWith("40feet") || n == "ContainersFromAPI" ||
                               n.Contains("ContainerMarkings");

            bool isNumbering = n == "YardNumbering" || n.StartsWith("Numbering_") || n.StartsWith("GridNumbering_") ||
                               n.StartsWith("YardNumberingAndLabels") || n.StartsWith("Txt_") ||
                               n.StartsWith("Text_") || n == "LabelUtara" || n == "LabelSelatan" ||
                               n.StartsWith("BlockIconBox") || n.StartsWith("YellowBlock") || n.Contains("BingkaiKuning");

            bool isYard;
            if (isPre1Eagle)
            {
                // Di pre1eagle: Hapus YardA/B/C dan bawahnya agar digantikan DraggableYard,
                // tapi pertahankan gedung bawaan scene & aksesoris lingkungan
                isYard = (n.StartsWith("Yard") && (n.EndsWith("_Editor") || n.Length <= 6 || n.Contains("Variant"))) ||
                         (nLower.StartsWith("bawahnya") && !nLower.Contains("gedung")) || n == "YardPlatform";
            }
            else
            {
                isYard = (n.StartsWith("Yard") && (n.EndsWith("_Editor") || n.Length <= 6 || n.Contains("Variant"))) ||
                         nLower.StartsWith("bawahnya") || n == "YardPlatform" || nLower == "gedung";
            }

            bool isRoadOrAcc;
            if (isPre1Eagle)
            {
                // Di pre1eagle: Pertahankan pagar (Cube / ContainerPagarBesi) dan markaJalan bawaan scene!
                // Hanya hapus jalan dan aksesoris runtime yang di-spawn oleh YardEditor
                isRoadOrAcc = n.StartsWith("Road_") || n.StartsWith("Accessory_");
            }
            else
            {
                isRoadOrAcc = n.StartsWith("Road_") || n == "jln" || n.StartsWith("jln_") ||
                              n.StartsWith("DashedLine") || n.StartsWith("markaJalan") ||
                              n.StartsWith("Accessory_") || n.StartsWith("Pagar_") ||
                              n.Contains("ContainerPagarBesi") || n.StartsWith("Cube");
            }

            if (isContainer || isNumbering || isYard || isRoadOrAcc)
            {
                if (!toDestroy.Contains(t.gameObject))
                {
                    toDestroy.Add(t.gameObject);
                }
            }
        }

        foreach (GameObject go in toDestroy)
        {
            if (go != null)
            {
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
        }

        selectedYard = null;
        selectedRoad = null;
        selectedAccessory = null;
        nextRoadId = 1;
        nextAccessoryId = 1;

        if (editorUI != null)
        {
            editorUI.RebuildYardButtons();
        }
    }

    public void ClearAll()
    {
        PurgeAllYardObjects();
        YardEditorDataUtil.ClearLayout();
        Debug.Log("[YardEditor] Semua yard, kontainer, jalan, aksesoris, dan penomoran telah dihapus total dari scene! Tersisa hanya VR-env.");
    }

    // --- Selection ---

    public void DeselectAll()
    {
        if (activeYards != null)
        {
            foreach (var y in activeYards)
            {
                if (y != null) y.Deselect();
            }
        }
        selectedYard = null;

        if (activeRoads != null)
        {
            foreach (var r in activeRoads)
            {
                if (r != null) r.Deselect();
            }
        }
        selectedRoad = null;

        if (activeAccessories != null)
        {
            foreach (var a in activeAccessories)
            {
                if (a != null) a.Deselect();
            }
        }
        selectedAccessory = null;
    }

    public DraggableYard GetSelectedYard() { return selectedYard; }
    public DraggableRoad GetSelectedRoad() { return selectedRoad; }
    public DraggableAccessory GetSelectedAccessory() { return selectedAccessory; }

    public void ToggleSelectedOrientation()
    {
        if (selectedYard != null)
        {
            selectedYard.ToggleOrientation();
        }
        else if (selectedRoad != null)
        {
            selectedRoad.RotateDirection();
        }
        else if (selectedAccessory != null)
        {
            selectedAccessory.RotateDirection();
        }
    }

    // --- Save/Load ---

    public void SaveLayout()
    {
        YardLayout layout = new YardLayout();

        foreach (var y in activeYards)
        {
            if (y != null) layout.yards.Add(y.ToPlacement());
        }

        foreach (var r in activeRoads)
        {
            if (r != null) layout.roads.Add(r.ToPlacement());
        }

        foreach (var a in activeAccessories)
        {
            if (a != null) layout.accessories.Add(a.ToPlacement());
        }

        loadedFolderDenah = layout;
        YardEditorDataUtil.SaveLayout(layout);

        // Jika HP VR (Viewer) sedang terhubung, langsung sinkronkan layout baru ke headset VR secara real-time!
        if (VRRemoteSyncManager.Instance != null && VRRemoteSyncManager.Instance.isHost && VRRemoteSyncManager.Instance.isConnected)
        {
            VRRemoteSyncManager.Instance.SyncLayoutToViewer();
        }

        int accCount = layout.accessories != null ? layout.accessories.Count : 0;
        string msg = "Layout berhasil disimpan! (" + layout.yards.Count + " Yard, " + layout.roads.Count + " Road, " + accCount + " Aksesoris)";
        if (editorUI != null) editorUI.ShowToastNotification(msg);
    }

    public void ApplyLayoutJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        pendingLayoutJson = json;
        if (!apiLoaded)
        {
            Debug.Log("[YardEditor] Layout sinkronisasi diterima. Menunggu API selesai sebelum render...");
            return;
        }
        ApplyPendingLayout();
    }

    private void ApplyPendingLayout()
    {
        if (string.IsNullOrEmpty(pendingLayoutJson)) return;
        try
        {
            YardLayout layout = JsonUtility.FromJson<YardLayout>(pendingLayoutJson);
            if (layout != null)
            {
                ApplyLayout(layout);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[YardEditor] Gagal parse JSON layout sinkronisasi: " + ex.Message);
        }
        pendingLayoutJson = null;
    }

    public void ApplyLayout(YardLayout layout)
    {
        if (layout == null) return;
        PurgeAllYardObjects();

        foreach (var yp in layout.yards)
        {
            Vector3 pos = new Vector3(yp.posX, groundY, yp.posZ);
            SpawnYardAtPosition(yp.blockName, pos, yp.isHorizontal, yp.sizeWidth, yp.sizeLength);
        }

        foreach (var rp in layout.roads)
        {
            Vector3 pos = new Vector3(rp.posX, groundY, rp.posZ);
            SpawnRoadAtPosition(rp.id, pos, rp.rotationY, rp.scaleX, rp.scaleZ);
        }

        if (layout.accessories != null)
        {
            foreach (var ap in layout.accessories)
            {
                Vector3 pos = new Vector3(ap.posX, groundY, ap.posZ);
                SpawnAccessoryAtPosition(ap.id, ap.accessoryType, pos, ap.rotationY, ap.scaleMultiplier, ap.fenceLength);
            }
        }

        RefreshAllYardContainersVisibility();

        int accCount = layout.accessories != null ? layout.accessories.Count : 0;
        if (layout.yards.Count > 0 || layout.roads.Count > 0 || accCount > 0)
        {
            string msg = "Layout disinkronkan (" + layout.yards.Count + " Yard, " + layout.roads.Count + " Road, " + accCount + " Aksesoris)";
            if (editorUI != null) editorUI.ShowToastNotification(msg);
        }
    }

    public void LoadLayout()
    {
        YardLayout layout = YardEditorDataUtil.LoadLayout();
        if ((layout == null || (layout.yards.Count == 0 && layout.accessories.Count == 0 && layout.roads.Count == 0)) && loadedFolderDenah != null)
        {
            layout = loadedFolderDenah;
        }
        ApplyLayout(layout);
    }

    /// <summary>
    /// Memuat layout default 1Eagle (Blok A, B, C) dengan posisi dan orientasi presisi sesuai Gambar 1 / SampleScene.
    /// Digunakan di scene pre1eagle sehingga saat pertama kali dibuka, tampilan langsung komplit dengan kontainer.
    /// Blok yard dapat diklik, digeser, dihapus, atau diganti dengan blok lain via Yard Editor.
    /// </summary>
    public void LoadDefault1EagleLayout()
    {
        PurgeAllYardObjects();

        // 1. Yard B (Vertical di kiri, aman dari jalan selatan)
        SpawnYardAtPosition("B", new Vector3(32.9f, groundY, -488.0f), false);

        // 2. Yard A (Vertical di tengah depan, sejajar dengan Yard B)
        SpawnYardAtPosition("A", new Vector3(99.9f, groundY, -488.0f), false);

        // 3. Yard C (Horizontal di atas-kiri sesuai Gambar 2)
        SpawnYardAtPosition("C", new Vector3(88.1f, groundY, -372.8f), true);

        // 4. Gedung Kantor / Warehouse (Di sebelah kanan Blok A, sejajar sesuai Gambar 2)
        SpawnAccessoryAtPosition(nextAccessoryId++, "Building", new Vector3(166.8f, groundY, -484.9f), 0f, 1f);

        // Catatan: Gate, Top Loader, dan Reach Stacker tidak di-spawn lewat kode sesuai permintaan pengguna,
        // melainkan dapat ditambahkan secara manual melalui panel Yard Editor jika diinginkan.

        // Pastikan kontainer dimunculkan jika di luar mode editor
        if (!isEditorActive)
        {
            RefreshAllYardContainersVisibility();
        }

        Debug.Log("[YardEditor] Layout default 1Eagle berhasil dimuat: Blok A, B, C dan Gedung terpasang sesuai Gambar 2!");
    }

    // --- Update: Handle Input & 2D Camera ---

    void Update()
    {
        if (gameObject.scene.name == "SampleScene") return;
        if (!Application.isPlaying) return;

        if (!isEditorActive)
        {
            HandleContainerClickOutsideEditor();
            return;
        }

        // Pastikan selama mode Yard Editor aktif, kamera selalu berada di 2D Top-Down View
        Enforce2DViewDuringEditor();

        Camera cam = GetActiveCamera();
        if (cam == null) return;

        // Mouse Down — Selection / Start Drag
        if (Input.GetMouseButtonDown(0))
        {
            HandleMouseDown(cam);
        }

        // Mouse Drag — Object Drag
        if (Input.GetMouseButton(0) && isDraggingAnything)
        {
            HandleMouseDrag(cam);
        }

        // Mouse Up — End Drag
        if (Input.GetMouseButtonUp(0))
        {
            if (isDraggingAnything) HandleMouseUp();
            isPanning2DWithLeftMouse = false;
        }

        // 2D Top-Down Camera Pan & Zoom Controls
        HandleEditor2DCameraControls(cam);

        // Keyboard shortcuts
        if (Input.GetKeyDown(KeyCode.R))
        {
            ToggleSelectedOrientation();
        }

        if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
        {
            DeleteSelected();
        }
    }

    private void Enforce2DViewDuringEditor()
    {
        ViewModeManager vmm = ViewModeManager.Instance;
        if (vmm != null)
        {
            if (vmm.currentMode != CameraViewMode.TopDown2D)
            {
                vmm.SetViewMode(CameraViewMode.TopDown2D);
            }
            if (vmm.enabled)
            {
                vmm.enabled = false;
            }
        }
    }

    private void HandleEditor2DCameraControls(Camera cam)
    {
        if (cam == null) return;

        bool pointerOverUI = UnityEngine.EventSystems.EventSystem.current != null &&
                             UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        // Pan dengan Right-Drag, Middle-Drag, atau Left-Drag di tanah kosong (ketika tidak sedang drag objek)
        if (!isDraggingAnything && (Input.GetMouseButton(1) || Input.GetMouseButton(2) || (isPanning2DWithLeftMouse && Input.GetMouseButton(0))))
        {
            float panX = -Input.GetAxis("Mouse X") * editor2DOrthoSize * 0.05f;
            float panZ = -Input.GetAxis("Mouse Y") * editor2DOrthoSize * 0.05f;
            editor2DCamTargetPos += new Vector3(panX, 0f, panZ);
        }

        // Pan dengan WASD / Arrow Keys
        float moveX = Input.GetAxis("Horizontal") * editor2DOrthoSize * 0.9f * Time.deltaTime;
        float moveZ = Input.GetAxis("Vertical") * editor2DOrthoSize * 0.9f * Time.deltaTime;
        editor2DCamTargetPos += new Vector3(moveX, 0f, moveZ);

        // Zoom dengan ScrollWheel (hanya jika kursor tidak berada di atas panel ScrollView sidebar UI)
        if (!pointerOverUI && Input.mousePosition.x < Screen.width - 290f)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                editor2DOrthoSize = Mathf.Clamp(editor2DOrthoSize - scroll * 35f, 15f, 350f);
            }
        }

        cam.orthographic = true;
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, editor2DOrthoSize, Time.deltaTime * 12f);
        editor2DCamTargetPos.y = groundY + 180f;
        cam.transform.position = Vector3.Lerp(cam.transform.position, editor2DCamTargetPos, Time.deltaTime * 12f);
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private Vector3 containerMouseDownPos;
    private bool isPotentialContainerClick = false;

    private void HandleContainerClickOutsideEditor()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;
            if (VRPlayerController.IsPointerOverAnyUI())
                return;

            containerMouseDownPos = Input.mousePosition;
            isPotentialContainerClick = true;
        }

        if (Input.GetMouseButtonUp(0) && isPotentialContainerClick)
        {
            isPotentialContainerClick = false;

            // Jika mouse digeser lebih dari 6 pixel, berarti user sedang PAN / menggeser kamera dengan mouse, bukan klik kontainer!
            if (Vector3.Distance(Input.mousePosition, containerMouseDownPos) > 6f)
            {
                return;
            }

            Camera cam = GetActiveCamera();
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                Transform curr = hit.transform;
                while (curr != null)
                {
                    if (spawnedContainerInfo.TryGetValue(curr.gameObject, out ContainerLocData cData))
                    {
                        clickedContainerData = cData;
                        return;
                    }
                    curr = curr.parent;
                }
            }
            clickedContainerData = null;
        }
    }

    void HandleMouseDown(Camera cam)
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // Abaikan jika klik di area tombol sidebar kanan
        if (Input.mousePosition.x > Screen.width - 290f)
        {
            return;
        }

        Physics.SyncTransforms();

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 3000f, ~0, QueryTriggerInteraction.Collide);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            DraggableAccessory hitAcc = FindAccessoryFromHit(hit.transform);
            if (hitAcc != null)
            {
                DeselectAll();
                selectedAccessory = hitAcc;
                selectedAccessory.Select();

                Vector3 worldPoint = GetGroundPoint(cam);
                selectedAccessory.StartDrag(worldPoint);
                isDraggingAnything = true;
                return;
            }

            DraggableRoad hitRoad = FindRoadFromHit(hit.transform);
            if (hitRoad != null)
            {
                DeselectAll();
                selectedRoad = hitRoad;
                selectedRoad.Select();

                Vector3 worldPoint = GetGroundPoint(cam);
                selectedRoad.StartDrag(worldPoint);
                isDraggingAnything = true;
                return;
            }

            DraggableYard hitYard = FindYardFromHit(hit.transform);
            if (hitYard != null)
            {
                DeselectAll();
                selectedYard = hitYard;
                selectedYard.Select();

                Vector3 worldPoint = GetGroundPoint(cam);
                selectedYard.StartDrag(worldPoint);
                isDraggingAnything = true;
                return;
            }
        }

        // Fallback Geometris 2D di Ground Plane: Jika raycast collider meleset di Build/WebGL, cek koordinat X-Z langsung terhadap seluruh Road & Yard aktif
        Vector3 groundClickPt = GetGroundPoint(cam);
        for (int i = activeRoads.Count - 1; i >= 0; i--)
        {
            DraggableRoad r = activeRoads[i];
            if (r == null) continue;
            Vector3 localPt = Quaternion.Inverse(r.transform.rotation) * (groundClickPt - r.transform.position);
            if (Mathf.Abs(localPt.x) <= (r.roadWidth * 0.5f + 0.5f) && Mathf.Abs(localPt.z) <= (r.roadLength * 0.5f + 0.5f))
            {
                DeselectAll();
                selectedRoad = r;
                selectedRoad.Select();
                selectedRoad.StartDrag(groundClickPt);
                isDraggingAnything = true;
                return;
            }
        }

        for (int i = activeYards.Count - 1; i >= 0; i--)
        {
            DraggableYard y = activeYards[i];
            if (y == null) continue;
            Vector2 sz = CalculateYardSizeFromName(y.blockName);
            Transform plat = y.transform.Find("YardPlatform");
            if (plat != null) sz = new Vector2(plat.localScale.x, plat.localScale.z);

            Vector3 localPt = Quaternion.Inverse(y.transform.rotation) * (groundClickPt - y.transform.position);
            if (Mathf.Abs(localPt.x) <= (sz.x * 0.5f + 0.5f) && Mathf.Abs(localPt.z) <= (sz.y * 0.5f + 0.5f))
            {
                DeselectAll();
                selectedYard = y;
                selectedYard.Select();
                selectedYard.StartDrag(groundClickPt);
                isDraggingAnything = true;
                return;
            }
        }

        // Klik di tempat kosong -> Deselect & izinkan pan kamera 2D
        DeselectAll();
        isPanning2DWithLeftMouse = true;
    }

    void HandleMouseDrag(Camera cam)
    {
        Vector3 worldPoint = GetGroundPoint(cam);

        if (selectedYard != null && selectedYard.isDragging)
        {
            selectedYard.UpdateDrag(worldPoint);
        }
        else if (selectedRoad != null && selectedRoad.isDragging)
        {
            selectedRoad.UpdateDrag(worldPoint);
        }
        else if (selectedAccessory != null && selectedAccessory.isDragging)
        {
            selectedAccessory.UpdateDrag(worldPoint);
        }
    }

    void HandleMouseUp()
    {
        isDraggingAnything = false;

        if (selectedYard != null) selectedYard.EndDrag();
        if (selectedRoad != null) selectedRoad.EndDrag();
        if (selectedAccessory != null) selectedAccessory.EndDrag();
    }

    // --- Helpers ---

    DraggableYard FindYardFromHit(Transform hitTransform)
    {
        Transform current = hitTransform;
        while (current != null)
        {
            DraggableYard dy = current.GetComponent<DraggableYard>();
            if (dy != null) return dy;
            current = current.parent;
        }
        return null;
    }

    DraggableRoad FindRoadFromHit(Transform hitTransform)
    {
        Transform current = hitTransform;
        while (current != null)
        {
            DraggableRoad dr = current.GetComponent<DraggableRoad>();
            if (dr != null) return dr;
            current = current.parent;
        }
        return null;
    }

    DraggableAccessory FindAccessoryFromHit(Transform hitTransform)
    {
        Transform current = hitTransform;
        while (current != null)
        {
            DraggableAccessory da = current.GetComponent<DraggableAccessory>();
            if (da != null) return da;
            current = current.parent;
        }
        return null;
    }

    Vector3 GetGroundPoint(Camera cam)
    {
        if (groundY < 19.5f || groundY > 19.95f) groundY = 19.81f;
        float targetY = groundY;
        if (selectedYard != null) targetY = selectedYard.transform.position.y;
        else if (selectedRoad != null) targetY = selectedRoad.transform.position.y;
        else if (selectedAccessory != null) targetY = selectedAccessory.transform.position.y;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, targetY, 0));
        float distance;

        if (groundPlane.Raycast(ray, out distance))
        {
            Vector3 point = ray.GetPoint(distance);
            point.y = targetY;
            return point;
        }

        return new Vector3(0, targetY, 0);
    }

    public Vector3 GetCameraLookAtGroundPoint()
    {
        if (groundY < 19.5f || groundY > 19.95f) groundY = 19.81f;
        Camera cam = GetActiveCamera();
        Vector3 envCenter = GetEnvironmentCenter();

        if (cam != null)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.45f, 0.5f, 0));
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, groundY, 0));
            float distance;

            if (groundPlane.Raycast(ray, out distance))
            {
                Vector3 point = ray.GetPoint(distance);
                point.y = groundY;

                if (Vector3.Distance(new Vector3(point.x, 0, point.z), new Vector3(envCenter.x, 0, envCenter.z)) < 800f)
                {
                    return point;
                }
            }
        }

        return new Vector3(envCenter.x, groundY, envCenter.z);
    }

    public void EnsureOrbitCameraInstalled()
    {
        Camera cam = GetActiveCamera();
        if (cam != null && !isEditorActive)
        {
            OrbitCamera orbit = cam.GetComponent<OrbitCamera>();
            if (orbit == null)
            {
                orbit = cam.gameObject.AddComponent<OrbitCamera>();
                orbit.flySpeed = 50f;
                orbit.lookSensitivity = 2.5f;
                orbit.zoomSpeed = 15f;
            }
            orbit.enabled = true;
        }
    }

    public void FocusCameraOn(Vector3 targetPos)
    {
        Camera cam = GetActiveCamera();
        if (cam == null) return;

        if (isEditorActive)
        {
            // Mode 2D View saat editing yard
            editor2DCamTargetPos = new Vector3(targetPos.x, groundY + 180f, targetPos.z);
            cam.transform.position = editor2DCamTargetPos;
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            return;
        }

        EnsureOrbitCameraInstalled();
        OrbitCamera orbit = cam.GetComponent<OrbitCamera>();
        if (orbit != null)
        {
            orbit.FocusOnPosition(targetPos, 120f);
        }
        else
        {
            Vector3 newCamPos = targetPos + new Vector3(0, 120f, -140f);
            cam.transform.position = newCamPos;
            cam.transform.LookAt(targetPos);
        }
    }

    public void AdjustSelectedRoadWidth(float delta)
    {
        if (selectedRoad != null)
        {
            selectedRoad.AdjustWidth(delta);
        }
    }

    public void AdjustSelectedRoadLength(float delta)
    {
        if (selectedRoad != null)
        {
            selectedRoad.AdjustLength(delta);
        }
        else if (selectedAccessory != null && selectedAccessory.accessoryType == "Pagar")
        {
            selectedAccessory.AdjustLength(delta);
        }
    }

    Camera GetActiveCamera()
    {
        if (ViewModeManager.Instance != null)
        {
            if (isEditorActive && ViewModeManager.Instance.topDownCamera != null)
            {
                return ViewModeManager.Instance.topDownCamera;
            }
            Camera viewCam = ViewModeManager.Instance.GetActiveCamera();
            if (viewCam != null && viewCam.isActiveAndEnabled) return viewCam;
        }
        if (Camera.main != null && Camera.main.isActiveAndEnabled) return Camera.main;

        Camera anyCam = FindFirstObjectByType<Camera>();
        if (anyCam != null) return anyCam;

        return Camera.current;
    }

    // --- Public Getters ---

    public bool IsDraggingAnything() { return isEditorActive && isDraggingAnything; }

    public List<DraggableYard> GetActiveYards() { return activeYards; }
    public List<DraggableRoad> GetActiveRoads() { return activeRoads; }
    public List<DraggableAccessory> GetActiveAccessories() { return activeAccessories; }

    public bool IsYardSpawned(string blockName)
    {
        foreach (var y in activeYards)
        {
            if (y != null && y.blockName == blockName) return true;
        }
        return false;
    }

    void OnDestroy()
    {
        foreach (var kvp in yardMaterials)
        {
            if (kvp.Value != null)
            {
                if (Application.isPlaying) Destroy(kvp.Value);
                else DestroyImmediate(kvp.Value);
            }
        }
        yardMaterials.Clear();
    }
}
