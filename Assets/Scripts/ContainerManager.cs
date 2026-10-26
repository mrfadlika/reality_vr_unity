using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

[ExecuteAlways]
public class ContainerManager : MonoBehaviour
{
    public string ctrLocApiUrl = "https://apps.globalhalalhub.co.id/global-halal-api/api/vr/getCtrLoc";

    [Header("Edit Mode Tools")]
    [Tooltip("Centang untuk langsung memuat kontainer dari API di Scene View sebelum di-Run.")]
    public bool loadInEditModeOnValidate = false;

    // Database kontainer untuk fitur klik
    private Dictionary<GameObject, ContainerData> containerDatabase = new Dictionary<GameObject, ContainerData>();
    private ContainerData selectedContainer = null;
    
    // Simpan daftar kontainer per blok untuk proses perataan paksa (Auto-Alignment)
    private Dictionary<string, List<GameObject>> blockContainers = new Dictionary<string, List<GameObject>>();

    // Dictionary untuk memadatkan nomor baris/slot yang bolong (Sparse to Dense mapping)
    private Dictionary<string, List<int>> blockRows = new Dictionary<string, List<int>>();
    private Dictionary<string, List<int>> blockSlots = new Dictionary<string, List<int>>();
    private Dictionary<string, List<int>> blockTiers = new Dictionary<string, List<int>>();

    [Header("Custom 3D Indicator Prefabs (Optional)")]
    [Tooltip("Drag & drop FBX / Prefab panah buatan Anda sendiri di sini.")]
    public GameObject customDirectionalArrowPrefab;
    public GameObject customDownwardArrowPrefab;

    [Header("Container Prefabs (Default)")]
    public GameObject prefab20ft;
    public GameObject prefab40ft;

    [Header("Container Prefabs (Agent 20ft)")]
    public GameObject prefab20ftMSC;
    public GameObject prefab20ftCK;
    public GameObject prefab20ftSLS;
    public GameObject prefab20ftSKS;
    public GameObject prefab20ftSBS;
    public GameObject prefab20ftWHL;

    [Header("Container Prefabs (Agent 40ft)")]
    public GameObject prefab40ftMSC;
    public GameObject prefab40ftCK;
    public GameObject prefab40ftSLS;
    public GameObject prefab40ftSKS;
    public GameObject prefab40ftSBS;
    public GameObject prefab40ftWHL;

    [Header("Container Standard Real Size")]
    [Tooltip("Ubah ukuran ini jika skala dunia (Yard) Anda lebih besar/kecil dari 1 unit = 1 meter.")]
    public float standardWidth = 2.44f;
    public float standardHeight = 2.59f;
    public float standardLength20ft = 6.06f;
    public float standardLength40ft = 12.19f;

    [Header("World Scale Adjustment")]
    [Tooltip("Ubah angka ini (misal: 2, 5, atau 10) untuk membesarkan kontainer beserta grid-nya sekaligus!")]
    public float masterScale = 1f;

    [Header("Spacing configuration (Real World Meters)")]
    [Tooltip("Jarak total antar slot. Standar = 6.1")]
    public float slotSpacing = 6.1f; 
    [Tooltip("Jarak total antar baris. Standar = 2.8")]
    public float rowSpacing = 2.8f;  
    [Tooltip("Jarak total vertikal. Standar = 2.6")]
    public float tierSpacing = 2.6f; 

    [Header("Container Scale & Offset")]
    [Tooltip("Gunakan ini untuk memperbesar/memperkecil kontainer jika tidak terlihat (misal: ubah ke 10 atau 100)")]
    public float containerScaleMultiplier = 1f;
    [Tooltip("Jika kontainer tenggelam separuh, naikkan nilai ini (misal 1.3)")]
    public float yOffset = 0f;
    [Tooltip("Jarak teks pintu masuk ke dalam kontainer (recess). Nilai semakin negatif = semakin mundur ke dalam pelat pintu. Default = -0.02")]
    public float doorTextZOffset = -0.02f;
    [Tooltip("Geser posisi horizontal teks pintu (X). Positif = geser ke kanan menjauhi tiang, Negatif = geser ke kiri.")]
    public float doorTextXOffset = 0f;
    [Tooltip("Ukuran huruf tulisan di pintu belakang kontainer (Nomor, ISO, TARE). Standar terbaca jelas = 0.65")]
    public float doorTextFontSize = 0.75f;

    private Vector3 size20ft;
    private Vector3 offset20ft;
    private Vector3 size40ft;
    private Vector3 offset40ft;
    private bool hasMeasuredPrefabs = false;

    void OnValidate()
    {
        if (!Application.isPlaying && loadInEditModeOnValidate)
        {
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
            UnityEditor.EditorApplication.delayCall -= DelayFetchEditMode;
            UnityEditor.EditorApplication.delayCall += DelayFetchEditMode;
#endif
        }
    }

#if UNITY_EDITOR
    void DelayFetchEditMode()
    {
        if (this != null && gameObject != null && loadInEditModeOnValidate)
        {
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
            FetchContainersInEditMode();
        }
    }
#endif

    [ContextMenu("Fetch & Spawn Containers (Edit Mode)")]
    public void FetchContainersInEditMode()
    {
        try
        {
            using (var client = new System.Net.WebClient())
            {
                client.Headers.Add("X-Pinggy-No-Screen", "1");
                string json = client.DownloadString(ctrLocApiUrl);
                
                ClearContainersInEditMode();
                ParseAndSpawn(json);
                Debug.Log("[Edit Mode] Berhasil memuat & menampilkan seluruh kontainer 3D di Scene View!");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[Edit Mode] Gagal mengambil data kontainer dari API: " + ex.Message);
        }
    }

    private readonly HashSet<Material> instancedMaterials = new HashSet<Material>();

    public static ContainerManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        // Buka lock frame rate ke 60 FPS agar rendering di WebGL mulus tanpa stuttering
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        ClearContainersInEditMode();
    }

    [ContextMenu("Hapus Semua Kontainer (Clear)")]
    public void ClearContainersInEditMode()
    {
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform t in allTransforms)
        {
            if (t != null && (t.name.Contains("ft)") || t.name.StartsWith("Container_") || 
                t.name.StartsWith("GridNumbering_") || t.name.Contains("YardNumbering") || 
                t.name.StartsWith("Numbering_") || t.name == "ContainerMarkings" ||
                t.name.StartsWith("ContainerMarkings") || t.name.StartsWith("Txt_") ||
                t.name.StartsWith("Logo") || t.name.StartsWith("20feet") || t.name.StartsWith("40feet")))
            {
                toDestroy.Add(t.gameObject);
            }
        }

