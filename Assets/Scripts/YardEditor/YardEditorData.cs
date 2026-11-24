using UnityEngine;
using System.Collections.Generic;

// ============================================================
// YardEditorData.cs
// Data models & serialization untuk Yard Editor (LEGO mode)
// ============================================================

// --- API Response Models ---

[System.Serializable]
public class MasterBlockData
{
    public string YARD_ROW;
    public string YARD_BLOCK;
    public string YARD_SLOT;
    public string YARD_BLOCK_NAME;
    public string YARD_TIER;
}

[System.Serializable]
public class MasterBlockResponse
{
    public MasterBlockData[] data;
}

// --- Container Location API Models (getCtrLoc) ---

[System.Serializable]
public class ContainerLocData
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

[System.Serializable]
public class ContainerLocResponse
{
    public ContainerLocData[] data;
}

// --- Saved Layout Models ---

[System.Serializable]
public class YardPlacement
{
    public string blockName;       // Nama block dari API (bisa apapun: "A", "B", "X1", "EAST", dll)
    public float posX;
    public float posZ;
    public bool isHorizontal;     // false = default (panjang di Z), true = rotated 90°
    public float sizeWidth;        // Ukuran X yard (dari API calculation)
    public float sizeLength;       // Ukuran Z yard (dari API calculation)

    public YardPlacement() { }

    public YardPlacement(string name, Vector3 worldPos, bool horizontal, float width = 0, float length = 0)
    {
        blockName = name;
        posX = worldPos.x;
        posZ = worldPos.z;
        isHorizontal = horizontal;
        sizeWidth = width;
        sizeLength = length;
    }
}

[System.Serializable]
public class RoadPlacement
{
    public int id;                 // Unique ID
    public float posX;
    public float posZ;
    public float rotationY;       // 0, 90, 180, 270
    public float scaleX;          // Lebar road
    public float scaleZ;          // Panjang road

    public RoadPlacement() 
    {
        scaleX = 12f;
        scaleZ = 50f;
        rotationY = 0f;
    }

    public RoadPlacement(int roadId, Vector3 worldPos, float rotY, float sX, float sZ)
    {
        id = roadId;
        posX = worldPos.x;
        posZ = worldPos.z;
        rotationY = rotY;
        scaleX = sX;
        scaleZ = sZ;
    }
}

[System.Serializable]
public class AccessoryPlacement
{
    public int id;
    public string accessoryType;   // "Gedung", "ReachStacker", "TopLoader", "Gate", "Pagar"
    public float posX;
    public float posZ;
    public float rotationY;
    public float scaleMultiplier = 1f;
    public float fenceLength = 40f;

    public AccessoryPlacement()
    {
        scaleMultiplier = 1f;
        fenceLength = 40f;
        rotationY = 0f;
    }

    public AccessoryPlacement(int accId, string type, Vector3 worldPos, float rotY, float scale = 1f, float length = 40f)
    {
        id = accId;
        accessoryType = type;
        posX = worldPos.x;
        posZ = worldPos.z;
        rotationY = rotY;
        scaleMultiplier = scale <= 0.01f ? 1f : scale;
        fenceLength = length <= 1f ? 40f : length;
    }
}

[System.Serializable]
public class YardLayout
{
    public List<YardPlacement> yards = new List<YardPlacement>();
    public List<RoadPlacement> roads = new List<RoadPlacement>();
    public List<AccessoryPlacement> accessories = new List<AccessoryPlacement>();
}

// --- Static Utility ---

public static class YardEditorDataUtil
{
    private const string DEFAULT_SAVE_KEY = "YardEditorLayout";

    public static YardLayout loadedDenahFromFile = null;
    public static string loadedDenahJsonFromFile = null;

    public static string GetSaveKey()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(sceneName) && sceneName != "Copy")
        {
            return DEFAULT_SAVE_KEY + "_" + sceneName;
        }
        return DEFAULT_SAVE_KEY;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void YardEditor_SaveToLocalStorage(string key, string value);

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern string YardEditor_LoadFromLocalStorage(string key);

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern int YardEditor_HasLocalStorageKey(string key);

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void YardEditor_RemoveLocalStorageKey(string key);

    [System.Runtime.InteropServices.DllImport("__Internal")]
    public static extern void YardEditor_DownloadFile(string filename, string content);
#endif

    public static void SaveLayout(YardLayout layout)
    {
        string json = JsonUtility.ToJson(layout, true);
        string saveKey = GetSaveKey();

        // 1. Simpan ke PlayerPrefs (IndexedDB WebGL / Windows Registry)
        try
        {
            PlayerPrefs.SetString(saveKey, json);
            PlayerPrefs.Save();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[YardEditor] PlayerPrefs save warning: " + ex.Message);
        }

        // 2. Simpan juga ke HTML5 LocalStorage (Sangat tangguh & instan di WebGL, 100% tahan reload halaman)
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            YardEditor_SaveToLocalStorage(saveKey, json);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[YardEditor] LocalStorage save warning: " + ex.Message);
        }
