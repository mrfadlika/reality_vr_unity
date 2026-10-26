using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// ============================================================
// YardEditorManager.cs
// Orchestrator utama: fetch API, toggle mode, manage state,
// handle drag-drop interaction, save/load layout
//
// FULLY DYNAMIC: Jumlah yard/block dari API, bisa berapapun
// Yard di-generate secara PROCEDURAL (tidak butuh prefab)
// ============================================================

public class YardEditorManager : MonoBehaviour
{
    public static YardEditorManager Instance { get; private set; }

    [Header("API")]
    public string masterBlockApiUrl = "https://apps.globalhalalhub.co.id/global-halal-api/api/vr/getMasterBlock";
    public string containerLocApiUrl = "https://apps.globalhalalhub.co.id/global-halal-api/api/vr/getCtrLoc";

    [Header("Editor State")]
    public bool isEditorActive = false;

    [Header("Settings")]
    [Tooltip("Auto-load saved layout saat scene start")]
    public bool autoLoadOnStart = true;

    [Header("Yard Generation (Real-World Meters)")]
    [Tooltip("Jarak total antar slot (meter). Standar = 6.1")]
    public float slotSpacing = 6.1f;
    [Tooltip("Jarak total antar baris (meter). Standar = 2.8")]
    public float rowSpacing = 2.8f;
    [Tooltip("Padding ekstra dari tepi yard (meter) untuk ruang marka, nomor slot/row & logo")]
    public float yardPadding = 5.0f;
    [Tooltip("Ketebalan yard block (tinggi Y)")]
    public float yardThickness = 1.8f;

    [Header("Yard Visual")]
    [Tooltip("Material untuk yard (opsional, jika kosong akan dibuat otomatis)")]
    public Material yardMaterial;

    [Header("Yard Prefabs")]
    [Tooltip("Master Prefab Yard jika menggunakan 1 prefab utama")]
    public GameObject masterYardPrefab;
    [Tooltip("Daftar Prefab Yard spesifik per blok (opsional)")]
    public GameObject[] yardPrefabs;

    // API data — DYNAMIC: bisa 1 yard, bisa 100 yard
    private List<MasterBlockData> masterBlocks = new List<MasterBlockData>();
    private List<ContainerLocData> containerLocList = new List<ContainerLocData>();
    private bool apiLoaded = false;

    // Active objects in scene
    private List<DraggableYard> activeYards = new List<DraggableYard>();
    private List<DraggableRoad> activeRoads = new List<DraggableRoad>();

    // Selection
    private DraggableYard selectedYard = null;
    private DraggableRoad selectedRoad = null;

    // Drag state
    private bool isDraggingAnything = false;
    public float groundY = 19.0f;

    // Road ID counter
    private int nextRoadId = 1;

    // UI reference
    private YardEditorUI editorUI;

    // Generated material cache
    private Dictionary<string, Material> yardMaterials = new Dictionary<string, Material>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        CleanupOrphanBingkaiKuning();
        FindGroundY();
        EnsureOrbitCameraInstalled();
        StartCoroutine(FetchAllApiData());

        // Auto-setup UI
        editorUI = GetComponent<YardEditorUI>();
        if (editorUI == null) editorUI = gameObject.AddComponent<YardEditorUI>();

        // Posisikan kamera ke vr-env jika kamera masih jauh / default
        Camera cam = GetActiveCamera();
        Vector3 envCenter = GetEnvironmentCenter();
        if (cam != null && (cam.transform.position == Vector3.zero || Vector3.Distance(cam.transform.position, envCenter) > 600f))
        {
            FocusCameraOn(envCenter);
        }