        foreach (GameObject go in toDestroy)
        {
            if (go == null) continue;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
        Debug.Log("[Edit Mode] Semua kontainer & penomoran grid dibersihkan.");
    }

    public void MoveBlockContainers(string blockName, Vector3 delta)
    {
        string bUpper = blockName.ToUpper();
        if (blockContainers.ContainsKey(bUpper))
        {
            foreach (var go in blockContainers[bUpper])
            {
                if (go != null) go.transform.position += delta;
            }
        }
    }

    public void RotateBlockContainers(string blockName, Vector3 pivot, float angleY)
    {
        string bUpper = blockName.ToUpper();
        if (blockContainers.ContainsKey(bUpper))
        {
            foreach (var go in blockContainers[bUpper])
            {
                if (go != null)
                {
                    go.transform.RotateAround(pivot, Vector3.up, angleY);
                }
            }
        }
    }

    void Start()
    {
        // Auto-initialize ViewModeManager and ContainerSearchManager if not present
        if (Object.FindFirstObjectByType<ViewModeManager>() == null)
        {
            GameObject vmmObj = new GameObject("ViewModeManager");
            vmmObj.AddComponent<ViewModeManager>();
        }

        if (Object.FindFirstObjectByType<ContainerSearchManager>() == null)
        {
            GameObject csmObj = new GameObject("ContainerSearchManager");
            ContainerSearchManager csm = csmObj.AddComponent<ContainerSearchManager>();
            csm.customDirectionalArrowPrefab = customDirectionalArrowPrefab;
            csm.customDownwardArrowPrefab = customDownwardArrowPrefab;
        }

        // Pastikan seluruh kontainer yang ada di scene memiliki collider fisik solid (tidak tembus)
        FixAllContainerColliders();

        StartCoroutine(FetchContainers());
    }

    [ContextMenu("Perbaiki Semua Collider Kontainer (Fix Colliders)")]
    public void FixAllContainerColliders()
    {
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        int fixedCount = 0;
        foreach (Transform t in allTransforms)
        {
            if (t != null && t.name.Contains("ft)"))
            {
                BoxCollider c = t.GetComponent<BoxCollider>();
                if (c == null) c = t.gameObject.AddComponent<BoxCollider>();

                Renderer[] rends = t.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++)
                    {
                        b.Encapsulate(rends[i].bounds);
                    }

                    Vector3 lossy = t.lossyScale;
                    c.center = t.InverseTransformPoint(b.center);
                    c.size = new Vector3(
                        b.size.x / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
                        b.size.y / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)),
                        b.size.z / Mathf.Max(0.0001f, Mathf.Abs(lossy.z))
                    );
                    fixedCount++;
                }
            }
        }
        if (fixedCount > 0)
        {
            Debug.Log($"[ContainerManager] Berhasil memasang BoxCollider solid pada {fixedCount} kontainer agar tidak tembus!");
        }
    }

    IEnumerator FetchContainers()
    {
        string primaryUrl = ctrLocApiUrl;
#if UNITY_WEBGL && !UNITY_EDITOR
        if (primaryUrl.Contains("globalhalalhub.co.id"))
        {
            primaryUrl = "api/getCtrLoc";
        }
#endif
        List<string> candidateUrls = new List<string> { primaryUrl };
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!candidateUrls.Contains(ctrLocApiUrl))
        {
            candidateUrls.Add(ctrLocApiUrl);
        }