#endif

        // 3. Simpan ke file fisik data/denah.json jika berjalan di Editor atau Standalone build
        try
        {
            string projectDataDir = System.IO.Path.Combine(Application.dataPath, "..", "data");
            if (!System.IO.Directory.Exists(projectDataDir)) System.IO.Directory.CreateDirectory(projectDataDir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(projectDataDir, "denah.json"), json);

            string streamingDataDir = System.IO.Path.Combine(Application.dataPath, "StreamingAssets", "data");
            if (!System.IO.Directory.Exists(streamingDataDir)) System.IO.Directory.CreateDirectory(streamingDataDir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(streamingDataDir, "denah.json"), json);
            Debug.Log("[YardEditor] Berhasil menulis data ke folder data/denah.json!");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[YardEditor] Gagal menulis ke data/denah.json fisik: " + ex.Message);
        }

        // 4. Di WebGL, autosave langsung ke data/denah.json tanpa memunculkan dialog Save As browser
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            YardEditor_SaveToLocalStorage("data/denah.json", json);
            YardEditor_DownloadFile("denah.json", json);
            Debug.Log("[YardEditor] WebGL autosave ke data/denah.json berhasil dijalankan!");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[YardEditor] WebGL autosave data/denah.json warning: " + ex.Message);
        }
#endif

        loadedDenahFromFile = layout;
        loadedDenahJsonFromFile = json;

        int accCount = layout.accessories != null ? layout.accessories.Count : 0;
        Debug.Log("[YardEditor] Layout tersimpan di key '" + saveKey + "'! (" + layout.yards.Count + " yards, " + layout.roads.Count + " roads, " + accCount + " accessories)");
    }

    private const string SERVER_BASELINE_KEY = "YardEditor_ServerDenahBaseline";

    public static string GetServerDenahBaseline()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            if (YardEditor_HasLocalStorageKey(SERVER_BASELINE_KEY) == 1)
            {
                return YardEditor_LoadFromLocalStorage(SERVER_BASELINE_KEY);
            }
        }
        catch {}
#endif
        if (PlayerPrefs.HasKey(SERVER_BASELINE_KEY))
        {
            return PlayerPrefs.GetString(SERVER_BASELINE_KEY);
        }
        return null;
    }

    public static void SetServerDenahBaseline(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            PlayerPrefs.SetString(SERVER_BASELINE_KEY, json);
            PlayerPrefs.Save();
        }
        catch {}
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            YardEditor_SaveToLocalStorage(SERVER_BASELINE_KEY, json);
        }
        catch {}
#endif
    }
    
    public static string GetSavedLayoutJson()
    {
        string saveKey = GetSaveKey();
        string json = null;
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            if (YardEditor_HasLocalStorageKey(saveKey) == 1)
            {
                json = YardEditor_LoadFromLocalStorage(saveKey);
            }
        }
        catch {}
#endif
        if (string.IsNullOrEmpty(json) && PlayerPrefs.HasKey(saveKey))
        {
            json = PlayerPrefs.GetString(saveKey);
        }
        return json;
    }

    public static void SaveLayoutJsonDirectly(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        string saveKey = GetSaveKey();
        try
        {
            PlayerPrefs.SetString(saveKey, json);
            PlayerPrefs.Save();
        }
        catch {}
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            YardEditor_SaveToLocalStorage(saveKey, json);
        }
        catch {}
#endif
    }

    public static YardLayout LoadLayout()
    {
        string saveKey = GetSaveKey();
        string json = null;

        // 1. Prioritas membaca dari HTML5 LocalStorage (Paling instan & tidak terpengaruh async mount IndexedDB)
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            if (YardEditor_HasLocalStorageKey(saveKey) == 1)
            {
                json = YardEditor_LoadFromLocalStorage(saveKey);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[YardEditor] LocalStorage load warning: " + ex.Message);
        }
#endif

        // 2. Fallback ke PlayerPrefs jika belum ada di LocalStorage
        if (string.IsNullOrEmpty(json) && PlayerPrefs.HasKey(saveKey))
        {
            json = PlayerPrefs.GetString(saveKey);
        }

        if (string.IsNullOrEmpty(json))
        {
            Debug.Log("[YardEditor] Tidak ada layout tersimpan di key '" + saveKey + "', memuat layout kosong.");
            return new YardLayout();
        }

        YardLayout layout = null;
        try
        {
            layout = JsonUtility.FromJson<YardLayout>(json);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[YardEditor] Gagal parse JSON layout: " + ex.Message);
        }

        if (layout == null) layout = new YardLayout();
        if (layout.yards == null) layout.yards = new List<YardPlacement>();
        if (layout.roads == null) layout.roads = new List<RoadPlacement>();
        if (layout.accessories == null) layout.accessories = new List<AccessoryPlacement>();
        
        Debug.Log("[YardEditor] Layout dimuat dari key '" + saveKey + "'! (" + layout.yards.Count + " yards, " + layout.roads.Count + " roads, " + layout.accessories.Count + " accessories)");
        return layout;
    }

    public static bool HasSavedLayout()
    {
        string saveKey = GetSaveKey();
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            if (YardEditor_HasLocalStorageKey(saveKey) == 1) return true;
        }
        catch {}