        if (autoLoadOnStart && YardEditorDataUtil.HasSavedLayout())
        {
            // Delay load agar API sempat fetch dulu
            StartCoroutine(DelayedLoad());
        }
        else
        {
            DetectExistingYardsAndRoads();
        }
    }

    public void DetectExistingYardsAndRoads()
    {
        // 1. Deteksi Yard di scene (YardA, YardB, YardC, gedung)
        string[] candidates = new string[] { "YardA", "YardB", "YardC", "gedung", "bawahnyaA", "bawahnyaB", "bawahnyaC", "bawahnyaGedung" };
        foreach (string name in candidates)
        {
            GameObject go = GameObject.Find(name);
            if (go != null)
            {
                Transform rootYard = go.transform;
                if (rootYard.parent != null && (rootYard.parent.name.StartsWith("Yard") || rootYard.parent.name == "gedung"))
                {
                    rootYard = rootYard.parent;
                }

                DraggableYard dy = rootYard.GetComponent<DraggableYard>();
                if (dy == null) dy = rootYard.gameObject.AddComponent<DraggableYard>();

                string cleanBlock = rootYard.name.Replace("Yard", "").ToUpper().Trim();
                if (rootYard.name.ToLower().Contains("gedung")) cleanBlock = "D";
                dy.blockName = cleanBlock;
                dy.isHorizontal = Mathf.Abs(rootYard.eulerAngles.y - 90f) < 45f || Mathf.Abs(rootYard.eulerAngles.y - 270f) < 45f;

                if (!activeYards.Contains(dy))
                {
                    activeYards.Add(dy);
                }
            }
        }

        // 2. Deteksi Jalan (Road) di scene
        DraggableRoad[] existingRoads = Object.FindObjectsByType<DraggableRoad>(FindObjectsSortMode.None);
        foreach (var r in existingRoads)
        {
            if (!activeRoads.Contains(r)) activeRoads.Add(r);
        }

        GameObject[] allGos = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in allGos)
        {
            if (go.name == "jln" || go.name.StartsWith("Road") || go.name.StartsWith("jln_"))
            {
                if (go.transform.parent != null && (go.transform.parent.name.StartsWith("Yard") || go.transform.parent.name == "gedung"))
                    continue;

                DraggableRoad dr = go.GetComponent<DraggableRoad>();
                if (dr == null) dr = go.AddComponent<DraggableRoad>();
                dr.roadId = nextRoadId++;
                if (!activeRoads.Contains(dr)) activeRoads.Add(dr);
            }
        }

        Debug.Log($"[YardEditor] Auto-detected {activeYards.Count} yards and {activeRoads.Count} roads from scene.");
        if (editorUI != null) editorUI.RebuildYardButtons();
    }

    public void CleanupOrphanBingkaiKuning()
    {
        // Bersihkan objek yatim BingkaiKuning di root Scene jika ada dari sisa-sisa lama
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
        // Tunggu API selesai fetch
        while (!apiLoaded) yield return null;
        LoadLayout();
        if (activeYards.Count == 0)
        {
            DetectExistingYardsAndRoads();
        }
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
        }

        // Raycast dari Y = +1000 ke bawah untuk mencari permukaan paling atas vr-env
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
        groundY = envPos.y + 0.2f; // Elevated slightly so 3D objects sit clearly on top of terrain
        Debug.Log("[YardEditor] Ground Y terdeteksi di: " + groundY + " (Posisi vr-env: " + envPos + ")");
    }

    // --- API Fetch ---

    IEnumerator FetchAllApiData()
    {
        yield return StartCoroutine(FetchMasterBlocksCoroutine());
        yield return StartCoroutine(FetchContainerLocationsCoroutine());

        apiLoaded = true;

        if (editorUI != null) editorUI.RebuildYardButtons();

        Debug.Log("[YardEditor] API loaded: " + masterBlocks.Count + " yards, " + containerLocList.Count + " container locations");
    }

    IEnumerator FetchMasterBlocksCoroutine()
    {
        string url = masterBlockApiUrl;
#if UNITY_WEBGL && !UNITY_EDITOR
        if (url.Contains("globalhalalhub.co.id")) url = "api/getMasterBlock";
#endif
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("X-Pinggy-No-Screen", "1");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string json = req.downloadHandler.text;
                ParseMasterBlocks(json);
            }
            else
            {
                Debug.LogError("[YardEditor] Gagal fetch getMasterBlock: " + req.error);
            }
        }
    }

    IEnumerator FetchContainerLocationsCoroutine()
    {
        string url = containerLocApiUrl;
#if UNITY_WEBGL && !UNITY_EDITOR
        if (url.Contains("globalhalalhub.co.id")) url = "api/getCtrLoc";
#endif
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("X-Pinggy-No-Screen", "1");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = req.downloadHandler.text;
                    string wrapped = "{\"data\":" + json + "}";
                    ContainerLocResponse resp = JsonUtility.FromJson<ContainerLocResponse>(wrapped);
                    if (resp != null && resp.data != null)
                    {
                        containerLocList = new List<ContainerLocData>(resp.data);
                        Debug.Log("[YardEditor] Loaded " + containerLocList.Count + " container locations from getCtrLoc API");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[YardEditor] getCtrLoc parse error: " + ex.Message);
                }
            }
        }
    }

    void ParseMasterBlocks(string json)
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

    // --- Public API for UI ---

    public List<MasterBlockData> GetMasterBlocks()
    {
        return masterBlocks;
    }

    public bool IsApiLoaded() { return apiLoaded; }

    public void ToggleEditor()
    {
        isEditorActive = !isEditorActive;

        if (!isEditorActive)
        {
            DeselectAll();
        }

        Debug.Log("[YardEditor] Editor mode: " + (isEditorActive ? "ACTIVE" : "INACTIVE"));
    }

    public void SetEditorActive(bool active)
    {
        isEditorActive = active;
        if (!active) DeselectAll();
    }

    // --- Yard Dimension Calculation ---

    /// <summary>
    /// Hitung koordinat awal (startX, startZ) untuk grid kontainer.
    /// Memberikan offset ruang aspal (8.5m di depan, 6.0m di kiri) agar nomor slot & row punya space luas.
    /// </summary>
    public Vector3 GetGridOriginOffset(int maxRow, int maxSlot)
    {
        float actualRowSpacing = rowSpacing;
        float actualSlotSpacing = slotSpacing;

        // Shift +1.5m ke kanan agar ada jalur aspal 6.0m di sebelah kiri (Barat)
        float startX = (-((maxRow - 1) * actualRowSpacing) / 2.0f) + 1.5f;

        // Shift +5.5m ke atas/Utara agar ada jalur aspal 9.5m di sebelah depan (Selatan)
        float startZ = (-(((maxSlot + 1) / 2.0f - 1) * actualSlotSpacing) / 2.0f) + 5.5f;

        return new Vector3(startX, 0, startZ);
    }

    /// <summary>
    /// Hitung ukuran yard (lebar × panjang) dari data master block.
    /// Lebar = YARD_ROW × rowSpacing + padding
    /// Panjang = (YARD_SLOT / 2) × slotSpacing + padding  (slot ganjil: 1,3,5...45 → 23 posisi)
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

        float width = (maxRow * rowSpacing) + 10.0f;
        float length = ((maxSlot + 1) / 2f * slotSpacing) + 16.0f;

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
        // Default jika tidak ditemukan di API
        return new Vector2(20f, 140f);
    }

    /// <summary>
    /// Cari MasterBlockData berdasarkan nama block
    /// </summary>
    public MasterBlockData GetMasterBlock(string blockName)
    {
        foreach (var mb in masterBlocks)
        {
            if (mb.YARD_BLOCK == blockName || mb.YARD_BLOCK_NAME == blockName)
                return mb;
        }
        return null;
    }

    // --- YARD PREFAB & GENERATION ---

    public GameObject GetYardPrefab(string blockName)
    {
        string cleanBlock = blockName.Replace("Yard", "").Replace("YARD", "").Trim().ToUpper();

        if (yardPrefabs != null)
        {
            foreach (var p in yardPrefabs)
            {
                if (p != null)
                {
                    string pName = p.name.ToUpper();
                    if (pName.Contains("YARD" + cleanBlock) || pName == cleanBlock || pName.Contains("YARD " + cleanBlock))
                        return p;
                }
            }
        }

#if UNITY_EDITOR
        // Utamakan prefab di folder "Assets/Yard Prefab/"
        string[] candidatePaths = new string[]
        {
            "Assets/Yard Prefab/Yard" + cleanBlock + ".prefab",
            "Assets/Yard Prefab/YardA.prefab",                  // Master template di folder Yard Prefab
            "Assets/Yard" + cleanBlock + ".prefab",
            "Assets/Yard" + cleanBlock + " Variant.prefab",
            "Assets/YardA Variant.prefab"
        };

        foreach (string path in candidatePaths)
        {
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;
        }
#endif

        if (masterYardPrefab != null) return masterYardPrefab;

        return null;
    }

    Transform FindBawahnyaChild(Transform parent)
    {
        foreach (Transform child in parent)
        {
            string cName = child.name.ToLower();
            if (cName.StartsWith("bawahnya") || cName.Contains("bawahnya"))
            {
                return child;
            }
        }
        foreach (Transform child in parent)
        {
            Transform found = FindBawahnyaChild(child);
            if (found != null) return found;
        }
        return null;
    }

    Bounds GetCombinedBounds(GameObject go)
    {
        Renderer[] rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(go.transform.position, new Vector3(20f, 5f, 140f));

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
        {
            if (rends[i] != null && rends[i].enabled)
                b.Encapsulate(rends[i].bounds);
        }
        return b;
    }

    void UpdatePrefabBlockName(GameObject yardGo, string blockName)
    {
        string cleanBlock = blockName.Replace("Yard", "").Replace("YARD", "").Trim().ToUpper();

        // 1. Rename child "bawahnya..." lantai beton di dalam prefab
        Transform floorTrans = FindBawahnyaChild(yardGo.transform);
        if (floorTrans != null)
        {
            floorTrans.name = "bawahnya" + cleanBlock;
        }

        // 2. Cari & ubah semua teks label blok di dalam prefab (TMPro / TextMesh)
        TMPro.TextMeshPro[] tmproList = yardGo.GetComponentsInChildren<TMPro.TextMeshPro>(true);
        foreach (var tmp in tmproList)
        {
            if (tmp != null)
            {
                if (tmp.gameObject.name.StartsWith("Text_") || tmp.text == "A" || (tmp.transform.parent != null && tmp.transform.parent.name.StartsWith("Label")))
                {
                    tmp.text = cleanBlock;
                    if (tmp.gameObject.name.StartsWith("Text_"))
                    {
                        tmp.gameObject.name = "Text_" + cleanBlock;
                    }
                }
            }
        }

        TextMesh[] tmList = yardGo.GetComponentsInChildren<TextMesh>(true);
        foreach (var tm in tmList)
        {
            if (tm != null && (tm.text == "A" || tm.gameObject.name.StartsWith("Text_")))
            {
                tm.text = cleanBlock;
                if (tm.gameObject.name.StartsWith("Text_"))
                {
                    tm.gameObject.name = "Text_" + cleanBlock;
                }
            }
        }

        // 4. Update YardBoundary script: Matikan tampilkanBingkaiKuning agar tidak buat BingkaiKuning yatim di root
        YardBoundary yb = yardGo.GetComponent<YardBoundary>();
        if (yb == null) yb = yardGo.AddComponent<YardBoundary>();
        yb.customBlockName = cleanBlock;
        yb.tampilkanBingkaiKuning = false; // Matikan agar tidak membuat objek BingkaiKuning yatim di Hierarchy
        yb.tampilkanLabelBlok = true;
        yb.BuatGarisBatas();
    }

    Transform FindYardInnerChild(Transform parent)
    {
        foreach (Transform child in parent)
        {
            string cName = child.name.ToLower();
            if ((cName.StartsWith("yard") || cName == "cube" || cName == "yardplatform") && !cName.Contains("editor"))
            {
                return child;
            }
        }
        foreach (Transform child in parent)
        {
            Transform found = FindYardInnerChild(child);
            if (found != null) return found;
        }
        return null;
    }

    GameObject GenerateYardProcedural(string blockName, Vector2 size)
    {
        GameObject prefab = GetYardPrefab(blockName);

        if (prefab != null)
        {
            GameObject yardGo = Instantiate(prefab);
            yardGo.name = "Yard" + blockName + "_Editor";

            // Update nama blok, child "bawahnya...", dan semua label teks di prefab secara otomatis lewat kode
            UpdatePrefabBlockName(yardGo, blockName);

            // 1. Objek Yard DALAM (1 pada gambar: YardA) tempat kontainer di-spawn
            Transform innerTrans = FindYardInnerChild(yardGo.transform);
            if (innerTrans == null) innerTrans = yardGo.transform;

            // 2. Objek BAWAHNYA LUAR (2 pada gambar: bawahnyaA) lantai tempat nomor row/slot & label diletakkan
            Transform bawahnyaTrans = FindBawahnyaChild(yardGo.transform);
            if (bawahnyaTrans == null) bawahnyaTrans = innerTrans;

            // 3. Hitung titik pojok (containerCorner) dari seluruh mesh lantai fisik Yard
            Renderer[] allYardRends = yardGo.GetComponentsInChildren<Renderer>();
            System.Collections.Generic.List<Renderer> floorRends = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer r in allYardRends)
            {
                if (r != null && !r.name.Contains("ft)") && !r.name.Contains("Container"))
                    floorRends.Add(r);
            }

            Vector3 containerCorner = yardGo.transform.position;
            float innerMaxY = yardGo.transform.position.y;

            if (floorRends.Count > 0)
            {
                Bounds yb = floorRends[0].bounds;
                for (int i = 1; i < floorRends.Count; i++) yb.Encapsulate(floorRends[i].bounds);
                containerCorner = new Vector3(yb.min.x, yb.max.y, yb.min.z);
                innerMaxY = yb.max.y;
            }

            // Margin offset 1.0m right + 1.0m forward persis dari pojok Yard
            Vector3 margin = (innerTrans.right * 1.0f) + (innerTrans.forward * 1.0f);
            containerCorner += margin;

            Renderer bawahnyaRend = bawahnyaTrans.GetComponent<Renderer>();
            float bawahnyaMaxY = bawahnyaRend != null ? bawahnyaRend.bounds.max.y : innerMaxY;

            // 4. Spawn 3D Container di atas permukaan YardA (1)
            GenerateContainersOnFloor(yardGo.transform, innerTrans, blockName, containerCorner, innerMaxY);

            // 5. Gambar Penomoran Slot (001..045) dan Row (R1..R6) melayang di atas permukaan bawahnyaA (2)
            AddFloorNumberingOnPrefab(yardGo.transform, innerTrans, bawahnyaTrans, blockName, containerCorner, bawahnyaMaxY);

            // Hapus BoxCollider di root object agar raycast click langsung mengenai mesh fisik child (bawahnya, jln, kontainer)
            BoxCollider rootCol = yardGo.GetComponent<BoxCollider>();
            if (rootCol != null) DestroyImmediate(rootCol);

            return yardGo;
        }

        // Fallback procedural jika prefab tidak ditemukan
        return GenerateYardPureProcedural(blockName, size);
    }

    GameObject GenerateYardPureProcedural(string blockName, Vector2 size)
    {
        // Root object
        GameObject yardGo = new GameObject("Yard" + blockName + "_Editor");

        // 1. Platform Base Slab (Lantai beton tebal 1.5m agar tidak tenggelam di lantai vr-env)
        float slabThickness = 1.5f;
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "YardPlatform";
        platform.transform.SetParent(yardGo.transform);
        platform.transform.localPosition = new Vector3(0, slabThickness / 2f, 0);
        platform.transform.localRotation = Quaternion.identity;
        platform.transform.localScale = new Vector3(size.x, slabThickness, size.y);

        Material mat = GetOrCreateYardMaterial(blockName);
        Renderer rend = platform.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = mat;

        // 2. Marka Jalan (Yellow Industrial Safety Border Frame seperti di SampleScene)
        AddYardMarkingBorder(yardGo.transform, size, slabThickness);

        // 3. Penomoran Slot (001, 003...), Row (R1..R6), dan Kotak Nama Blok (seperti di SampleScene)
        AddYardFloorNumberingAndLabels(yardGo.transform, blockName, size, slabThickness);

        // 4. Generate 3D Container Stacks dari getCtrLoc API!
        GenerateContainerStacksFromApi(yardGo.transform, blockName, size, slabThickness);

        // 5. Tambahkan BoxCollider utama di root object agar raycast click 100% kena
        BoxCollider rootCol = yardGo.AddComponent<BoxCollider>();
        rootCol.center = new Vector3(0, 5f, 0);
        rootCol.size = new Vector3(size.x, 12f, size.y);

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

        GameObject numGroup = new GameObject("YardNumberingAndLabels");
        numGroup.transform.SetParent(parent);
        numGroup.transform.localPosition = Vector3.zero;
        numGroup.transform.localRotation = Quaternion.identity;

        float textY = heightY + 0.12f; // Melayang presisi di atas permukaan aspal beton (tinggi slab = 1.5m, top = 1.5m + 0.12m = 1.62m)

        Vector3 gridOrigin = GetGridOriginOffset(maxRow, maxSlot);
        float startX = gridOrigin.x;
        float startZ = gridOrigin.z;

        float actualRowSpacing = rowSpacing;
        float actualSlotSpacing = slotSpacing;

        // 1. SLOT NUMBERS (001, 003, 005, ... 045)
        // Posisi X di tengah-tengah jalur aspal sebelah barat (kiri), aman dari kontainer & garis kuning
        float xPosSlotLine = startX - 3.8f;

        for (int s = 1; s <= maxSlot; s += 2)
        {
            float slotIndex = (s - 1) / 2.0f;
            float zPos = startZ + (slotIndex * actualSlotSpacing);

            Vector3 localPos = new Vector3(xPosSlotLine, textY, zPos);
            // Rotasi (90, 90, 0) agar teks dibaca dari samping (seperti SampleScene)
            Quaternion rot = Quaternion.Euler(90, 90, 0);
            Create3DFloorText(numGroup.transform, localPos, rot, s.ToString("000"), 10f, Color.white);
        }

        // 2. ROW NUMBERS (R1, R2, R3, R4, R5, R6)
        // Posisi Z di jalur aspal depan (selatan), berjarak 7.5 meter di depan center Slot 001 (tidak tertutup kontainer 40ft)
        float zPosRowLine = startZ - 7.5f;

        for (int r = 1; r <= maxRow; r++)
        {
            float rowIndex = r - 1;
            float posX = startX + (rowIndex * actualRowSpacing);

            Vector3 localPos = new Vector3(posX, textY, zPosRowLine);
            // Rotasi (90, 0, 0) agar teks menghadap ke Utara (horisontal)
            Quaternion rot = Quaternion.Euler(90, 0, 0);
            Create3DFloorText(numGroup.transform, localPos, rot, "R" + r, 11f, Color.white);
        }

        // 3. KOTAK KUNING NAMA BLOK ([ A ], [ B ], [ C ], dst)
        string letter = blockName.Replace("Yard", "").Replace("YARD", "").Trim().ToUpper();
        if (string.IsNullOrEmpty(letter)) letter = blockName.ToUpper();

        float halfWidth = yardSize.x / 2f;
        float halfLength = yardSize.y / 2f;

        // Sudut Tenggara (Selatan-Timur) — berada aman di dalam batas lantai beton tanpa menyentuh garis kuning luar
        Vector3 labelBoxPosSE = new Vector3(halfWidth - 2.8f, textY, -halfLength + 3.0f);
        CreateYellowBlockIconBox(numGroup.transform, labelBoxPosSE, letter);

        // Sudut Barat Laut (Utara-Barat) — berada aman di dalam batas lantai beton tanpa menyentuh garis kuning luar
        Vector3 labelBoxPosNW = new Vector3(-halfWidth + 3.8f, textY, halfLength - 3.0f);
        CreateYellowBlockIconBox(numGroup.transform, labelBoxPosNW, letter);
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

        float borderThick = 0.5f;
        float posY = heightY + 0.05f;
        float halfX = yardSize.x / 2f;
        float halfZ = yardSize.y / 2f;

        CreateBorderBar(borderGroup.transform, new Vector3(0, posY, halfZ), new Vector3(yardSize.x, 0.05f, borderThick), yellowMat);
        CreateBorderBar(borderGroup.transform, new Vector3(0, posY, -halfZ), new Vector3(yardSize.x, 0.05f, borderThick), yellowMat);
        CreateBorderBar(borderGroup.transform, new Vector3(-halfX, posY, 0), new Vector3(borderThick, 0.05f, yardSize.y), yellowMat);
        CreateBorderBar(borderGroup.transform, new Vector3(halfX, posY, 0), new Vector3(borderThick, 0.05f, yardSize.y), yellowMat);
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

    void GenerateContainerStacksFromApi(Transform parent, string blockName, Vector2 yardSize, float startY)
    {
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

        GameObject containerGroup = new GameObject("ContainersFromAPI");
        containerGroup.transform.SetParent(parent);
        containerGroup.transform.localPosition = Vector3.zero;

        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Unlit/Color");
        if (s == null) s = Shader.Find("Sprites/Default");
        if (s == null) s = Shader.Find("Standard");

        if (containersForBlock.Count > 0)
        {
            Debug.Log("[YardEditor] Spawning " + containersForBlock.Count + " kontainer dari getCtrLoc API untuk Yard " + blockName);

            float actualRowSpacing = rowSpacing;
            float actualSlotSpacing = slotSpacing;

            Vector3 gridOrigin = GetGridOriginOffset(maxRow, maxSlot);
            float startX = gridOrigin.x;
            float startZ = gridOrigin.z;

            foreach (var c in containersForBlock)
            {
                int r = 1, sl = 1, t = 1;
                int.TryParse(c.YARD_ROW, out r);
                int.TryParse(c.YARD_SLOT, out sl);
                int.TryParse(c.YARD_TIER, out t);

                if (r > 6 || r < 1) continue;

                float slotIndex = (sl - 1) / 2.0f;
                float rowIndex = r - 1;
                float tierIndex = t - 1;

                bool is40ft = c.CTR_SIZE == "40";
                float containerLength = is40ft ? 12.19f : 6.06f;
                float containerWidth = 2.44f;
                float containerHeight = 2.59f;

                float posX = startX + (rowIndex * actualRowSpacing);
                float posZ = startZ + (slotIndex * actualSlotSpacing);
                float posY = startY + (tierIndex * 2.6f) + (containerHeight / 2.0f);

                // Coba ambil PREFAB kontainer asli dari project (Assets/PrefabContainer/*.prefab)
                GameObject prefab = GetContainerPrefab(c.AGENT, c.CTR_MERK, is40ft);
                GameObject cBox;

                if (prefab != null)
                {
                    cBox = Instantiate(prefab);
                    cBox.name = (is40ft ? "40ft_" : "20ft_") + (string.IsNullOrEmpty(c.CTR_NO) ? "Container" : c.CTR_NO);
                    cBox.transform.SetParent(containerGroup.transform);
                    cBox.transform.localPosition = new Vector3(posX, posY, posZ);
                    cBox.transform.localRotation = Quaternion.identity;

                    // Fit scale otomatis agar ukuran fisik kontainer sesuai 2.44m x 2.59m x 6.06m/12.19m (persis seperti SampleScene)
                    Vector3 standardSize = is40ft ? new Vector3(2.44f, 2.59f, 12.19f) : new Vector3(2.44f, 2.59f, 6.06f);
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
                }
                else
                {
                    cBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cBox.name = (is40ft ? "40ft_" : "20ft_") + (string.IsNullOrEmpty(c.CTR_NO) ? "Container" : c.CTR_NO);
                    cBox.transform.SetParent(containerGroup.transform);
                    cBox.transform.localPosition = new Vector3(posX, posY, posZ);
                    cBox.transform.localScale = new Vector3(containerWidth, containerHeight, containerLength);

                    Color cColor = GetAgentColor(c.AGENT, c.CTR_MERK);
                    Material cMat = new Material(s);
                    cMat.color = cColor;
                    if (cMat.HasProperty("_BaseColor")) cMat.SetColor("_BaseColor", cColor);
                    if (cMat.HasProperty("_Color")) cMat.SetColor("_Color", cColor);

                    Renderer cRend = cBox.GetComponent<Renderer>();
                    if (cRend != null) cRend.sharedMaterial = cMat;

                    Collider col = cBox.GetComponent<Collider>();
                    if (col != null) DestroyImmediate(col);
                }
            }
        }
        else
        {
            GenerateContainerStacksFallback(containerGroup.transform, maxRow, maxSlot, startY, s);
        }
    }

    void GenerateContainersOnFloor(Transform parent, Transform floorTrans, string blockName, Vector3 yardCorner, float floorMaxY)
    {
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

        GameObject containerGroup = new GameObject("ContainersFromAPI");
        containerGroup.transform.SetParent(parent, false);

        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Unlit/Color");
        if (s == null) s = Shader.Find("Sprites/Default");
        if (s == null) s = Shader.Find("Standard");

        if (containersForBlock.Count > 0)
        {
            Debug.Log("[YardEditor] Spawning " + containersForBlock.Count + " kontainer dari API di atas " + floorTrans.name);

            float actualRowSpacing = rowSpacing;
            float actualSlotSpacing = slotSpacing;

            foreach (var c in containersForBlock)
            {
                int r = 1, sl = 1, t = 1;
                int.TryParse(c.YARD_ROW, out r);
                int.TryParse(c.YARD_SLOT, out sl);
                int.TryParse(c.YARD_TIER, out t);

                if (r > 6 || r < 1) continue;

                float slotIndex = (sl - 1) / 2.0f;
                float rowIndex = r - 1;
                float tierIndex = t - 1;

                bool is40ft = c.CTR_SIZE == "40";
                float containerLength = is40ft ? 12.19f : 6.06f;
                float containerWidth = 2.44f;
                float containerHeight = 2.59f;

                float xPos = rowIndex * actualRowSpacing;
                float zPos = slotIndex * actualSlotSpacing;

                Vector3 targetMinPos = yardCorner
                                     + (floorTrans.right * xPos)
                                     + (floorTrans.forward * zPos);

                GameObject prefab = GetContainerPrefab(c.AGENT, c.CTR_MERK, is40ft);
                GameObject cBox;

                if (prefab != null)
                {
                    cBox = Instantiate(prefab);
                    cBox.name = (is40ft ? "40ft_" : "20ft_") + (string.IsNullOrEmpty(c.CTR_NO) ? "Container" : c.CTR_NO);
                    cBox.transform.SetParent(containerGroup.transform, false);

                    // Fit scale otomatis sesuai ukuran asli (2.44m x 2.59m x 6.06m/12.19m)
                    Vector3 standardSize = is40ft ? new Vector3(containerWidth, containerHeight, 12.19f) : new Vector3(containerWidth, containerHeight, 6.06f);
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
                            if (currentSize.x > currentSize.z) { float tmp = scaleX; scaleX = scaleZ; scaleZ = tmp; }
                            cBox.transform.localScale = new Vector3(cBox.transform.localScale.x * scaleX, cBox.transform.localScale.y * scaleY, cBox.transform.localScale.z * scaleZ);
                        }
                    }

                    Renderer[] rends = cBox.GetComponentsInChildren<Renderer>();
                    Vector3 pivotOffset = Vector3.zero;
                    if (rends.Length > 0)
                    {
                        Bounds cb = rends[0].bounds;
                        for (int i = 1; i < rends.Length; i++) cb.Encapsulate(rends[i].bounds);
                        pivotOffset = cb.min - cBox.transform.position;
                    }

                    Vector3 finalPos = targetMinPos 
                                     - (floorTrans.right * pivotOffset.x)
                                     - (floorTrans.up * pivotOffset.y)
                                     - (floorTrans.forward * pivotOffset.z);
                    finalPos.y = floorMaxY + (tierIndex * 2.6f);

                    cBox.transform.position = finalPos;
                    cBox.transform.rotation = floorTrans.rotation;

                    // Decal & cleanup
                    foreach (Camera cam in cBox.GetComponentsInChildren<Camera>()) Destroy(cam.gameObject);
                    foreach (Light l in cBox.GetComponentsInChildren<Light>()) Destroy(l.gameObject);
                    foreach (Behaviour comp in cBox.GetComponentsInChildren<Behaviour>())
                    {
                        if (comp.GetType().Name == "DecalProjector")
                        {
                            bool prevState = comp.enabled;
                            comp.enabled = false;
                            comp.enabled = true;
                            comp.enabled = prevState;
                        }
                    }

                    // BoxCollider untuk raycast click
                    foreach (MeshRenderer mr in cBox.GetComponentsInChildren<MeshRenderer>())
                    {
                        if (mr.gameObject.GetComponent<Collider>() == null)
                        {
                            mr.gameObject.AddComponent<BoxCollider>();
                        }
                    }
                }
                else
                {
                    cBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cBox.name = (is40ft ? "40ft_" : "20ft_") + (string.IsNullOrEmpty(c.CTR_NO) ? "Container" : c.CTR_NO);
                    cBox.transform.localScale = new Vector3(containerWidth, containerHeight, containerLength);

                    cBox.transform.SetParent(containerGroup.transform, false);

                    Vector3 finalPos = targetMinPos;
                    finalPos.y = floorMaxY + (tierIndex * 2.6f) + (containerHeight / 2.0f);

                    cBox.transform.position = finalPos;
                    cBox.transform.rotation = floorTrans.rotation;

                    Color cColor = GetAgentColor(c.AGENT, c.CTR_MERK);
                    Material cMat = new Material(s);
                    cMat.color = cColor;
                    if (cMat.HasProperty("_BaseColor")) cMat.SetColor("_BaseColor", cColor);
                    if (cMat.HasProperty("_Color")) cMat.SetColor("_Color", cColor);

                    Renderer cRend = cBox.GetComponent<Renderer>();
                    if (cRend != null) cRend.sharedMaterial = cMat;
                }
            }
        }
        else
        {
            GenerateContainerStacksFallbackOnFloor(containerGroup.transform, floorTrans, yardCorner, maxRow, maxSlot, floorMaxY, s);
        }
    }

    void AddFloorNumberingOnPrefab(Transform parent, Transform innerTrans, Transform bawahnyaTrans, string blockName, Vector3 yardCorner, float floorMaxY)
    {
        MasterBlockData mb = GetMasterBlock(blockName);
        int maxRow = 6, maxSlot = 45;
        if (mb != null)
        {
            int.TryParse(mb.YARD_ROW, out maxRow);
            int.TryParse(mb.YARD_SLOT, out maxSlot);
        }

        GameObject numGroup = new GameObject("YardNumberingAndLabels");
        numGroup.transform.SetParent(parent, false);

        float textY = floorMaxY + 0.08f;
        Color numColor = Color.white;

        // 1. SLOT NUMBERS (001, 003, ..., 045) — Di sebelah kiri YardA di atas lantai bawahnyaA
        for (int s = 1; s <= maxSlot; s += 2)
        {
            float slotIndex = (s - 1) / 2.0f;
            float zPos = slotIndex * slotSpacing;
            float zCenter = zPos + 3.03f;
            float xPosSlotLine = -3.8f;

            Vector3 pos = yardCorner
                         + (innerTrans.right * xPosSlotLine)
                         + (innerTrans.forward * zCenter);
            pos.y = textY;

            Quaternion rot = innerTrans.rotation * Quaternion.Euler(90, 90, 0);
            Create3DFloorText(numGroup.transform, pos, rot, s.ToString("000"), 10f, numColor);
        }

        // 2. ROW NUMBERS (R1, R2, ..., R6) — Di sebelah depan YardA di atas lantai bawahnyaA
        for (int r = 1; r <= maxRow; r++)
        {
            float rowIndex = r - 1;
            float xPos = rowIndex * rowSpacing;
            float xCenter = xPos + 1.22f;
            float zPosRowLine = -4.0f;

            Vector3 pos = yardCorner
                         + (innerTrans.right * xCenter)
                         + (innerTrans.forward * zPosRowLine);
            pos.y = textY;

            Quaternion rot = innerTrans.rotation * Quaternion.Euler(90, 0, 0);
            Create3DFloorText(numGroup.transform, pos, rot, "R" + r, 11f, numColor);
        }
    }

    void GenerateContainerStacksFallbackOnFloor(Transform parent, Transform floorTrans, Vector3 yardCorner, int maxRow, int maxSlot, float floorMaxY, Shader s)
    {
        maxRow = Mathf.Clamp(maxRow, 2, 6);
        maxSlot = Mathf.Clamp(maxSlot, 4, 16);

        float containerWidth = 2.44f;
        float containerLength = 6.06f;
        float containerHeight = 2.59f;

        Color[] colors = new Color[]
        {
            new Color(0.82f, 0.62f, 0.15f, 1f),
            new Color(0.85f, 0.22f, 0.2f, 1f),
            new Color(0.18f, 0.45f, 0.85f, 1f),
            new Color(0.2f, 0.68f, 0.3f, 1f),
            new Color(0.92f, 0.52f, 0.12f, 1f)
        };

        for (int r = 0; r < maxRow; r++)
        {
            for (int sl = 0; sl < maxSlot / 2; sl++)
            {
                int stackHeight = ((r + sl) % 3) + 1;

                for (int t = 0; t < stackHeight; t++)
                {
                    GameObject cBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cBox.name = "DemoContainer_R" + r + "_S" + sl + "_T" + t + " (20ft)";
                    cBox.transform.SetParent(parent, false);

                    float posX = r * rowSpacing;
                    float posZ = sl * slotSpacing;
                    float posY = floorMaxY + (t * 2.6f) + (containerHeight / 2f);

                    Vector3 targetPos = yardCorner 
                                      + (floorTrans.right * (posX + containerWidth / 2f)) 
                                      + (floorTrans.forward * (posZ + containerLength / 2f));
                    targetPos.y = posY;

                    cBox.transform.position = targetPos;
                    cBox.transform.rotation = floorTrans.rotation;
                    cBox.transform.localScale = new Vector3(containerWidth, containerHeight, containerLength);

                    Color cColor = colors[(r + sl + t) % colors.Length];
                    Material cMat = new Material(s);
                    cMat.color = cColor;
                    if (cMat.HasProperty("_BaseColor")) cMat.SetColor("_BaseColor", cColor);
                    if (cMat.HasProperty("_Color")) cMat.SetColor("_Color", cColor);

                    Renderer cRend = cBox.GetComponent<Renderer>();
                    if (cRend != null) cRend.sharedMaterial = cMat;

                    Collider col = cBox.GetComponent<Collider>();
                    if (col != null) DestroyImmediate(col);
                }
            }
        }
    }


    GameObject GetContainerPrefab(string agent, string merk, bool is40ft)
    {
        string a = (agent ?? "").ToLower().Trim();
        string m = (merk ?? "").ToLower().Trim();
        string combined = a + " " + m;
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

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(targetName))
        {
            string path = "Assets/PrefabContainer/" + targetName + ".prefab";
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;
        }

        string defaultPath = is40ft ? "Assets/40feet.prefab" : "Assets/20feet.prefab";
        GameObject defaultPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(defaultPath);
        if (defaultPrefab != null) return defaultPrefab;