#endif

        bool fetchSuccess = false;

        foreach (string url in candidateUrls)
        {
            for (int attempt = 1; attempt <= 2 && !fetchSuccess; attempt++)
            {
                using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
                {
                    webRequest.timeout = 15;
                    if (url.Contains("pinggy"))
                    {
                        webRequest.SetRequestHeader("X-Pinggy-No-Screen", "1");
                    }

                    yield return webRequest.SendWebRequest();

                    if (webRequest.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(webRequest.downloadHandler.text))
                    {
                        string jsonResponse = webRequest.downloadHandler.text.Trim();
                        if (jsonResponse.StartsWith("[") && jsonResponse.EndsWith("]"))
                        {
                            Debug.Log($"[ContainerManager] Berhasil memuat kontainer dari {url} ({webRequest.downloadHandler.text.Length} bytes)");
                            ParseAndSpawn(jsonResponse);
                            fetchSuccess = true;
                            yield break;
                        }
                    }

                    Debug.LogWarning($"[ContainerManager] Percobaan {attempt}/2 ke {url} gagal: {webRequest.error}");
                    if (attempt < 2)
                    {
                        yield return new WaitForSeconds(1.5f);
                    }
                }
            }
        }

        // JIKA SERVER MASIH TIMEOUT (504) ATAU OFFLINE:
        // Gunakan Fallback Lokal Otomatis 1.142 Kontainer agar yard TIDAK PERNAH KOSONG!
        if (!fetchSuccess)
        {
            Debug.LogWarning("[ContainerManager] Server API tidak merespons (504 Gateway Timeout / Offline). Mengaktifkan Fallback Lokal Otomatis!");
            TextAsset fallbackAsset = Resources.Load<TextAsset>("containers_fallback");
            if (fallbackAsset != null && !string.IsNullOrEmpty(fallbackAsset.text))
            {
                Debug.Log("[ContainerManager] Berhasil memuat 1.142 kontainer dari cache lokal!");
                ParseAndSpawn(fallbackAsset.text);
            }
            else
            {
                Debug.LogError("[ContainerManager] Fallback data tidak ditemukan di Resources/containers_fallback.json!");
            }
        }
    }

    void ParseAndSpawn(string json)
    {
        ClearContainersInEditMode(); // Purge old containers & numbering before spawning
        if (!hasMeasuredPrefabs) MeasureStandardPrefabs();

        // Wrap JSON array into an object so JsonUtility can parse it
        string wrappedJson = "{\"data\":" + json + "}";
        CtrLocResponse response = JsonUtility.FromJson<CtrLocResponse>(wrappedJson);

        if (response != null && response.data != null)
        {
            containerDatabase.Clear();
            selectedContainer = null;
            blockContainers.Clear(); // Bersihkan data lama tiap kali fetch
            blockRows.Clear();
            blockSlots.Clear();
            blockTiers.Clear();

            // 1. Kumpulkan semua nomor Baris, Slot, dan Tier yang unik untuk tiap Yard
            foreach (var ctr in response.data)
            {
                string b = ctr.YARD_BLOCK;
                int r = 0, s = 0, t = 0;
                int.TryParse(ctr.YARD_ROW, out r);
                int.TryParse(ctr.YARD_SLOT, out s);
                int.TryParse(ctr.YARD_TIER, out t);

                // BATAS MAKSIMUM ROW: Sesuai Master Block API, maksimal row adalah 6
                // Jika data API mengirimkan row > 6 (offside), kita potong/hiraukan
                if (r > 6) continue;

                if (!blockRows.ContainsKey(b)) blockRows[b] = new List<int>();
                if (!blockSlots.ContainsKey(b)) blockSlots[b] = new List<int>();
                if (!blockTiers.ContainsKey(b)) blockTiers[b] = new List<int>();

                if (!blockRows[b].Contains(r)) blockRows[b].Add(r);
                if (!blockTiers[b].Contains(t)) blockTiers[b].Add(t);
                if (!blockSlots[b].Contains(s)) blockSlots[b].Add(s);
            }

            // 2. Urutkan agar kita bisa memetakannya dari 0, 1, 2, dst secara rapat tanpa celah
            foreach (var key in blockRows.Keys)
            {
                blockRows[key].Sort();
                blockSlots[key].Sort();
                blockTiers[key].Sort();
            }

            // 3. Mulai Spawn
            foreach (var ctr in response.data)
            {
                SpawnContainer(ctr);
            }

            // AUTO-ALIGNMENT KHUSUS SUMBU Y (Agar menyentuh lantai dengan sempurna)
            AlignContainersToYards();

            // GAMBAR PENOMORAN SLOT & ROW DI LANTAI
            DrawYardNumbering();

            // REFRESH KAMERA 3D VIEW AGAR LANGSUNG MENATAP KETENGAH SELURUH YARD SECARA PRESISI
            if (ViewModeManager.Instance != null && ViewModeManager.Instance.currentMode == CameraViewMode.BirdView3D)
            {
                ViewModeManager.Instance.SetViewMode(CameraViewMode.BirdView3D);
            }

            Debug.Log($"Successfully spawned {response.data.Length} containers!");
        }
        else
        {
            Debug.LogError("Failed to parse container data.");
        }
    }

    void SpawnContainer(ContainerData data)
    {
        Transform targetYard = GetYard(data.YARD_BLOCK);
        if (targetYard == null) return;

        int originalSlot = 0, originalRow = 0, originalTier = 0;
        int.TryParse(data.YARD_SLOT, out originalSlot);
        int.TryParse(data.YARD_ROW, out originalRow);
        int.TryParse(data.YARD_TIER, out originalTier);

        // BATAS MAKSIMUM ROW
        // Potong sisa row yang offside/melebihi 6
        if (originalRow > 6) return;

        GameObject prefab = null;
        string currentAgent = data.AGENT != null ? data.AGENT.ToLower().Trim() : "";
        string sizeStr = data.CTR_SIZE != null ? data.CTR_SIZE.Trim() : "";

        if (data.CTR_SIZE == "40")
        {
            if (currentAgent == "msc" && prefab40ftMSC != null) prefab = prefab40ftMSC;
            else if (currentAgent == "ck" && prefab40ftCK != null) prefab = prefab40ftCK;
            else if (currentAgent == "sls" && prefab40ftSLS != null) prefab = prefab40ftSLS;
            else if (currentAgent == "sks" && prefab40ftSKS != null) prefab = prefab40ftSKS;
            else if (currentAgent == "sbs" && prefab40ftSBS != null) prefab = prefab40ftSBS;
            else if (currentAgent == "whl" && prefab40ftWHL != null) prefab = prefab40ftWHL;
            
            if (prefab == null) prefab = prefab40ft; // Fallback
        }
        else
        {
            if (currentAgent == "msc" && prefab20ftMSC != null) prefab = prefab20ftMSC;
            else if (currentAgent == "ck" && prefab20ftCK != null) prefab = prefab20ftCK;
            else if (currentAgent == "sls" && prefab20ftSLS != null) prefab = prefab20ftSLS;
            else if (currentAgent == "sks" && prefab20ftSKS != null) prefab = prefab20ftSKS;
            else if (currentAgent == "sbs" && prefab20ftSBS != null) prefab = prefab20ftSBS;
            else if (currentAgent == "whl" && prefab20ftWHL != null) prefab = prefab20ftWHL;
            
            if (prefab == null) prefab = prefab20ft; // Fallback
        }
        
        if (prefab == null) return;

        // INSTANTIATE TANPA PARENT
        GameObject containerGo = Instantiate(prefab);
        
        // PENTING: Set posisi ke 0 dan rotasi ke 0 sementara waktu
        // Tujuannya agar pengukuran Bounding Box murni dan tidak miring
        containerGo.transform.position = Vector3.zero;
        containerGo.transform.rotation = Quaternion.identity;
        containerGo.transform.localScale = prefab.transform.localScale * containerScaleMultiplier;

        // AUTO-FIX SCALE (SMART & EXACT): 
        // Memaksa proporsi kontainer menjadi tepat seperti ukuran asli di dunia nyata (dikalikan masterScale).
        Vector3 standardSize = data.CTR_SIZE == "40" ? new Vector3(standardWidth, standardHeight, standardLength40ft) * masterScale 
                                                     : new Vector3(standardWidth, standardHeight, standardLength20ft) * masterScale;
        MeshRenderer[] mrs = containerGo.GetComponentsInChildren<MeshRenderer>();
        if (mrs.Length > 0)
        {
            MeshRenderer mainBody = mrs[0];
            float maxVol = 0;
            foreach (var mr in mrs)
            {
                Vector3 s = mr.bounds.size;
                float vol = s.x * s.y * s.z;
                if (vol > maxVol) { maxVol = vol; mainBody = mr; }
            }
            
            Vector3 currentSize = mainBody.bounds.size;
            if (currentSize.x > 0.1f && currentSize.y > 0.1f && currentSize.z > 0.1f)
            {
                // NON-UNIFORM SCALING: Memaksa Lebar, Tinggi, dan Panjang secara individu
                // Ini menjamin bentuk fisik kontainer tidak akan "kurus" atau "gepeng"
                float scaleX = standardSize.x / (currentSize.x > currentSize.z ? currentSize.z : currentSize.x);
                float scaleY = standardSize.y / currentSize.y;
                float scaleZ = standardSize.z / (currentSize.x > currentSize.z ? currentSize.x : currentSize.z);

                // Perbaiki jika orientasi model terbalik (menghadap sumbu X)
                if (currentSize.x > currentSize.z)
                {
                    float temp = scaleX;
                    scaleX = scaleZ;
                    scaleZ = temp;
                }

                containerGo.transform.localScale = new Vector3(
                    containerGo.transform.localScale.x * scaleX,
                    containerGo.transform.localScale.y * scaleY,
                    containerGo.transform.localScale.z * scaleZ
                );
            }
        } 

        // BACA UKURAN DAN PIVOT AKTUAL DARI KONTAINER INI
        Renderer[] renderers = containerGo.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 pivotOffsetToMin = bounds.min - containerGo.transform.position; 

        // LOGIKA GRID (ABSOLUTE SCALED):
        // Semua slot di API bernomor GANJIL (1, 3, 5, 7, ...).
        // 1 slot = 1 posisi fisik di tanah. 20ft dan 40ft bisa menempati slot yang sama (beda tier).
        // 40ft hanya lebih panjang secara visual, tapi posisi Z-nya tetap sama dengan 20ft di slot itu.
        float actualRowSpacing = rowSpacing * masterScale;
        float actualTierSpacing = tierSpacing * masterScale;
        float actualSlotSpacing = slotSpacing * masterScale;

        // Konversi nomor slot ganjil (1,3,5,7...) ke indeks padat (0,1,2,3...)
        float slotIndex = (originalSlot - 1) / 2.0f;
        int rowIndex = originalRow - 1;
        int tierIndex = originalTier - 1; 

        float zPos = slotIndex * actualSlotSpacing;
        float xPos = rowIndex * actualRowSpacing;
        float yPos = tierIndex * actualTierSpacing + yOffset;

        // 2. CARI TITIK POJOK YARD (Corner Anchoring)
        Vector3 yardCorner = targetYard.position;
        
        // Cari renderer LANTAI YARD saja (abaikan kontainer yang sudah ter-spawn)
        Renderer[] allRends = targetYard.GetComponentsInChildren<Renderer>();
        System.Collections.Generic.List<Renderer> floorRends = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer r in allRends)
        {
            bool isContainer = false;
            Transform curr = r.transform;
            while (curr != null && curr != targetYard)
            {
                if (curr.name.Contains("ft)")) { isContainer = true; break; }
                curr = curr.parent;
            }
            if (!isContainer) floorRends.Add(r);
        }

        if (floorRends.Count > 0)
        {
            Renderer yardRenderer = floorRends[0]; // Asumsi child pertama adalah bentuk yard-nya
            MeshFilter mf = yardRenderer.GetComponent<MeshFilter>();
            LineRenderer lr = yardRenderer as LineRenderer;

            if (mf != null && mf.sharedMesh != null)
            {
                // Ambil pojok dari Mesh (lokal) lalu konversi ke posisi aslinya di dunia (World Space)
                Vector3 localMin = mf.sharedMesh.bounds.min;
                Vector3 localMax = mf.sharedMesh.bounds.max;
                Vector3 localCorner = new Vector3(localMin.x, localMax.y, localMin.z);
                yardCorner = yardRenderer.transform.TransformPoint(localCorner);
            }
            else if (lr != null && lr.positionCount > 0)
            {
                // Jika yard hanya digambar dari garis LineRenderer
                Vector3[] positions = new Vector3[lr.positionCount];
                lr.GetPositions(positions);
                
                Vector3 localMin = positions[0];
                Vector3 localMax = positions[0];
                for (int i = 1; i < positions.Length; i++)
                {
                    localMin = Vector3.Min(localMin, positions[i]);
                    localMax = Vector3.Max(localMax, positions[i]);
                }
                
                if (lr.useWorldSpace)
                {
                    yardCorner = new Vector3(localMin.x, localMax.y, localMin.z);
                }
                else
                {
                    Vector3 localCorner = new Vector3(localMin.x, localMax.y, localMin.z);
                    yardCorner = yardRenderer.transform.TransformPoint(localCorner);
                }
            }
            else
            {
                // Fallback: gunakan bounding box gabungan dunia (World AABB)
                Bounds yb = floorRends[0].bounds;
                for (int i = 1; i < floorRends.Count; i++) yb.Encapsulate(floorRends[i].bounds);
                yardCorner = new Vector3(yb.min.x, yb.max.y, yb.min.z);
            }
        }

        // Beri sedikit margin dari pinggir lantai agar kontainer tidak menabrak garis tepi persis (misal 1 meter)
        Vector3 margin = (targetYard.right * 1.0f) + (targetYard.forward * 1.0f);
        yardCorner += margin;

        // 3. POSISIKAN MURNI DARI POJOK (Tanpa Auto-Centering)
        Vector3 targetMinPos = yardCorner
                             + (targetYard.right * xPos) 
                             + (targetYard.up * yPos) 
                             + (targetYard.forward * zPos);
                             
        Vector3 finalPosition = targetMinPos 
                              - (targetYard.right * pivotOffsetToMin.x)
                              - (targetYard.up * pivotOffsetToMin.y)
                              - (targetYard.forward * pivotOffsetToMin.z);
        
        containerGo.transform.position = finalPosition;
        containerGo.transform.rotation = targetYard.rotation;

        // BUG FIX UNTUK URP DECAL PROJECTOR:
        // Jika prefab menggunakan URP Decal Projector, Unity sering gagal me-render saat 
        // di-instantiate dan langsung dipindah posisinya dalam satu frame. 
        // Solusinya: Matikan lalu nyalakan kembali komponen tersebut (Toggle).
        foreach (Behaviour comp in containerGo.GetComponentsInChildren<Behaviour>())
        {
            if (comp.GetType().Name == "DecalProjector")
            {
                bool prevState = comp.enabled;
                comp.enabled = false;
                comp.enabled = true;
                comp.enabled = prevState; // Pastikan kembali ke state aslinya jika dari awal memang mati
            }
        }

        // Bersihkan prefab
        foreach (Camera cam in containerGo.GetComponentsInChildren<Camera>()) Destroy(cam.gameObject);
        foreach (Light light in containerGo.GetComponentsInChildren<Light>()) Destroy(light.gameObject);

        // Tambahkan satu BoxCollider solid pada root kontainer untuk raycast & collision fisik (agar tidak tembus)
        BoxCollider col = containerGo.GetComponent<BoxCollider>();
        if (col == null) col = containerGo.AddComponent<BoxCollider>();

        // Hitung bounds akurat di local space kontainer
        Renderer[] containerRends = containerGo.GetComponentsInChildren<Renderer>();
        if (containerRends.Length > 0)
        {
            Bounds worldB = containerRends[0].bounds;
            for (int i = 1; i < containerRends.Length; i++) worldB.Encapsulate(containerRends[i].bounds);

            Vector3 lossy = containerGo.transform.lossyScale;
            col.center = containerGo.transform.InverseTransformPoint(worldB.center);
            col.size = new Vector3(
                worldB.size.x / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
                worldB.size.y / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)),
                worldB.size.z / Mathf.Max(0.0001f, Mathf.Abs(lossy.z))
            );
        }

        // Matikan cast shadows pada ribuan kontainer (hanya bangunan/lingkungan yang cast shadow)
        // Ini memangkas ribuan shadow draw calls dan membuat FPS naik drastis
        foreach (Renderer r in containerGo.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            // GPU Instancing: kontainer bertipe sama digambar dalam sedikit draw call
            foreach (Material m in r.sharedMaterials)
            {
                if (m != null && !m.enableInstancing && instancedMaterials.Add(m))
                {
                    m.enableInstancing = true;
                }
            }
        }

        containerGo.name = data.CTR_NO + " (" + data.CTR_SIZE + "ft)";

        // Tambahkan tulisan TARE, ISO, dan Check Digit di pintu kontainer
        AddContainerMarkings(containerGo, bounds, data.ISO_CODE, data.TARE);

        containerDatabase.Add(containerGo, data);

        // Simpan ke daftar blok
        if (!blockContainers.ContainsKey(data.YARD_BLOCK)) 
            blockContainers[data.YARD_BLOCK] = new List<GameObject>();
        blockContainers[data.YARD_BLOCK].Add(containerGo);
    }

    public bool TryGetContainerData(GameObject go, out ContainerData data)
    {
        return containerDatabase.TryGetValue(go, out data);
    }

    public void AddContainerMarkings(GameObject containerGo, Bounds localBounds, string isoCode, string tareWeight)
    {
        // Hapus markings lama jika sudah ada (termasuk sisa-sisa ContainerMarkings, LeftCol, RightCol)
        for (int i = containerGo.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = containerGo.transform.GetChild(i);
            if (child.name == "ContainerMarkings" || child.name == "LeftCol" || child.name == "RightCol")
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        // 1. Ambil nomor kontainer MURNI dari CTR_NO di API
        string formattedCtr = containerGo.name.Split(' ')[0].ToUpper().Trim();

        // Parse nomor kontainer: Prefiks (4 huruf), Nomor Seri (6 angka), dan Check Number/Digit
        string prefix = "";
        string digits = formattedCtr;
        string checkDigit = "";

        if (formattedCtr.Length >= 11)
        {
            prefix = formattedCtr.Substring(0, 4);       // Misal: DFSU, MSBU, MSNU, HPCU
            digits = formattedCtr.Substring(4, 6);       // Misal: 646102, 239840, 250158
            checkDigit = formattedCtr.Substring(10, 1);  // Check Digit: misal 4, 0, 8
        }
        else if (formattedCtr.Length == 10)
        {
            prefix = formattedCtr.Substring(0, 4);
            digits = formattedCtr.Substring(4, 6);
            checkDigit = CalculateCheckDigit(prefix + digits).ToString();
        }
        else if (formattedCtr.Length > 4)
        {
            prefix = formattedCtr.Substring(0, 4);
            digits = formattedCtr.Substring(4);
        }

        // 2. Ambil ISO Code MURNI dari API (contoh: 40HC, 20GP, 22G1)
        string isoText = string.IsNullOrEmpty(isoCode) ? "" : isoCode.ToUpper().Trim();
        if (isoText == "-") isoText = "";

        // 3. Ambil TARE MURNI dari API (format: TARE 3,810 KG)
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

        // Buat GameObject baru sebagai anak kontainer untuk menampung teks
        GameObject textObj = new GameObject("ContainerMarkings");
        textObj.transform.SetParent(containerGo.transform, false);

        Vector3 localScale = containerGo.transform.localScale;
        float sX = Mathf.Approximately(localScale.x, 0f) ? 0.001f : localScale.x;
        float sY = Mathf.Approximately(localScale.y, 0f) ? 0.001f : localScale.y;
        float sZ = Mathf.Approximately(localScale.z, 0f) ? 0.001f : localScale.z;

        // Posisikan di panel pintu kanan (persis Gambar 2 ekspektasi):
        // X: -0.33f dari center agar pas di panel daun pintu kanan antara tiang besi (+ doorTextXOffset)
        // Y: +0.28f dari center agar rapi di sudut atas pintu di atas tuas pengunci
        // Z: menempel pas di panel pintu belakang (+ doorTextZOffset)
        float localX = (localBounds.center.x - (localBounds.size.x * 0.33f) + doorTextXOffset) / sX;
        float localY = (localBounds.center.y + (localBounds.size.y * 0.28f)) / sY;
        float localZ = (localBounds.max.z + doorTextZOffset) / sZ;

        textObj.transform.localPosition = new Vector3(localX, localY, localZ);
        textObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        // PENTING: Kompres sumbu X (0.65f) untuk mensimulasikan Font Stensil Industri yang tinggi & ramping persis Gambar 2
        textObj.transform.localScale = new Vector3((masterScale / sX) * 0.65f, masterScale / sY, masterScale / sZ);

        // Susun baris teks menggunakan alignment kolom tunggal (<pos=48%>) persis Gambar 2 & ISO 6346:
        // Baris 1: DFSU<pos=48%>646102 <space=0.35em>4
        // Baris 2: <pos=48%>40HC
        // (spasi kosong \n\n)
        // Baris 3: TARE<pos=48%>3,810 KG
        // (Check number berada di baris 1 setelah spasi, dibalut rapi oleh bingkai kotak rectangle simetris)
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

        string line2 = string.IsNullOrEmpty(isoText) ? "" : $"<pos=48%><b>{isoText}</b>";
        string line3 = string.IsNullOrEmpty(tareDisplay) ? "" : $"TARE<pos=48%>{tareDisplay}";

        string contentText = line1;
        if (!string.IsNullOrEmpty(line2)) contentText += "\n" + line2;
        if (!string.IsNullOrEmpty(line3)) contentText += "\n\n<size=80%>" + line3 + "</size>";

        // Tambahkan komponen TextMeshPro tunggal untuk teks utama
        TMPro.TextMeshPro tmp = textObj.AddComponent<TMPro.TextMeshPro>();
        tmp.text = contentText;

        // Auto-sizing agar teks menyesuaikan ruang secara dinamis dan terbaca besar & jelas
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.3f;
        float maxFont = (doorTextFontSize > 0.1f) ? Mathf.Max(1.3f, doorTextFontSize) : 1.3f;
        tmp.fontSizeMax = maxFont;
        tmp.alignment = TMPro.TextAlignmentOptions.Left;
        tmp.color = new Color(0.92f, 0.92f, 0.92f, 0.95f);
        tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

        // Ukuran box teks disesuaikan untuk menampung format kolom persis Gambar 2
        tmp.rectTransform.sizeDelta = new Vector2(1.10f, 0.75f);

        // Tambahkan outline hitam tipis hanya di desktop/editor (di WebGL dihindari agar tidak boros multi-pass shader)
#if !UNITY_WEBGL
        if (Application.isPlaying)
        {
            tmp.outlineWidth = 0.15f;
            tmp.outlineColor = Color.black;
        }
#endif

        // Paksa TextMeshPro memperbarui kalkulasi glyph dan layout seketika
        tmp.ForceMeshUpdate();

        // Distance Culling: Hanya render detail teks pada kontainer dalam jarak 45 meter dari kamera
        ContainerDetailCuller culler = containerGo.AddComponent<ContainerDetailCuller>();
        culler.targetDetails = textObj;
        culler.cullDistance = 45f;

#if !UNITY_WEBGL || UNITY_EDITOR
        // =========================================================================
        // CHECK NUMBER DI BALUT BINGKAI KOTAK RECTANGLE RAPI (Hanya di Editor / Desktop)
        // =========================================================================
        if (!string.IsNullOrEmpty(checkDigit) && tmp.textInfo != null && tmp.textInfo.characterCount > 0)
        {
            // Ambil character info dari check digit (karakter terlihat terakhir pada Baris 1)
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

                    // Ketebalan garis border rapi & proporsional terhadap ukuran huruf
                    float thick = Mathf.Max(0.007f, digitHeight * 0.075f);

                    // Padding simetris agar angka 100% pas dan elegan di tengah bingkai kotak
                    float padX = Mathf.Max(0.020f, digitWidth * 0.35f);
                    float padY = Mathf.Max(0.015f, digitHeight * 0.18f);

                    // Ukuran luar kotak
                    float boxW = digitWidth + (padX * 2f) + (thick * 2f);
                    float boxH = digitHeight + (padY * 2f) + (thick * 2f);

                    GameObject boxObj = new GameObject("CheckDigitBox");
                    boxObj.transform.SetParent(textObj.transform, false);
                    boxObj.transform.localPosition = new Vector3(digitCenterX, digitCenterY, -0.001f);
                    boxObj.transform.localRotation = Quaternion.identity;
                    boxObj.transform.localScale = Vector3.one;

                    Material borderMat = GetWhiteBorderMaterial();

                    // Buat 4 sisi border tanpa tumpang-tindih di sudut (sudut siku-siku rapi & bersih)
                    CreateBorderQuad(boxObj.transform, "Border_Top", new Vector3(0f, (boxH - thick) * 0.5f, 0f), new Vector3(boxW, thick, 0.001f), borderMat);
                    CreateBorderQuad(boxObj.transform, "Border_Bottom", new Vector3(0f, -(boxH - thick) * 0.5f, 0f), new Vector3(boxW, thick, 0.001f), borderMat);

                    float innerH = boxH - (thick * 2f);
                    CreateBorderQuad(boxObj.transform, "Border_Left", new Vector3(-(boxW - thick) * 0.5f, 0f, 0f), new Vector3(thick, innerH, 0.001f), borderMat);
                    CreateBorderQuad(boxObj.transform, "Border_Right", new Vector3((boxW - thick) * 0.5f, 0f, 0f), new Vector3(thick, innerH, 0.001f), borderMat);
                }
            }
        }