#endif
        return PlayerPrefs.HasKey(saveKey);
    }

    public static void ClearLayout()
    {
        string saveKey = GetSaveKey();
        PlayerPrefs.DeleteKey(saveKey);
        PlayerPrefs.Save();
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            YardEditor_RemoveLocalStorageKey(saveKey);
        }
        catch {}
#endif
        Debug.Log("[YardEditor] Layout dihapus dari memory pada key '" + saveKey + "'.");
    }
}

// ============================================================
// DraggableAccessory.cs
// Komponen untuk aset aksesoris (Gedung, Reach Stacker, Top Loader, Gate, Pagar)
// Mendukung FBX / Prefab kustom dari Inspector maupun fallback procedural
// ============================================================

public class DraggableAccessory : MonoBehaviour
{
    [Header("Accessory Info")]
    public int accessoryId;
    public string accessoryType = "Gedung"; // "Gedung", "ReachStacker", "TopLoader", "Gate", "Pagar"
    public float directionAngle = 0f;
    public float scaleMultiplier = 1f;
    public float fenceLength = 40f; // Khusus Pagar: memanjang mirip Road (meter)

    [Header("Visual Feedback")]
    public Color selectedColor = new Color(1f, 0.92f, 0.2f, 1f);
    public Color draggingColor = new Color(0.2f, 0.9f, 0.4f, 1f);

    [HideInInspector] public bool isSelected = false;
    [HideInInspector] public bool isDragging = false;

    private Vector3 dragOffset;
    private float groundY = 19.81f;
    private GameObject modelContainer;
    private GameObject editorLabelObj;
    private GameObject selectionOutlineObj;

    void Start()
    {
        FindGroundY();
        if (transform.position.y < 19.5f || transform.position.y > 19.95f)
        {
            Vector3 pos = transform.position;
            pos.y = groundY;
            transform.position = pos;
        }
        if (modelContainer == null)
        {
            BuildAccessoryVisual();
        }
    }

    void Update()
    {
        // Tampilkan label 2D & seleksi hanya saat di mode Yard Editor.
        // Sembunyikan sepenuhnya saat keluar dari Yard Editor agar visual 3D/Walk View bersih.
        bool inEditor = YardEditorManager.Instance != null && YardEditorManager.Instance.isEditorActive;
        if (!inEditor)
        {
            if (isSelected) Deselect();
            if (selectionOutlineObj != null && selectionOutlineObj.activeSelf)
            {
                selectionOutlineObj.SetActive(false);
            }
            if (editorLabelObj != null && editorLabelObj.activeSelf)
            {
                editorLabelObj.SetActive(false);
            }
            return;
        }

        if (editorLabelObj != null && editorLabelObj.activeSelf != inEditor)
        {
            editorLabelObj.SetActive(inEditor);
        }
    }

    void FindGroundY()
    {
        if (YardEditorManager.Instance != null && YardEditorManager.Instance.groundY > 19.5f && YardEditorManager.Instance.groundY < 19.95f)
        {
            groundY = YardEditorManager.Instance.groundY;
        }
        else
        {
            groundY = 19.81f;
        }
    }

    public string GetDisplayName()
    {
        string clean = (accessoryType ?? "").Replace(" ", "").Trim();
        switch (clean)
        {
            case "ReachStacker": return "REACH STACKER";
            case "TopLoader": return "TOP LOADER";
            case "Gate": return "GATE";
            case "Pagar":
            case "Wall": return "PAGAR";
            default: return "GEDUNG";
        }
    }

    public static Shader GetSafeShader(bool unlit = false)
    {
        Shader s = null;
        if (!unlit)
        {
            s = Shader.Find("Universal Render Pipeline/Lit");
        }
        if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Unlit/Color");
        if (s == null) s = Shader.Find("Sprites/Default");
        if (s == null) s = Shader.Find("Standard");
        if (s == null) s = Shader.Find("Mobile/Diffuse");
        if (s == null) s = Shader.Find("Hidden/InternalErrorShader");
        return s;
    }

    public static Material CreateSafeMaterial(Color col, bool unlit = false)
    {
        Shader s = GetSafeShader(unlit);
        Material m = s != null ? new Material(s) : new Material(Shader.Find("Sprites/Default"));
        m.color = col;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        return m;
    }