#endif

        return null;
    }

    Color GetAgentColor(string agent, string merk)
    {
        string a = (agent ?? "").ToLower().Trim();
        string m = (merk ?? "").ToLower().Trim();

        if (a.Contains("msc") || m.Contains("msc")) return new Color(0.82f, 0.62f, 0.15f, 1f); // Yellowish Brown MSC
        if (a.Contains("ck") || m.Contains("ck")) return new Color(0.85f, 0.22f, 0.2f, 1f);   // Crimson Red CK
        if (a.Contains("sls") || m.Contains("sls")) return new Color(0.18f, 0.45f, 0.85f, 1f); // Royal Blue SLS
        if (a.Contains("sks") || m.Contains("sks")) return new Color(0.2f, 0.68f, 0.3f, 1f);   // Forest Green SKS
        if (a.Contains("sbs") || m.Contains("sbs")) return new Color(0.92f, 0.52f, 0.12f, 1f); // Orange SBS
        if (a.Contains("whl") || m.Contains("whl")) return new Color(0.12f, 0.25f, 0.52f, 1f); // Dark Navy WHL

        return new Color(0.35f, 0.55f, 0.75f, 1f); // Default Steel Blue
    }

    void GenerateContainerStacksFallback(Transform parent, int maxRow, int maxSlot, float startY, Shader s)
    {
        maxRow = Mathf.Clamp(maxRow, 2, 6);
        maxSlot = Mathf.Clamp(maxSlot, 4, 16);

        float containerWidth = 2.44f;
        float containerLength = 6.06f;
        float containerHeight = 2.59f;

        float startX = -((maxRow - 1) * rowSpacing) / 2f;
        float startZ = -((maxSlot / 2 - 1) * slotSpacing) / 2f;

        Color[] colors = new Color[]
        {
            new Color(0.82f, 0.62f, 0.15f, 1f), // MSC
            new Color(0.85f, 0.22f, 0.2f, 1f),  // CK
            new Color(0.18f, 0.45f, 0.85f, 1f), // SLS
            new Color(0.2f, 0.68f, 0.3f, 1f),  // SKS
            new Color(0.92f, 0.52f, 0.12f, 1f)  // SBS
        };

        for (int r = 0; r < maxRow; r++)
        {
            for (int sl = 0; sl < maxSlot / 2; sl++)
            {
                int stackHeight = ((r + sl) % 3) + 1;

                for (int t = 0; t < stackHeight; t++)
                {
                    GameObject cBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cBox.name = "DemoContainer_R" + r + "_S" + sl + "_T" + t;
                    cBox.transform.SetParent(parent);

                    float posX = startX + r * rowSpacing;
                    float posZ = startZ + sl * slotSpacing;
                    float posY = startY + (t * 2.6f) + (containerHeight / 2f);

                    cBox.transform.localPosition = new Vector3(posX, posY, posZ);
                    cBox.transform.localScale = new Vector3(containerWidth, containerHeight, containerLength);

                    Color cColor = colors[(r + sl + t) % colors.Length];
                    Material cMat = new Material(s);
                    cMat.color = cColor;
                    if (cMat.HasProperty("_BaseColor")) cMat.SetColor("_BaseColor", cColor);
                    if (cMat.HasProperty("_Color")) cMat.SetColor("_Color", cColor);

                    Renderer cRend = cBox.GetComponent<Renderer>();
                    if (cRend != null) cRend.sharedMaterial = cMat;

                    Collider col = cBox.GetComponent<Collider>();
                    if (col != null) DestroyImmediate(col);
                }
            }
        }
    }

    Material GetOrCreateYardMaterial(string blockName)
    {
        // Jika user menyediakan material custom, gunakan itu
        if (yardMaterial != null) return yardMaterial;

        // Cache per block name
        if (yardMaterials.ContainsKey(blockName) && yardMaterials[blockName] != null)
            return yardMaterials[blockName];

        // Generate warna unik berdasarkan nama block (deterministic hash)
        Color yardColor = GenerateBlockColor(blockName);

        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");

        Material mat = new Material(s);
        mat.color = yardColor;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", yardColor);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
        mat.name = "YardMat_" + blockName;

        yardMaterials[blockName] = mat;
        return mat;
    }

    /// <summary>
    /// Generate warna unik & deterministic dari nama block.
    /// Menggunakan HSV dengan hue yang di-hash dari string agar selalu konsisten.
    /// </summary>
    Color GenerateBlockColor(string blockName)
    {
        // Hash sederhana dari string → hue
        int hash = 0;
        foreach (char c in blockName)
        {
            hash = hash * 31 + c;
        }
        hash = Mathf.Abs(hash);

        float hue = (hash % 360) / 360f;
        float saturation = 0.25f;  // Desaturated agar terlihat seperti beton
        float value = 0.45f;       // Gelap agar terlihat seperti aspal

        return Color.HSVToRGB(hue, saturation, value);
    }

    // --- Spawn Yard ---

    // --- Spawn Yard ---

    public void SpawnYard(string blockName)
    {
        // Cek apakah yard ini sudah ada di scene
        DraggableYard existing = activeYards.Find(y => y != null && y.blockName == blockName);
        if (existing != null)
        {
            // Sudah ada! Pilih yard tersebut dan fokuskan kamera ke sana
            DeselectAll();
            selectedYard = existing;
            selectedYard.Select();
            FocusCameraOn(existing.transform.position);
            Debug.Log("[YardEditor] Yard " + blockName + " sudah ada. Kamera difokuskan ke posisinya.");
            return;
        }

        // Hitung ukuran dari API data
        Vector2 size = CalculateYardSizeFromName(blockName);

        // Spawn di titik tanah yang sedang dilihat oleh Kamera
        Vector3 spawnPos = GetCameraLookAtGroundPoint();

        // Generate procedural
        GameObject yardGo = GenerateYardProcedural(blockName, size);
        yardGo.transform.position = spawnPos;

        // Tambahkan komponen DraggableYard
        DraggableYard draggable = yardGo.AddComponent<DraggableYard>();
        draggable.blockName = blockName;

        // Simpan info ukuran dari API
        MasterBlockData mb = GetMasterBlock(blockName);
        if (mb != null)
        {
            draggable.maxRow = int.TryParse(mb.YARD_ROW, out int r) ? r : 6;
            draggable.maxSlot = int.TryParse(mb.YARD_SLOT, out int s) ? s : 45;
            draggable.maxTier = int.TryParse(mb.YARD_TIER, out int t) ? t : 7;
        }

        activeYards.Add(draggable);

        // Auto select & fokus kamera
        DeselectAll();
        selectedYard = draggable;
        selectedYard.Select();
        FocusCameraOn(spawnPos);

        Debug.Log("[YardEditor] Yard " + blockName + " spawned at " + spawnPos + "! Size: " + size.x + " x " + size.y + " meters");
    }

    public void SpawnYardAtPosition(string blockName, Vector3 position, bool isHorizontal, float customWidth = 0, float customLength = 0)
    {
        // Cek duplikat
        foreach (var y in activeYards)
        {
            if (y != null && y.blockName == blockName) return;
        }

        // Hitung ukuran
        Vector2 size;
        if (customWidth > 0 && customLength > 0)
            size = new Vector2(customWidth, customLength);
        else
            size = CalculateYardSizeFromName(blockName);

        // Generate procedural
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
        Vector3 spawnPos = GetCameraLookAtGroundPoint();

        GameObject roadGo = new GameObject("Road_" + nextRoadId);
        roadGo.transform.position = spawnPos;

        DraggableRoad road = roadGo.AddComponent<DraggableRoad>();
        road.roadId = nextRoadId;

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
        GameObject roadGo = new GameObject("Road_" + id);
        roadGo.transform.position = position;

        DraggableRoad road = roadGo.AddComponent<DraggableRoad>();
        road.roadId = id;
        road.roadWidth = sX;
        road.roadLength = sZ;
        road.SetDirection(rotY);

        activeRoads.Add(road);
        if (id >= nextRoadId) nextRoadId = id + 1;
    }

    // --- Delete ---

    public void DeleteSelected()
    {
        if (selectedYard != null)
        {
            activeYards.Remove(selectedYard);
            Destroy(selectedYard.gameObject);
            selectedYard = null;
            Debug.Log("[YardEditor] Yard dihapus!");
        }
        else if (selectedRoad != null)
        {
            activeRoads.Remove(selectedRoad);
            Destroy(selectedRoad.gameObject);
            selectedRoad = null;
            Debug.Log("[YardEditor] Road dihapus!");
        }
    }

    // --- Clear All ---

    public void ClearAll()
    {
        foreach (var y in activeYards)
        {
            if (y != null) Destroy(y.gameObject);
        }
        activeYards.Clear();

        foreach (var r in activeRoads)
        {
            if (r != null) Destroy(r.gameObject);
        }
        activeRoads.Clear();

        selectedYard = null;
        selectedRoad = null;
        nextRoadId = 1;

        Debug.Log("[YardEditor] Semua objek dihapus dari scene!");
    }

    // --- Selection ---

    void DeselectAll()
    {
        if (selectedYard != null)
        {
            selectedYard.Deselect();
            selectedYard = null;
        }
        if (selectedRoad != null)
        {
            selectedRoad.Deselect();
            selectedRoad = null;
        }
    }

    public DraggableYard GetSelectedYard() { return selectedYard; }
    public DraggableRoad GetSelectedRoad() { return selectedRoad; }

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

        YardEditorDataUtil.SaveLayout(layout);
    }

    public void LoadLayout()
    {
        ClearAll();

        YardLayout layout = YardEditorDataUtil.LoadLayout();

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
    }

    // --- Update: Handle Input ---

    void Update()
    {
        if (!isEditorActive) return;

        Camera cam = GetActiveCamera();
        if (cam == null) return;

        // Mouse Down — Selection / Start Drag
        if (Input.GetMouseButtonDown(0))
        {
            HandleMouseDown(cam);
        }

        // Mouse Drag
        if (Input.GetMouseButton(0) && isDraggingAnything)
        {
            HandleMouseDrag(cam);
        }

        // Mouse Up — End Drag
        if (Input.GetMouseButtonUp(0) && isDraggingAnything)
        {
            HandleMouseUp();
        }

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

    void HandleMouseDown(Camera cam)
    {
        // Abaikan klik jika mouse berada di atas UI Button (seperti sidebar UI)
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 3000f);

        // Urutkan hits dari yang terdekat dari kamera
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
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
        }

        // Klik di tempat kosong
        DeselectAll();
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
    }

    void HandleMouseUp()
    {
        isDraggingAnything = false;

        if (selectedYard != null) selectedYard.EndDrag();
        if (selectedRoad != null) selectedRoad.EndDrag();
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

    Vector3 GetGroundPoint(Camera cam)
    {
        float targetY = groundY;
        if (selectedYard != null) targetY = selectedYard.transform.position.y;
        else if (selectedRoad != null) targetY = selectedRoad.transform.position.y;

        // Raycast ke bidang datar Y = targetY
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
        Camera cam = GetActiveCamera();
        Vector3 envCenter = GetEnvironmentCenter();

        if (cam != null)
        {
            // Raycast dari tengah viewport kamera ke bidang tanah Y = groundY
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, groundY, 0));
            float distance;

            if (groundPlane.Raycast(ray, out distance))
            {
                Vector3 point = ray.GetPoint(distance);
                point.y = groundY;

                // Jika titik raycast tidak terlalu jauh dari vr-env (< 600m), gunakan raycast point
                if (Vector3.Distance(new Vector3(point.x, 0, point.z), new Vector3(envCenter.x, 0, envCenter.z)) < 600f)
                {
                    return point;
                }
            }
        }

        // Fallback: tepat di vr-env (X: 111, Y: 19, Z: -455)
        return envCenter;
    }

    public void EnsureOrbitCameraInstalled()
    {
        Camera cam = GetActiveCamera();
        if (cam != null)
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
    }

    Camera GetActiveCamera()
    {
        if (ViewModeManager.Instance != null)
        {
            Camera viewCam = ViewModeManager.Instance.GetActiveCamera();
            if (viewCam != null && viewCam.isActiveAndEnabled) return viewCam;
        }
        if (Camera.main != null && Camera.main.isActiveAndEnabled) return Camera.main;

        Camera anyCam = FindFirstObjectByType<Camera>();
        if (anyCam != null) return anyCam;

        return Camera.current;
    }

    // --- Public Getters ---

    public bool IsDraggingAnything() { return isDraggingAnything; }

    public List<DraggableYard> GetActiveYards() { return activeYards; }
    public List<DraggableRoad> GetActiveRoads() { return activeRoads; }

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
        // Cleanup cached materials
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