#endif
    }

    private static Material sharedWhiteBorderMat;
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

    private void CreateBorderQuad(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
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

    public static int CalculateCheckDigit(string containerNumber10)
    {
        if (string.IsNullOrEmpty(containerNumber10) || containerNumber10.Length < 10) return 0;
        int sum = 0;
        for (int i = 0; i < 10; i++)
        {
            char c = char.ToUpperInvariant(containerNumber10[i]);
            int val;
            if (char.IsDigit(c))
            {
                val = c - '0';
            }
            else if (char.IsLetter(c))
            {
                int letterIndex = c - 'A';
                if (letterIndex == 0) val = 10;
                else if (letterIndex <= 10) val = letterIndex + 11;
                else if (letterIndex <= 20) val = letterIndex + 12;
                else val = letterIndex + 13;
            }
            else
            {
                return 0;
            }
            sum += val * (1 << i);
        }
        int remainder = sum % 11;
        return remainder % 10;
    }

    void AlignContainersToYards()
    {
        foreach (var kvp in blockContainers)
        {
            string blockName = kvp.Key;
            System.Collections.Generic.List<GameObject> containers = kvp.Value;
            if (containers.Count == 0) continue;

            Transform yard = GetYard(blockName);
            if (yard == null) continue;

            float floorMaxY = yard.position.y;
            
            // Cari renderer lantai YARD saja (abaikan kontainer)
            Renderer[] allRends = yard.GetComponentsInChildren<Renderer>();
            System.Collections.Generic.List<Renderer> floorRends = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer r in allRends)
            {
                bool isContainer = false;
                Transform curr = r.transform;
                while (curr != null && curr != yard)
                {
                    if (curr.name.Contains("ft)")) { isContainer = true; break; }
                    curr = curr.parent;
                }
                if (!isContainer) floorRends.Add(r);
            }

            if (floorRends.Count > 0)
            {
                Bounds yardBounds = floorRends[0].bounds;
                for (int i = 1; i < floorRends.Count; i++) yardBounds.Encapsulate(floorRends[i].bounds);
                floorMaxY = yardBounds.max.y;
            }

            // Hitung Bounding Box (Volume) dari seluruh kumpulan kontainer di blok ini
            Bounds clusterBounds = new Bounds(containers[0].transform.position, Vector3.zero);
            foreach (GameObject c in containers)
            {
                Renderer[] crs = c.GetComponentsInChildren<Renderer>();
                foreach (Renderer cr in crs) clusterBounds.Encapsulate(cr.bounds);
            }

            // HANYA geser Y agar dasar kontainer persis menyentuh lantai
            // X dan Z dibiarkan absolut (sesuai slot asli)
            float shiftY = floorMaxY - clusterBounds.min.y;
            Vector3 shift = new Vector3(0, shiftY, 0);

            foreach (GameObject c in containers)
            {
                c.transform.position += shift;
            }
        }
    }

    void Update()
    {
        // Batch culling teks pintu kontainer (sangat ringan, 75 kontainer per frame tanpa coroutine)
        ContainerDetailCuller.GlobalUpdate();

        if (Application.isPlaying && Input.GetKeyDown(KeyCode.F5))
        {
            StartCoroutine(FetchContainers());
        }

        // Deteksi klik kiri mouse (atau sentuhan layar HP)
        if (Input.GetMouseButtonDown(0))
        {
            if (Camera.main == null) return;
            
            // Tembakkan laser dari kamera ke titik yang diklik
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit))
            {
                // Cek apakah objek yang diklik (atau parent-nya) ada di database kontainer
                Transform current = hit.transform;
                bool found = false;
                while (current != null)
                {
                    if (containerDatabase.ContainsKey(current.gameObject))
                    {
                        selectedContainer = containerDatabase[current.gameObject];
                        found = true;
                        break;
                    }
                    current = current.parent;
                }
                
                // Jika klik objek lain selain kontainer, tutup info
                if (!found) selectedContainer = null;
            }
            else
            {
                // Jika klik di tempat kosong/langit, tutup info
                selectedContainer = null;
            }
        }
    }

    void OnGUI()
    {
        if (selectedContainer != null)
        {
            // Buat kotak UI sederhana di pojok kiri atas
            float boxWidth = 250;
            float boxHeight = 200;
            
            // Styling kotak agar agak tembus pandang
            GUI.color = new Color(1, 1, 1, 0.9f);
            GUI.Box(new Rect(10, 10, boxWidth, boxHeight), "Info Kontainer");

            // Tampilkan Data
            GUI.Label(new Rect(20, 40, boxWidth - 30, 20), "CTR NO    : " + selectedContainer.CTR_NO);
            GUI.Label(new Rect(20, 60, boxWidth - 30, 20), "SIZE      : " + selectedContainer.CTR_SIZE + "ft");
            GUI.Label(new Rect(20, 80, boxWidth - 30, 20), "AGENT     : " + (string.IsNullOrEmpty(selectedContainer.AGENT) ? (string.IsNullOrEmpty(selectedContainer.CTR_MERK) ? "-" : selectedContainer.CTR_MERK) : selectedContainer.AGENT));
            GUI.Label(new Rect(20, 100, boxWidth - 30, 20), "BLOCK     : " + selectedContainer.YARD_BLOCK);
            
            GUI.Label(new Rect(20, 120, boxWidth - 30, 20), "SLOT      : " + selectedContainer.YARD_SLOT);
            
            GUI.Label(new Rect(20, 140, boxWidth - 30, 20), "ROW       : " + selectedContainer.YARD_ROW);
            GUI.Label(new Rect(20, 160, boxWidth - 30, 20), "TIER      : " + selectedContainer.YARD_TIER);

            if (GUI.Button(new Rect(boxWidth - 30, 10, 30, 20), "X"))
            {
                selectedContainer = null; // Tutup UI
            }
        }
    }

    Transform GetYard(string blockName)
    {
        string bUpper = blockName.ToUpper();
        
        // 1. Cari GameObject YardA / YardB / YardC
        GameObject yardGo = GameObject.Find("Yard" + bUpper);
        if (yardGo != null) return yardGo.transform;

        // 2. Fallback: Cari GameObject bawahnyaA / bawahnyaB / bawahnyaC (Objek lantai beton)
        yardGo = GameObject.Find("bawahnya" + bUpper);
        if (yardGo != null) return yardGo.transform;

        yardGo = GameObject.Find("bawahnya" + blockName);
        if (yardGo != null) return yardGo.transform;

        return null;
    }

    void MeasureStandardPrefabs()
    {
        if (prefab20ft != null)
        {
            GameObject dummy20 = Instantiate(prefab20ft);
            dummy20.hideFlags = HideFlags.HideAndDontSave;
            dummy20.transform.position = Vector3.zero;
            dummy20.transform.rotation = Quaternion.identity;
            dummy20.transform.localScale = prefab20ft.transform.localScale * containerScaleMultiplier;
            Renderer[] r20 = dummy20.GetComponentsInChildren<Renderer>();
            if (r20.Length > 0)
            {
                Bounds b = r20[0].bounds;
                for (int i = 1; i < r20.Length; i++) b.Encapsulate(r20[i].bounds);
                size20ft = b.size;
                offset20ft = b.min - dummy20.transform.position;
            }
            if (Application.isPlaying) Destroy(dummy20);
            else DestroyImmediate(dummy20);
        }

        if (prefab40ft != null)
        {
            GameObject dummy40 = Instantiate(prefab40ft);
            dummy40.hideFlags = HideFlags.HideAndDontSave;
            dummy40.transform.position = Vector3.zero;
            dummy40.transform.rotation = Quaternion.identity;
            dummy40.transform.localScale = prefab40ft.transform.localScale * containerScaleMultiplier;
            Renderer[] r40 = dummy40.GetComponentsInChildren<Renderer>();
            if (r40.Length > 0)
            {
                Bounds b = r40[0].bounds;
                for (int i = 1; i < r40.Length; i++) b.Encapsulate(r40[i].bounds);
                size40ft = b.size;
                offset40ft = b.min - dummy40.transform.position;
            }
            if (Application.isPlaying) Destroy(dummy40);
            else DestroyImmediate(dummy40);
        }
        
        if (size20ft == Vector3.zero) size20ft = new Vector3(2.4f, 2.6f, 6.0f);
        if (size40ft == Vector3.zero) size40ft = new Vector3(2.4f, 2.6f, 12.0f);

        hasMeasuredPrefabs = true;
    }

    void DrawYardNumbering()
    {
        // Hapus numbering lama jika ada (agar tidak menumpuk saat reload)
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        List<GameObject> oldNumList = new List<GameObject>();
        foreach (Transform t in allTransforms)
        {
            if (t != null && (t.name == "YardNumbering" || t.name.StartsWith("Numbering_") || t.name.StartsWith("GridNumbering_")))
            {
                oldNumList.Add(t.gameObject);
            }
        }
        foreach (GameObject go in oldNumList)
        {
            if (go == null) continue;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        int maxSlot = 45; // Maksimal slot dari API Master Block
        int maxRow = 6;   // Maksimal row

        float actualRowSpacing = rowSpacing * masterScale;
        float actualSlotSpacing = slotSpacing * masterScale;

        GameObject numberingParent = new GameObject("YardNumbering");

        foreach (var blockName in blockRows.Keys)
        {
            Transform targetYard = GetYard(blockName);
            if (targetYard == null) continue;

            // 1. CARI TITIK POJOK YARD 
            Vector3 yardCorner = targetYard.position;
            Renderer[] allRends = targetYard.GetComponentsInChildren<Renderer>();
            System.Collections.Generic.List<Renderer> floorRends = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer r in allRends)
            {
                bool isContainer = false;
                Transform curr = r.transform;
                while (curr != null && curr != targetYard)
                {
                    if (curr.name.Contains("ft)")) { isContainer = true; break; }
                    curr = curr.parent;
                }
                if (!isContainer) floorRends.Add(r);
            }

            float floorMaxY = yardCorner.y;
            if (floorRends.Count > 0)
            {
                Renderer yardRenderer = floorRends[0]; 
                MeshFilter mf = yardRenderer.GetComponent<MeshFilter>();
                LineRenderer lr = yardRenderer as LineRenderer;

                if (mf != null && mf.sharedMesh != null)
                {
                    Vector3 localMin = mf.sharedMesh.bounds.min;
                    Vector3 localMax = mf.sharedMesh.bounds.max;
                    Vector3 localCorner = new Vector3(localMin.x, localMax.y, localMin.z);
                    yardCorner = yardRenderer.transform.TransformPoint(localCorner);
                }
                else if (lr != null && lr.positionCount > 0)
                {
                    Vector3[] positions = new Vector3[lr.positionCount];
                    lr.GetPositions(positions);
                    Vector3 localMin = positions[0];
                    Vector3 localMax = positions[0];
                    for (int i = 1; i < positions.Length; i++)
                    {
                        localMin = Vector3.Min(localMin, positions[i]);
                        localMax = Vector3.Max(localMax, positions[i]);
                    }
                    if (lr.useWorldSpace) yardCorner = new Vector3(localMin.x, localMax.y, localMin.z);
                    else yardCorner = yardRenderer.transform.TransformPoint(new Vector3(localMin.x, localMax.y, localMin.z));
                }
                else
                {
                    Bounds yb = floorRends[0].bounds;
                    for (int i = 1; i < floorRends.Count; i++) yb.Encapsulate(floorRends[i].bounds);
                    yardCorner = new Vector3(yb.min.x, yb.max.y, yb.min.z);
                }

                // Hitung Y maksimal dari seluruh lantai untuk posisi teks agar tidak tenggelam
                Bounds yMaxBounds = floorRends[0].bounds;
                for (int i = 1; i < floorRends.Count; i++) yMaxBounds.Encapsulate(floorRends[i].bounds);
                floorMaxY = yMaxBounds.max.y;
            }

            // Margin persis seperti yang digunakan di SpawnContainer
            Vector3 margin = (targetYard.right * 1.0f) + (targetYard.forward * 1.0f);
            yardCorner += margin;
            
            float textY = floorMaxY + 0.05f; // Sedikit melayang dari aspal

            GameObject blockParent = new GameObject("Numbering_" + blockName);
            blockParent.transform.SetParent(numberingParent.transform);

            // SLOT NUMBERS (Ganjil 001, 003, ..., 045)
            for (int s = 1; s <= maxSlot; s += 2)
            {
                float slotIndex = (s - 1) / 2.0f;
                float zPos = slotIndex * actualSlotSpacing;
                
                // Posisikan teks di titik tengah (center) dari kontainer (panjang standar 6.06 meter)
                float zCenter = zPos + ((standardLength20ft * masterScale) / 2f);
                
                // Geser lebih jauh ke luar aspal (-4.0m) agar tidak terlalu mepet
                float xPosSlotLine = -4.0f * masterScale; 

                Vector3 pos = yardCorner
                             + (targetYard.right * xPosSlotLine) 
                             + (targetYard.up * 0) 
                             + (targetYard.forward * zCenter);
                             
                pos.y = textY;
                // Rotasi 90 derajat agar bagian atas teks mengarah ke kontainer (membaca dari samping)
                Quaternion rot = targetYard.rotation * Quaternion.Euler(90, 90, 0);
                CreateTextAt(blockParent.transform, pos, rot, s.ToString("000"));
            }

            // ROW NUMBERS (R1, R2, ..., R6)
            for (int r = 1; r <= maxRow; r++)
            {
                int rowIndex = r - 1;
                float xPos = rowIndex * actualRowSpacing;
                
                // Posisikan teks di titik tengah (center) dari kontainer (lebar standar 2.44 meter)
                float xCenter = xPos + ((standardWidth * masterScale) / 2f);
                
                // Geser lebih jauh ke luar aspal (-4.0m) agar tidak terlalu mepet dengan kontainer terdepan
                float zPosRowLine = -4.0f * masterScale;

                Vector3 pos = yardCorner
                             + (targetYard.right * xCenter) 
                             + (targetYard.up * 0) 
                             + (targetYard.forward * zPosRowLine);
                             
                pos.y = textY;
                // Teks row tetap menghadap ke Utara
                Quaternion rot = targetYard.rotation * Quaternion.Euler(90, 0, 0);
                CreateTextAt(blockParent.transform, pos, rot, "R" + r.ToString());
            }
        }
    }

    void CreateTextAt(Transform parent, Vector3 pos, Quaternion rot, string text)
    {
        GameObject txtObj = new GameObject("Txt_" + text);
        txtObj.transform.SetParent(parent);
        txtObj.transform.position = pos;
        
        // Terapkan rotasi yang dikirim
        txtObj.transform.rotation = rot;

        TMPro.TextMeshPro tmp = txtObj.AddComponent<TMPro.TextMeshPro>();
        tmp.text = text;
        // Perkecil font agar tulisan R1, R2, R3 tidak bertabrakan/saling tumpuk
        tmp.fontSize = 14 * masterScale; 
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white; 
        if (Application.isPlaying)
        {
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = Color.black; 
        }
    }
}

[System.Serializable]
public class CtrLocResponse
{
    public ContainerData[] data;
}

[System.Serializable]
public class ContainerData
{
    public string YARD_ROW;
    public string YARD_BLOCK;
    public string YARD_SLOT;
    public string CTR_NO;
    public string CTR_SIZE;
    public string YARD_TIER;
    public string CTR_MERK;
    public string AGENT;
    public string ISO_CODE;
    public string TARE;
}