    public void BuildAccessoryVisual()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }

        if (scaleMultiplier <= 0.05f) scaleMultiplier = 1f;
        if (fenceLength <= 1f) fenceLength = 40f;

        // Reset rotasi root sementara ke Identity selama proses kalkulasi Bounds & penyusunan segmen lokal agar Renderer.bounds (World AABB) tidak tertukar sumbu X dan Z saat objek sudah di-rotate 90/270 derajat!
        transform.rotation = Quaternion.identity;

        modelContainer = new GameObject("AccessoryModel");
        modelContainer.transform.SetParent(transform, false);
        modelContainer.transform.localPosition = Vector3.zero;
        modelContainer.transform.localRotation = Quaternion.identity;
        modelContainer.transform.localScale = Vector3.one;

        string cleanType = (accessoryType ?? "").Replace(" ", "").Trim();
        GameObject customFbx = YardEditorManager.Instance != null
            ? YardEditorManager.Instance.GetAccessoryPrefab(cleanType)
            : null;

        Bounds localBounds = new Bounds(new Vector3(0, 2f, 0), new Vector3(6f, 4f, 6f));

        try
        {
            if (cleanType == "Pagar" || cleanType == "Wall")
            {
                // Sistem Pagar mirip Road: memanjang mengikuti fenceLength (menyusun berjejer segmen FBX atau fallback)
                localBounds = BuildPagarAlongLength(modelContainer.transform, customFbx, fenceLength, scaleMultiplier);
            }
            else if (customFbx != null)
            {
                GameObject instance = Instantiate(customFbx, modelContainer.transform);
                instance.name = "FBX_" + cleanType;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = customFbx.transform.localRotation;

                float baseScaleFactor = 1f;
                if ((cleanType == "Gedung" || cleanType == "Building") && customFbx.name.ToLower().Contains("bangunan"))
                {
                    baseScaleFactor = 6f; // Sesuai skala gedung di SampleScene
                }
                else if (cleanType == "Gate" && customFbx.transform.localScale.x <= 1.05f)
                {
                    baseScaleFactor = 2f; // Sesuai skala gate di SampleScene
                }

                instance.transform.localScale = customFbx.transform.localScale * (baseScaleFactor * scaleMultiplier);

                // Bersihkan kamera/lampu bawaan FBX Blender agar tidak mengganggu scene
                foreach (Camera c in instance.GetComponentsInChildren<Camera>(true))
                {
                    if (Application.isPlaying) Destroy(c.gameObject);
                    else DestroyImmediate(c.gameObject);
                }
                foreach (Light l in instance.GetComponentsInChildren<Light>(true))
                {
                    if (Application.isPlaying) Destroy(l.gameObject);
                    else DestroyImmediate(l.gameObject);
                }

                // Hitung bounds & penempatan vertikal yang presisi di ruang modelContainer (bebas dari rotasi internal FBX)
                Bounds contBounds = new Bounds(Vector3.zero, Vector3.zero);
                bool hasBounds = false;

                foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || !r.enabled) continue;
                    string n = r.gameObject.name.ToLower();
                    // Abaikan bingkai/jalan pembatas/sisi luar raksasa jika ada pada prefab gedung
                    if (n.Contains("bingkai") || n.Contains("sisi") || n.Contains("jln") || n.Contains("plane") || n.Contains("border")) continue;

                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        Bounds smb = mf.sharedMesh.bounds;
                        Vector3[] corners = new Vector3[8]
                        {
                            new Vector3(smb.min.x, smb.min.y, smb.min.z),
                            new Vector3(smb.min.x, smb.min.y, smb.max.z),
                            new Vector3(smb.min.x, smb.max.y, smb.min.z),
                            new Vector3(smb.min.x, smb.max.y, smb.max.z),
                            new Vector3(smb.max.x, smb.min.y, smb.min.z),
                            new Vector3(smb.max.x, smb.min.y, smb.max.z),
                            new Vector3(smb.max.x, smb.max.y, smb.min.z),
                            new Vector3(smb.max.x, smb.max.y, smb.max.z),
                        };
                        for (int c = 0; c < 8; c++)
                        {
                            Vector3 ptInContainer = modelContainer.transform.InverseTransformPoint(r.transform.TransformPoint(corners[c]));
                            if (!hasBounds) { contBounds = new Bounds(ptInContainer, Vector3.zero); hasBounds = true; }
                            else { contBounds.Encapsulate(ptInContainer); }
                        }
                    }
                    else
                    {
                        Vector3 ptMin = modelContainer.transform.InverseTransformPoint(r.bounds.min);
                        Vector3 ptMax = modelContainer.transform.InverseTransformPoint(r.bounds.max);
                        if (!hasBounds) { contBounds = new Bounds(ptMin, Vector3.zero); hasBounds = true; }
                        else { contBounds.Encapsulate(ptMin); }
                        contBounds.Encapsulate(ptMax);
                    }
                }

                float estimatedW, estimatedH, estimatedL;
                if (hasBounds)
                {
                    // Turunkan / naikkan model sehingga titik terbawah (dasar roda / fondasi) persis menapak di lantai Y = 0 (groundY)
                    float groundOffset = -contBounds.min.y;
                    instance.transform.localPosition = new Vector3(0f, groundOffset, 0f);

                    float maxVisualDim = (cleanType == "Gedung" || cleanType == "Building") ? 40f : 120f;
                    estimatedW = Mathf.Clamp(contBounds.size.x, 2f, maxVisualDim);
                    estimatedH = Mathf.Clamp(contBounds.size.y, 2f, 30f);
                    estimatedL = Mathf.Clamp(contBounds.size.z, 2f, maxVisualDim);

                    localBounds = new Bounds(
                        new Vector3(contBounds.center.x, contBounds.center.y + groundOffset, contBounds.center.z),
                        new Vector3(estimatedW, estimatedH, estimatedL)
                    );
                }
                else
                {
                    float baseW = (cleanType == "Gedung" || cleanType == "Building") ? 24f : 8f;
                    float baseH = (cleanType == "Gedung" || cleanType == "Building") ? 10f : 6f;
                    float baseL = (cleanType == "Gedung" || cleanType == "Building") ? 14f : 10f;
                    estimatedW = baseW * scaleMultiplier;
                    estimatedH = baseH * scaleMultiplier;
                    estimatedL = baseL * scaleMultiplier;
                    instance.transform.localPosition = Vector3.zero;

                    localBounds = new Bounds(
                        new Vector3(0f, estimatedH * 0.5f, 0f),
                        new Vector3(estimatedW, estimatedH, estimatedL)
                    );
                }
            }
            else
            {
                // Fallback procedural 3D jika user belum memasukkan FBX ke Inspector
                localBounds = BuildProceduralFallback(modelContainer.transform, accessoryType, scaleMultiplier);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[YardEditor] Warning building accessory model (" + accessoryType + "): " + ex.Message);
            localBounds = BuildProceduralFallback(modelContainer.transform, accessoryType, scaleMultiplier);
        }

        // Pasang BoxCollider di root untuk raycast click & drag di 2D View
        BoxCollider rootCol = GetComponent<BoxCollider>();
        if (rootCol == null) rootCol = gameObject.AddComponent<BoxCollider>();
        rootCol.center = localBounds.center;
        float maxColDim = accessoryType == "Gedung" ? 45f : 150f;
        rootCol.size = new Vector3(
            Mathf.Clamp(localBounds.size.x, 2.0f, maxColDim),
            Mathf.Clamp(localBounds.size.y, 2.0f, 30f),
            Mathf.Clamp(localBounds.size.z, 2.0f, maxColDim)
        );

        // Buat bingkai highlight untuk indikator seleksi di 2D/3D
        CreateSelectionHighlightBox(localBounds);

        // Buat label teks menghadap ke atas untuk mode 2D Yard Editor
        CreateEditor2DLabel(localBounds);

        // Terapkan kembali rotasi arah objek setelah seluruh geometri lokal dan bounds selesai disusun
        transform.rotation = Quaternion.Euler(0, directionAngle, 0);

        if (isSelected) SetHighlight(selectedColor);
        else ClearHighlight();
    }

    Bounds BuildProceduralFallback(Transform parent, string type, float scale)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");

        switch (type)
        {
            case "ReachStacker":
            {
                float w = 4.2f * scale, h = 4.5f * scale, l = 11.0f * scale;
                // Chassis
                CreatePart(parent, "Chassis", new Vector3(0, 1.0f * scale, 0), new Vector3(w, 1.4f * scale, l * 0.75f), new Color(0.85f, 0.65f, 0.1f), s);
                // Cabin
                CreatePart(parent, "Cabin", new Vector3(0, 2.4f * scale, -0.5f * scale), new Vector3(w * 0.65f, 1.6f * scale, 2.8f * scale), new Color(0.2f, 0.22f, 0.25f), s);
                // Boom arm
                GameObject boom = CreatePart(parent, "Boom", new Vector3(0, 3.2f * scale, 1.2f * scale), new Vector3(1.2f * scale, 1.0f * scale, l * 0.85f), new Color(0.95f, 0.72f, 0.08f), s);
                boom.transform.localRotation = Quaternion.Euler(-18f, 0, 0);
                // Spreader depan
                CreatePart(parent, "Spreader", new Vector3(0, 3.8f * scale, l * 0.45f), new Vector3(6.0f * scale, 0.6f * scale, 1.4f * scale), new Color(0.15f, 0.15f, 0.18f), s);
                return new Bounds(new Vector3(0, h * 0.5f, 0), new Vector3(6.0f * scale, h, l));
            }
            case "TopLoader":
            {
                float w = 4.0f * scale, h = 5.5f * scale, l = 9.0f * scale;
                // Body
                CreatePart(parent, "Body", new Vector3(0, 1.1f * scale, -0.8f * scale), new Vector3(w, 1.6f * scale, l * 0.75f), new Color(0.9f, 0.42f, 0.12f), s);
                // Cabin
                CreatePart(parent, "Cabin", new Vector3(0, 2.5f * scale, -0.5f * scale), new Vector3(w * 0.6f, 1.5f * scale, 2.4f * scale), new Color(0.2f, 0.22f, 0.25f), s);
                // Vertical Mast
                CreatePart(parent, "Mast", new Vector3(0, h * 0.5f, l * 0.32f), new Vector3(w * 0.55f, h, 0.7f * scale), new Color(0.18f, 0.18f, 0.2f), s);
                // Spreader
                CreatePart(parent, "Spreader", new Vector3(0, h * 0.75f, l * 0.42f), new Vector3(6.0f * scale, 0.6f * scale, 1.2f * scale), new Color(0.95f, 0.75f, 0.1f), s);
                return new Bounds(new Vector3(0, h * 0.5f, 0), new Vector3(6.0f * scale, h, l));
            }
            case "Gate":
            {
                float w = 18.0f * scale, h = 6.5f * scale, l = 6.0f * scale;
                // Tiang kiri & kanan & tengah
                CreatePart(parent, "PillarL", new Vector3(-w * 0.42f, h * 0.45f, 0), new Vector3(1.2f * scale, h * 0.9f, 2.5f * scale), new Color(0.75f, 0.78f, 0.82f), s);
                CreatePart(parent, "PillarR", new Vector3(w * 0.42f, h * 0.45f, 0), new Vector3(1.2f * scale, h * 0.9f, 2.5f * scale), new Color(0.75f, 0.78f, 0.82f), s);
                CreatePart(parent, "BoothCenter", new Vector3(0, 1.8f * scale, 0), new Vector3(2.2f * scale, 3.6f * scale, 4.0f * scale), new Color(0.25f, 0.45f, 0.7f), s);
                // Atap Kanopi Gate
                CreatePart(parent, "Canopy", new Vector3(0, h - 0.4f * scale, 0), new Vector3(w, 0.8f * scale, l), new Color(0.15f, 0.38f, 0.68f), s);
                return new Bounds(new Vector3(0, h * 0.5f, 0), new Vector3(w, h, l));
            }
            case "Pagar":
            {
                float w = 0.6f * scale, h = 2.6f * scale, l = 20.0f * scale;
                // Panel pagar memanjang di sumbu Z
                CreatePart(parent, "FenceBase", new Vector3(0, 0.25f * scale, 0), new Vector3(w, 0.5f * scale, l), new Color(0.65f, 0.67f, 0.7f), s);
                CreatePart(parent, "FenceRailTop", new Vector3(0, h - 0.2f * scale, 0), new Vector3(w * 0.4f, 0.15f * scale, l), new Color(0.35f, 0.4f, 0.45f), s);
                CreatePart(parent, "FenceRailMid", new Vector3(0, h * 0.55f, 0), new Vector3(w * 0.4f, 0.15f * scale, l), new Color(0.35f, 0.4f, 0.45f), s);
                int postCount = 6;
                for (int i = 0; i <= postCount; i++)
                {
                    float z = (-l * 0.5f) + (i * (l / postCount));
                    CreatePart(parent, "Post_" + i, new Vector3(0, h * 0.5f, z), new Vector3(w * 0.6f, h, w * 0.6f), new Color(0.3f, 0.35f, 0.4f), s);
                }
                return new Bounds(new Vector3(0, h * 0.5f, 0), new Vector3(Mathf.Max(1.5f, w), h, l));
            }
            default: // "Gedung"
            {
                float w = 24.0f * scale, h = 10.0f * scale, l = 14.0f * scale;
                // Bangunan utama
                CreatePart(parent, "MainBuilding", new Vector3(0, h * 0.5f, 0), new Vector3(w, h, l), new Color(0.78f, 0.80f, 0.84f), s);
                // Kaca jendela fasad
                CreatePart(parent, "GlassFront", new Vector3(0, h * 0.55f, -l * 0.5f - 0.05f), new Vector3(w * 0.85f, h * 0.55f, 0.1f), new Color(0.2f, 0.45f, 0.7f), s);
                // Atap list biru
                CreatePart(parent, "RoofTrim", new Vector3(0, h + 0.3f * scale, 0), new Vector3(w + 0.6f * scale, 0.6f * scale, l + 0.6f * scale), new Color(0.18f, 0.38f, 0.65f), s);
                return new Bounds(new Vector3(0, h * 0.5f, 0), new Vector3(w, h + 0.6f * scale, l));
            }
        }
    }

    GameObject CreatePart(Transform parent, string name, Vector3 localPos, Vector3 localScale, Color col, Shader customShader = null)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;

        Collider c = go.GetComponent<Collider>();
        if (c != null)
        {
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }

        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            if (customShader != null)
            {
                Material m = new Material(customShader);
                m.color = col;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
                if (m.HasProperty("_Color")) m.SetColor("_Color", col);
                r.sharedMaterial = m;
            }
            else
            {
                r.sharedMaterial = CreateSafeMaterial(col, false);
            }
        }
        return go;
    }

    void CreateSelectionHighlightBox(Bounds b)
    {
        selectionOutlineObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        selectionOutlineObj.name = "SelectionHighlight";
        selectionOutlineObj.transform.SetParent(transform, false);
        selectionOutlineObj.transform.localPosition = new Vector3(0, 0.15f, 0);
        float maxBoxDim = accessoryType == "Gedung" ? 45f : 150f;
        float boxX = Mathf.Clamp(b.size.x + 1.2f, 2f, maxBoxDim);
        float boxZ = Mathf.Clamp(b.size.z + 1.2f, 2f, maxBoxDim);
        selectionOutlineObj.transform.localScale = new Vector3(boxX, 0.25f, boxZ);

        Collider c = selectionOutlineObj.GetComponent<Collider>();
        if (c != null)
        {
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }

        Renderer r = selectionOutlineObj.GetComponent<Renderer>();
        if (r != null)
        {
            r.sharedMaterial = CreateSafeMaterial(selectedColor, true);
        }
        selectionOutlineObj.SetActive(false);
    }

    /// <summary>
    /// Menyusun Pagar secara memanjang sepanjang sumbu Z (mirip sistem Road khusus Length).
    /// Jika customFbx ada di Inspector, segmen pagar di-tile/diulang rapat sepanjang length.
    /// </summary>
    Bounds BuildPagarAlongLength(Transform parent, GameObject customFbx, float length, float scaleMult = 1f)
    {
        length = Mathf.Clamp(length, 5f, 1500f);
        if (scaleMult <= 0.05f) scaleMult = 1f;

        if (customFbx != null)
        {
            try
            {
                Vector3 baseScale = customFbx.transform.localScale * scaleMult;

                Quaternion savedParentRot = parent.rotation;
                parent.rotation = Quaternion.identity;

                GameObject dummy = Instantiate(customFbx, parent);
                dummy.transform.localPosition = Vector3.zero;
                dummy.transform.localRotation = customFbx.transform.localRotation;
                dummy.transform.localScale = baseScale;

                Renderer[] dummyRends = dummy.GetComponentsInChildren<Renderer>(true);
                bool longAxisIsX = true;
                float unitLen = 2.5f;
                float unitThick = 0.5f;
                float unitHeight = 2.6f;
                float localMinY = 0f;

                if (dummyRends != null && dummyRends.Length > 0)
                {
                    Bounds db = dummyRends[0].bounds;
                    for (int i = 1; i < dummyRends.Length; i++)
                    {
                        if (dummyRends[i] != null) db.Encapsulate(dummyRends[i].bounds);
                    }

                    longAxisIsX = db.size.x >= db.size.z;
                    unitLen = Mathf.Max(0.5f, longAxisIsX ? db.size.x : db.size.z);
                    unitThick = Mathf.Max(0.3f, longAxisIsX ? db.size.z : db.size.x);
                    unitHeight = Mathf.Max(1.0f, db.size.y);
                    localMinY = db.min.y - parent.position.y;
                }

                parent.rotation = savedParentRot;

                if (Application.isPlaying) Destroy(dummy);
                else DestroyImmediate(dummy);

                // Angkat seluruh segmen pagar ke atas agar dasar beton (beton bawah) berada persis di atas permukaan tanah (groundY), tidak tenggelam di bawah aspal
                float groundOffsetY = -localMinY;

                int segCount = Mathf.Max(1, Mathf.RoundToInt(length / unitLen));
                float actualSegLen = length / segCount;
                float stretch = actualSegLen / unitLen;

                for (int i = 0; i < segCount; i++)
                {
                    GameObject seg = Instantiate(customFbx, parent);
                    seg.name = "Pagar_" + i;

                    foreach (Camera c in seg.GetComponentsInChildren<Camera>(true))
                    {
                        if (Application.isPlaying) Destroy(c.gameObject);
                        else DestroyImmediate(c.gameObject);
                    }
                    foreach (Light l in seg.GetComponentsInChildren<Light>(true))
                    {
                        if (Application.isPlaying) Destroy(l.gameObject);
                        else DestroyImmediate(l.gameObject);
                    }

                    Quaternion segRot = longAxisIsX
                        ? Quaternion.Euler(0f, 90f, 0f) * customFbx.transform.localRotation
                        : customFbx.transform.localRotation;
                    seg.transform.localRotation = segRot;

                    seg.transform.localScale = longAxisIsX
                        ? new Vector3(baseScale.x * stretch, baseScale.y, baseScale.z)
                        : new Vector3(baseScale.x, baseScale.y, baseScale.z * stretch);

                    float targetZ = (-length * 0.5f) + ((i + 0.5f) * actualSegLen);
                    seg.transform.localPosition = new Vector3(0f, groundOffsetY, targetZ);
                }

                return new Bounds(
                    new Vector3(0f, unitHeight * 0.5f, 0f),
                    new Vector3(Mathf.Max(2.0f, unitThick), unitHeight, length)
                );
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[YardEditor] Pagar FBX instantiation fallback: " + ex.Message);
            }
        }

        // Fallback procedural Pagar memanjang sepanjang length
        float w = 0.6f, h = 2.6f;
        Color concreteCol = new Color(0.82f, 0.82f, 0.84f);
        Color barCol = new Color(0.55f, 0.25f, 0.2f);

        CreatePart(parent, "FenceBase", new Vector3(0, 0.25f, 0), new Vector3(w, 0.5f, length), concreteCol);
        CreatePart(parent, "FenceRailTop", new Vector3(0, h - 0.2f, 0), new Vector3(w * 0.4f, 0.15f, length), barCol);
        CreatePart(parent, "FenceRailMid", new Vector3(0, h * 0.55f, 0), new Vector3(w * 0.4f, 0.15f, length), barCol);

        int postCount = Mathf.Max(2, Mathf.RoundToInt(length / 3.5f));
        for (int i = 0; i <= postCount; i++)
        {
            float z = (-length * 0.5f) + (i * (length / postCount));
            CreatePart(parent, "Post_" + i, new Vector3(0, h * 0.5f, z), new Vector3(w * 0.6f, h, w * 0.6f), concreteCol);
        }
        return new Bounds(new Vector3(0, h * 0.5f, 0), new Vector3(2.0f, h, length));
    }

    void CreateEditor2DLabel(Bounds b)
    {
        try
        {
            editorLabelObj = new GameObject("AccessoryLabel_" + accessoryId);
            editorLabelObj.transform.SetParent(transform, false);
            editorLabelObj.transform.localPosition = new Vector3(0, b.size.y + 0.6f, 0);
            editorLabelObj.transform.localRotation = Quaternion.Euler(90, 0, 0);

            TMPro.TextMeshPro tmp = editorLabelObj.AddComponent<TMPro.TextMeshPro>();
            if (tmp != null)
            {
                if (accessoryType == "Pagar")
                {
                    tmp.text = "PAGAR #" + accessoryId + " (" + Mathf.RoundToInt(fenceLength) + "m)";
                }
                else
                {
                    tmp.text = GetDisplayName() + " #" + accessoryId;
                }
                tmp.fontSize = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.35f, 6f, 12f);
                tmp.fontStyle = TMPro.FontStyles.Bold;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.color = new Color(1f, 0.95f, 0.3f, 1f);
                try
                {
                    tmp.outlineWidth = 0.25f;
                    tmp.outlineColor = Color.black;
                }
                catch { }
                tmp.rectTransform.sizeDelta = new Vector2(40f, 8f);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[YardEditor] Label creation: " + ex.Message);
        }
    }

    public void Select()
    {
        isSelected = true;
        SetHighlight(selectedColor);
    }

    public void Deselect()
    {
        isSelected = false;
        isDragging = false;
        ClearHighlight();
    }

    public void StartDrag(Vector3 mouseWorldPos)
    {
        isDragging = true;
        Vector3 mPos = new Vector3(mouseWorldPos.x, transform.position.y, mouseWorldPos.z);
        dragOffset = transform.position - mPos;
        SetHighlight(draggingColor);
    }

    public void UpdateDrag(Vector3 mouseWorldPos)
    {
        if (!isDragging) return;
        FindGroundY();
        Vector3 newPos = mouseWorldPos + dragOffset;
        newPos.y = groundY;
        transform.position = newPos;
    }

    public void EndDrag()
    {
        isDragging = false;
        if (isSelected) SetHighlight(selectedColor);
        else ClearHighlight();
    }

    public void RotateDirection()
    {
        directionAngle = (directionAngle + 90f) % 360f;
        transform.rotation = Quaternion.Euler(0, directionAngle, 0);
        Debug.Log("[YardEditor] Accessory " + GetDisplayName() + " #" + accessoryId + " rotated to " + directionAngle + "°");
    }

    public void SetDirection(float angle)
    {
        directionAngle = angle;
        transform.rotation = Quaternion.Euler(0, directionAngle, 0);
    }

    public void AdjustScale(float factorDelta)
    {
        if (accessoryType == "Pagar")
        {
            // Jika Pagar dipilih lalu user klik Scale -/+, panjangkan/pendekkan pagar
            AdjustLength(factorDelta > 0 ? 5f : -5f);
            return;
        }
        scaleMultiplier = Mathf.Clamp(scaleMultiplier + factorDelta, 0.2f, 10f);
        BuildAccessoryVisual();
    }

    public void AdjustLength(float deltaLength)
    {
        fenceLength = Mathf.Clamp(fenceLength + deltaLength, 5f, 1500f);
        BuildAccessoryVisual();
        Debug.Log("[YardEditor] Panjang Pagar #" + accessoryId + " diubah menjadi: " + fenceLength + "m");
    }

    void SetHighlight(Color col)
    {
        if (selectionOutlineObj != null)
        {
            selectionOutlineObj.SetActive(true);
            Renderer r = selectionOutlineObj.GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null)
            {
                r.sharedMaterial.color = col;
                if (r.sharedMaterial.HasProperty("_BaseColor")) r.sharedMaterial.SetColor("_BaseColor", col);
            }
        }
    }

    void ClearHighlight()
    {
        if (selectionOutlineObj != null)
        {
            selectionOutlineObj.SetActive(false);
        }
    }

    public AccessoryPlacement ToPlacement()
    {
        return new AccessoryPlacement(accessoryId, accessoryType, transform.position, directionAngle, scaleMultiplier, fenceLength);
    }
}
