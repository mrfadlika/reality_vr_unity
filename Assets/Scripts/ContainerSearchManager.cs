using System.Collections.Generic;
using UnityEngine;

public class ContainerSearchManager : MonoBehaviour
{
    public static ContainerSearchManager Instance { get; private set; }

    [Header("Search Query")]
    public string searchQuery = "";
    private string lastSearchQuery = "";
    
    private GameObject currentTargetContainer = null;

    [Header("Visual Indicators Parent")]
    private GameObject searchIndicatorsParent;

    // Mode-specific indicator GameObjects
    private GameObject arrow3DObject;
    private GameObject containerHighlightBox;
    private GameObject beaconVRObject;
    private GameObject directionalPointerObject;
    private TMPro.TextMeshPro targetTextTag;

    [Header("Indicator Colors")]
    public Color highlightColor = new Color(1f, 0.84f, 0f, 1f); // Bright Gold/Yellow

    [Header("Custom 3D Model Prefabs (FBX / Blender)")]
    [Tooltip("Drag & drop FBX / Prefab panah buatan Anda sendiri di sini. Jika kosong, akan memakai panah bawaan.")]
    public GameObject customDirectionalArrowPrefab;
    [Tooltip("Drag & drop FBX / Prefab panah menunjuk ke bawah di sini.")]
    public GameObject customDownwardArrowPrefab;

    [Header("FBX Model Rotation Offsets")]
    [Tooltip("Ubah rotasi jika model FBX panah Anda terbalik (Default 180 di Y untuk membalikkan panah).")]
    public Vector3 directionalArrowRotationOffset = new Vector3(0, 180, 0);
    [Tooltip("Rotasi panah menunjuk ke bawah. Default (90,0,0) agar ujung panah mengarah ke tanah.")]
    public Vector3 downwardArrowRotationOffset = new Vector3(90, 0, 0);

    [Header("Downward Arrow Scale")]
    [Tooltip("Ukuran panah Downward di atas tumpukan kontainer. Perbesar jika terlalu kecil.")]
    public float downwardArrowScale = 5f;

    [Header("Custom Search Bar UI (Tarik file PNG ke sini)")]
    [Tooltip("Tarik gambar PNG untuk background bar/wadah pencarian")]
    public Texture2D searchBarBg;
    [Tooltip("Tarik gambar PNG untuk tombol Find (misal 3D capsule Find)")]
    public Texture2D btnFindIcon;
    [Tooltip("Tarik gambar PNG untuk tombol Reset/Clear (misal tombol bulat X)")]
    public Texture2D btnClearIcon;
    [Tooltip("Tarik gambar PNG untuk icon kaca pembesar di sebelah kiri (opsional)")]
    public Texture2D searchIcon;
    [Tooltip("Tarik gambar PNG untuk background kotak ketik teks (opsional)")]
    public Texture2D inputFieldBg;

    [Header("Search Bar Layout Settings")]
    [Tooltip("Ukuran keseluruhan Search Bar (Lebar X, Tinggi Y)")]
    public Vector2 barSize = new Vector2(420f, 52f);
    [Tooltip("Jarak dari tepi bawah layar")]
    public float bottomMargin = 25f;
    [Tooltip("Ukuran tombol Find (Lebar X, Tinggi Y)")]
    public Vector2 findButtonSize = new Vector2(80f, 40f);
    [Tooltip("Ukuran tombol Clear / X (Lebar X, Tinggi Y)")]
    public Vector2 clearButtonSize = new Vector2(40f, 40f);
    [Tooltip("Lebar kotak tempat mengetik teks nomor kontainer")]
    public float inputFieldWidth = 170f;

    [Header("Input Field & Text Colors")]
    [Tooltip("Warna latar belakang kotak input (hanya jika inputFieldBg kosong)")]
    public Color inputBackgroundColor = Color.white;
    [Tooltip("Warna teks yang diketik di dalam kotak pencarian")]
    public Color inputTextColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    [Tooltip("Warna teks label 'Search:' di sebelah kiri")]
    public Color labelTextColor = new Color(0.25f, 0.2f, 0.15f, 1f);

    private Texture2D inputBgTexture;
    private Color lastInputBgColor;

    Texture2D MakeSolidTexture(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Nilai default jika scene belum memiliki layout tersimpan
        if (barSize.x < 200f || barSize.y < 30f) barSize = new Vector2(420f, 52f);
        if (findButtonSize.x < 30f || findButtonSize.y < 20f) findButtonSize = new Vector2(80f, 40f);
        if (clearButtonSize.x < 20f || clearButtonSize.y < 20f) clearButtonSize = new Vector2(40f, 40f);
        if (inputFieldWidth < 50f) inputFieldWidth = 170f;

        // Jika nilai variabel di Inspector tersimpan (0,0,0) dari scene lama, isi otomatis nilai default yang pas
        if (directionalArrowRotationOffset == Vector3.zero)
        {
            directionalArrowRotationOffset = new Vector3(0, 180f, 0);
        }
        if (downwardArrowRotationOffset == Vector3.zero)
        {
            downwardArrowRotationOffset = new Vector3(90f, 0f, 0);
        }
    }

    void Start()
    {
        searchIndicatorsParent = new GameObject("SearchIndicatorsParent");
        CreateIndicatorObjects();
    }

    void CreateIndicatorObjects()
    {
        // 1. 3D Floating Downward Arrow Indicator & Tag
        if (customDownwardArrowPrefab != null)
        {
            arrow3DObject = Instantiate(customDownwardArrowPrefab);
            arrow3DObject.name = "3D_DownwardArrowIndicator";
            arrow3DObject.transform.SetParent(searchIndicatorsParent.transform);
        }
        else if (customDirectionalArrowPrefab != null)
        {
            // Jika user hanya memasukkan 1 FBX panah, otomatis pakai FBX yang sama untuk panah di atas!
            arrow3DObject = Instantiate(customDirectionalArrowPrefab);
            arrow3DObject.name = "3D_DownwardArrowIndicator";
            arrow3DObject.transform.SetParent(searchIndicatorsParent.transform);
        }
        else
        {
            // Fallback: Buat bentuk Panah 3D Menunjuk Ke Bawah (Downward Arrow)
            arrow3DObject = new GameObject("3D_DownwardArrowIndicator");
            arrow3DObject.transform.SetParent(searchIndicatorsParent.transform);
            
            // Batang Panah Vertikal
            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stem.transform.SetParent(arrow3DObject.transform);
            stem.transform.localPosition = new Vector3(0, 1.0f, 0);
            stem.transform.localScale = new Vector3(0.5f, 2.0f, 0.5f);
            stem.GetComponent<Renderer>().material.color = Color.yellow;
            Destroy(stem.GetComponent<Collider>());

            // Kepala Panah Kiri
            GameObject headLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            headLeft.transform.SetParent(arrow3DObject.transform);
            headLeft.transform.localPosition = new Vector3(-0.4f, 0.2f, 0);
            headLeft.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            headLeft.transform.localScale = new Vector3(0.3f, 1.2f, 0.3f);
            headLeft.GetComponent<Renderer>().material.color = Color.yellow;
            Destroy(headLeft.GetComponent<Collider>());

            // Kepala Panah Kanan
            GameObject headRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            headRight.transform.SetParent(arrow3DObject.transform);
            headRight.transform.localPosition = new Vector3(0.4f, 0.2f, 0);
            headRight.transform.localRotation = Quaternion.Euler(0, 0, -45f);
            headRight.transform.localScale = new Vector3(0.3f, 1.2f, 0.3f);
            headRight.GetComponent<Renderer>().material.color = Color.yellow;
            Destroy(headRight.GetComponent<Collider>());
        }

        // Floating Text Tag — TIDAK di-parent ke arrow agar TIDAK ikut membesar saat arrow di-scale!
        GameObject tagObj = new GameObject("TargetTag");
        tagObj.transform.SetParent(searchIndicatorsParent.transform);
        tagObj.transform.localPosition = Vector3.zero;
        targetTextTag = tagObj.AddComponent<TMPro.TextMeshPro>();
        targetTextTag.text = "TARGET";
        targetTextTag.fontSize = 8;
        targetTextTag.alignment = TMPro.TextAlignmentOptions.Center;
        targetTextTag.color = Color.yellow;
        targetTextTag.outlineWidth = 0.25f;
        targetTextTag.outlineColor = Color.black;
        tagObj.SetActive(false);

        arrow3DObject.SetActive(false);

        // 2. Precise Container Highlight Box (Fits exact container dimensions & Tier)
        containerHighlightBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        containerHighlightBox.name = "ContainerHighlightBox";
        containerHighlightBox.transform.SetParent(searchIndicatorsParent.transform);
        
        // Remove collider so it doesn't block raycasts
        Collider col = containerHighlightBox.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer boxRend = containerHighlightBox.GetComponent<Renderer>();
        if (boxRend != null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Standard");

            if (s != null)
            {
                Material highlightMat = new Material(s);
                highlightMat.name = "ContainerHighlight_GoldMat";
                highlightMat.SetFloat("_Surface", 1); // Transparent
                highlightMat.SetFloat("_Blend", 0);   // Alpha blend
                highlightMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                highlightMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                highlightMat.SetInt("_ZWrite", 0);
                highlightMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                highlightMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                Color goldColor = new Color(1f, 0.85f, 0.1f, 0.45f);
                highlightMat.SetColor("_BaseColor", goldColor);
                if (highlightMat.HasProperty("_Color")) highlightMat.SetColor("_Color", goldColor);
                boxRend.material = highlightMat;
            }
            else
            {
                boxRend.material.color = new Color(1f, 0.85f, 0.1f, 0.45f);
            }
        }

        containerHighlightBox.SetActive(false);

        // 3. VR Sky Beacon (Light Pillar) Indicator (Dibuat lebih pendek agar tidak kepanjangan)
        beaconVRObject = new GameObject("VR_SkyBeaconIndicator");
        beaconVRObject.transform.SetParent(searchIndicatorsParent.transform);

        GameObject pillarMesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillarMesh.transform.SetParent(beaconVRObject.transform);
        pillarMesh.transform.localPosition = new Vector3(0, 10f, 0); 
        pillarMesh.transform.localScale = new Vector3(0.8f, 10f, 0.8f); // 20m tinggi pilar (pas & tidak menembus langit)

        Renderer pillarRend = pillarMesh.GetComponent<Renderer>();
        if (pillarRend != null)
        {
            pillarRend.material.color = new Color(0f, 1f, 1f, 0.65f); // Cyan glowing beam
        }

        beaconVRObject.SetActive(false);

        // 4. VR Directional Arrow Pointer (Berbentuk Tanda Panah 3D Presisi)
        if (customDirectionalArrowPrefab != null)
        {
            directionalPointerObject = Instantiate(customDirectionalArrowPrefab);
            directionalPointerObject.name = "VR_DirectionalCompassPointer";
            directionalPointerObject.transform.SetParent(searchIndicatorsParent.transform);
        }
        else
        {
            directionalPointerObject = new GameObject("VR_DirectionalCompassPointer");
            directionalPointerObject.transform.SetParent(searchIndicatorsParent.transform);

            // Batang Panah (Stem)
            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stem.transform.SetParent(directionalPointerObject.transform);
            stem.transform.localPosition = new Vector3(0, 0, -0.3f);
            stem.transform.localScale = new Vector3(0.3f, 0.04f, 1.0f);
            stem.GetComponent<Renderer>().material.color = Color.cyan;
            Destroy(stem.GetComponent<Collider>());

            // Sayap Kiri Kepala Panah (Arrowhead Left)
            GameObject headLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            headLeft.transform.SetParent(directionalPointerObject.transform);
            headLeft.transform.localPosition = new Vector3(-0.2f, 0, 0.3f);
            headLeft.transform.localRotation = Quaternion.Euler(0, 35f, 0);
            headLeft.transform.localScale = new Vector3(0.12f, 0.04f, 0.7f);
            headLeft.GetComponent<Renderer>().material.color = Color.cyan;
            Destroy(headLeft.GetComponent<Collider>());

            // Sayap Kanan Kepala Panah (Arrowhead Right)
            GameObject headRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            headRight.transform.SetParent(directionalPointerObject.transform);
            headRight.transform.localPosition = new Vector3(0.2f, 0, 0.3f);
            headRight.transform.localRotation = Quaternion.Euler(0, -35f, 0);
            headRight.transform.localScale = new Vector3(0.12f, 0.04f, 0.7f);
            headRight.GetComponent<Renderer>().material.color = Color.cyan;
            Destroy(headRight.GetComponent<Collider>());
        }

        directionalPointerObject.SetActive(false);
    }

    float GetStackMaxY(Transform targetT, Vector3 targetWorldCenter, float targetMaxY)
    {
        float maxY = targetMaxY;
        Renderer[] allRends = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        Quaternion invRot = Quaternion.Inverse(targetT.rotation);

        foreach (var r in allRends)
        {
            if (r == null || r.gameObject == null) continue;
            if (r.gameObject.name.Contains("ft)"))
            {
                Vector3 c = r.bounds.center;
                Vector3 diff = c - targetWorldCenter;
                Vector3 localDiff = invRot * diff;

                // localDiff.x adalah jarak antar-row (lebar kontainer), localDiff.z adalah jarak antar-slot (panjang kontainer)
                // Berfungsi presisi 100% pada rotasi Yard mana pun (Yard A, B, C, dll)
                if (Mathf.Abs(localDiff.x) < 1.8f && Mathf.Abs(localDiff.z) < 4.0f)
                {
                    if (r.bounds.max.y > maxY)
                    {
                        maxY = r.bounds.max.y;
                    }
                }
            }
        }
        return maxY;
    }

    void UpdateDownwardArrowPosition(Vector3 targetCenterPos, Quaternion rotation)
    {
        if (arrow3DObject == null) return;
        arrow3DObject.SetActive(true);
        arrow3DObject.transform.localScale = Vector3.one * downwardArrowScale;
        arrow3DObject.transform.rotation = rotation;
        arrow3DObject.transform.position = targetCenterPos;

        // Kompensasi pivot/mesh offset agar titik tengah mesh visual PAS 100% di X dan Z target
        MeshRenderer[] rends = arrow3DObject.GetComponentsInChildren<MeshRenderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            float offsetX = b.center.x - targetCenterPos.x;
            float offsetZ = b.center.z - targetCenterPos.z;

            arrow3DObject.transform.position -= new Vector3(offsetX, 0f, offsetZ);
        }
    }

    void UpdateDirectionalPointerPosition(Camera activeCam, Vector3 targetWorldPos)
    {
        if (directionalPointerObject == null || activeCam == null) return;

        directionalPointerObject.SetActive(true);
        directionalPointerObject.transform.SetParent(activeCam.transform);
        directionalPointerObject.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);

        // Hitung arah rotasi menuju target dalam koordinat lokal kamera (bidang horizontal)
        Vector3 camPos = activeCam.transform.position;
        Vector3 dirToTarget = (targetWorldPos - camPos).normalized;
        Vector3 localDir = activeCam.transform.InverseTransformDirection(dirToTarget);
        localDir.y = 0;

        if (localDir.sqrMagnitude > 0.001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(localDir, Vector3.up);
            directionalPointerObject.transform.localRotation = lookRot * Quaternion.Euler(directionalArrowRotationOffset);
        }
        else
        {
            directionalPointerObject.transform.localRotation = Quaternion.Euler(directionalArrowRotationOffset);
        }

        // Posisi awal lokal: tepat di tengah horizontal camera view (x=0), bawah (y=-0.35), depan mata (z=1.3)
        directionalPointerObject.transform.localPosition = new Vector3(0f, -0.35f, 1.3f);

        // Kompensasi pivot mesh FBX agar visual mesh 100% TERLETAK PAS DI TENGAH LAYAR (X=0)
        MeshRenderer[] rends = directionalPointerObject.GetComponentsInChildren<MeshRenderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            Vector3 meshCenterInCam = activeCam.transform.InverseTransformPoint(b.center);
            float offsetX = meshCenterInCam.x - 0f;
            float offsetY = meshCenterInCam.y - (-0.35f);

            directionalPointerObject.transform.localPosition -= new Vector3(offsetX, offsetY, 0f);
        }
    }

    void Update()
    {
        if (currentTargetContainer == null)
        {
            // Sembunyikan SEMUA indikator termasuk teks tag saat tidak ada pencarian aktif!
            if (arrow3DObject != null) arrow3DObject.SetActive(false);
            if (containerHighlightBox != null) containerHighlightBox.SetActive(false);
            if (beaconVRObject != null) beaconVRObject.SetActive(false);
            if (directionalPointerObject != null)
            {
                directionalPointerObject.transform.SetParent(searchIndicatorsParent.transform);
                directionalPointerObject.SetActive(false);
            }
            if (targetTextTag != null) targetTextTag.gameObject.SetActive(false);
            return;
        }

        ContainerOrientedBounds ob = GetContainerOrientedBounds(currentTargetContainer);
        float topStackY = GetStackMaxY(currentTargetContainer.transform, ob.worldCenter, ob.worldMaxY);

        // Gunakan worldCenter sejati dari OBB kontainer (bebas dari distorsi rotasi Yard)
        float preciseX = ob.worldCenter.x;
        float preciseZ = ob.worldCenter.z;

        // Animate indicators based on active View Mode
        CameraViewMode mode = ViewModeManager.Instance != null ? ViewModeManager.Instance.currentMode : CameraViewMode.BirdView3D;
        Camera activeCam = ViewModeManager.Instance != null ? ViewModeManager.Instance.GetActiveCamera() : Camera.main;

        // Extract container number and name for tag
        string ctrName = currentTargetContainer.name;
        if (targetTextTag != null)
        {
            targetTextTag.text = $"<b>{ctrName}</b>";
            targetTextTag.gameObject.SetActive(true);

            float bounceTagY = mode == CameraViewMode.BirdView3D ? Mathf.Sin(Time.time * 4f) * 0.8f : 0f;
            targetTextTag.transform.position = new Vector3(preciseX, topStackY + 8f + bounceTagY, preciseZ);

            if (activeCam != null)
            {
                targetTextTag.transform.rotation = Quaternion.LookRotation(targetTextTag.transform.position - activeCam.transform.position);
            }
        }

        // Hide all indicators first
        if (arrow3DObject != null) arrow3DObject.SetActive(false);
        if (containerHighlightBox != null) containerHighlightBox.SetActive(false);
        if (beaconVRObject != null) beaconVRObject.SetActive(false);
        if (directionalPointerObject != null)
        {
            directionalPointerObject.transform.SetParent(searchIndicatorsParent.transform);
            directionalPointerObject.SetActive(false);
        }

        // Position & Scale the precise container highlight box around target container
        // Orientasi dan ukuran dihitung dalam local space kontainer sehingga tidak akan "melintang" pada rotasi Yard mana pun!
        if (containerHighlightBox != null)
        {
            containerHighlightBox.SetActive(true);
            containerHighlightBox.transform.position = ob.worldCenter;
            containerHighlightBox.transform.rotation = currentTargetContainer.transform.rotation;
            containerHighlightBox.transform.localScale = ob.localSize + new Vector3(0.15f, 0.15f, 0.15f); 
        }

        Quaternion downwardRot = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(downwardArrowRotationOffset);

        switch (mode)
        {
            case CameraViewMode.BirdView3D:
                float bounceY = Mathf.Sin(Time.time * 4f) * 0.8f;
                Vector3 downwardTargetPos3D = new Vector3(preciseX, topStackY + 5f + bounceY, preciseZ);
                UpdateDownwardArrowPosition(downwardTargetPos3D, downwardRot);
                break;

            case CameraViewMode.TopDown2D:
                Vector3 downwardTargetPos2D = new Vector3(preciseX, topStackY + 5f, preciseZ);
                UpdateDownwardArrowPosition(downwardTargetPos2D, downwardRot);
                break;

            case CameraViewMode.VRView:
                Vector3 downwardTargetPosVR = new Vector3(preciseX, topStackY + 5f, preciseZ);
                UpdateDownwardArrowPosition(downwardTargetPosVR, downwardRot);

                if (activeCam != null)
                {
                    UpdateDirectionalPointerPosition(activeCam, ob.worldCenter);
                }
                break;
        }
    }

    public void SearchContainer(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            ClearSearch();
            return;
        }

        query = query.Trim().ToUpper();
        searchQuery = query;

        // Search inside ContainerManager
        ContainerManager cm = Object.FindFirstObjectByType<ContainerManager>();
        if (cm == null) return;

        // Find match in scene objects
        GameObject matchedGo = null;

        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.name.ToUpper().Contains(query))
            {
                matchedGo = go;
                break;
            }
        }

        if (matchedGo != null)
        {
            currentTargetContainer = matchedGo;
            Debug.Log("Found Container: " + matchedGo.name);

            // Focus Camera on center of container
            ContainerOrientedBounds ob = GetContainerOrientedBounds(matchedGo);
            FocusCameraOnTarget(ob.worldCenter);
        }
        else
        {
            Debug.LogWarning("Container not found for query: " + query);
        }
    }

    public void FocusCameraOnTarget(Vector3 targetPos)
    {
        if (ViewModeManager.Instance == null) return;

        CameraViewMode mode = ViewModeManager.Instance.currentMode;
        if (mode == CameraViewMode.BirdView3D)
        {
            OrbitCamera orbitCam = Object.FindFirstObjectByType<OrbitCamera>();
            if (orbitCam != null)
            {
                orbitCam.FocusOnPosition(targetPos, 35f);
            }
        }
        else if (mode == CameraViewMode.TopDown2D)
        {
            ViewModeManager.Instance.FocusTarget2D(targetPos);
        }
        else if (mode == CameraViewMode.VRView)
        {
            // Move VR rig near the container (8 meters away at floor Y)
            GameObject vrRig = ViewModeManager.Instance.vrPlayerRig;
            if (vrRig != null)
            {
                Vector3 dest = targetPos - vrRig.transform.forward * 8f;
                dest.y = targetPos.y; // Match container floor level (Y = 19f)
                vrRig.transform.position = dest;
            }
        }
    }

    public void ClearSearch()
    {
        searchQuery = "";
        lastSearchQuery = "";
        currentTargetContainer = null;

        if (directionalPointerObject != null)
        {
            directionalPointerObject.transform.SetParent(searchIndicatorsParent.transform);
            directionalPointerObject.SetActive(false);
        }

        if (arrow3DObject != null) arrow3DObject.SetActive(false);
        if (containerHighlightBox != null) containerHighlightBox.SetActive(false);
        if (beaconVRObject != null) beaconVRObject.SetActive(false);
    }

    public struct ContainerOrientedBounds
    {
        public Vector3 worldCenter;
        public Vector3 localSize;
        public float worldMaxY;
    }

    public ContainerOrientedBounds GetContainerOrientedBounds(GameObject go)
    {
        ContainerOrientedBounds result = new ContainerOrientedBounds();
        if (go == null) return result;

        Transform rootT = go.transform;

        // Ambil semua MeshRenderer anak, tetapi abaikan dekorasi markings/teks/border
        MeshRenderer[] allRends = go.GetComponentsInChildren<MeshRenderer>();
        List<MeshRenderer> bodyRends = new List<MeshRenderer>();
        foreach (var mr in allRends)
        {
            if (mr == null) continue;
            string n = mr.gameObject.name;
            Transform p = mr.transform.parent;
            if (n.Contains("Border") || n.Contains("Marking") || n.Contains("CheckDigit") || n.Contains("DigitText") || (p != null && (p.name.Contains("Marking") || p.name.Contains("CheckDigit"))))
            {
                continue;
            }
            bodyRends.Add(mr);
        }

        if (bodyRends.Count == 0)
        {
            bodyRends.AddRange(allRends);
        }

        if (bodyRends.Count == 0)
        {
            result.worldCenter = rootT.position;
            result.localSize = new Vector3(2.44f, 2.59f, 12.19f);
            result.worldMaxY = rootT.position.y + 2.59f;
            return result;
        }

        Vector3 minPt = Vector3.zero;
        Vector3 maxPt = Vector3.zero;
        float maxY = float.MinValue;
        bool firstPoint = true;

        Quaternion invRot = Quaternion.Inverse(rootT.rotation);
        Vector3 rootPos = rootT.position;

        foreach (var mr in bodyRends)
        {
            MeshFilter mf = mr.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds mb = mf.sharedMesh.bounds;
                Vector3 bMin = mb.min;
                Vector3 bMax = mb.max;

                Vector3[] localCorners = new Vector3[8]
                {
                    new Vector3(bMin.x, bMin.y, bMin.z),
                    new Vector3(bMin.x, bMin.y, bMax.z),
                    new Vector3(bMin.x, bMax.y, bMin.z),
                    new Vector3(bMin.x, bMax.y, bMax.z),
                    new Vector3(bMax.x, bMin.y, bMin.z),
                    new Vector3(bMax.x, bMin.y, bMax.z),
                    new Vector3(bMax.x, bMax.y, bMin.z),
                    new Vector3(bMax.x, bMax.y, bMax.z)
                };

                for (int i = 0; i < 8; i++)
                {
                    Vector3 worldPt = mf.transform.TransformPoint(localCorners[i]);
                    if (worldPt.y > maxY) maxY = worldPt.y;

                    Vector3 orientedPt = invRot * (worldPt - rootPos);
                    if (firstPoint)
                    {
                        minPt = orientedPt;
                        maxPt = orientedPt;
                        firstPoint = false;
                    }
                    else
                    {
                        minPt = Vector3.Min(minPt, orientedPt);
                        maxPt = Vector3.Max(maxPt, orientedPt);
                    }
                }
            }
            else
            {
                Bounds wb = mr.bounds;
                if (wb.max.y > maxY) maxY = wb.max.y;

                Vector3 bMin = wb.min;
                Vector3 bMax = wb.max;
                Vector3[] worldCorners = new Vector3[8]
                {
                    new Vector3(bMin.x, bMin.y, bMin.z),
                    new Vector3(bMin.x, bMin.y, bMax.z),
                    new Vector3(bMin.x, bMax.y, bMin.z),
                    new Vector3(bMin.x, bMax.y, bMax.z),
                    new Vector3(bMax.x, bMin.y, bMin.z),
                    new Vector3(bMax.x, bMin.y, bMax.z),
                    new Vector3(bMax.x, bMax.y, bMin.z),
                    new Vector3(bMax.x, bMax.y, bMax.z)
                };

                for (int i = 0; i < 8; i++)
                {
                    Vector3 orientedPt = invRot * (worldCorners[i] - rootPos);
                    if (firstPoint)
                    {
                        minPt = orientedPt;
                        maxPt = orientedPt;
                        firstPoint = false;
                    }
                    else
                    {
                        minPt = Vector3.Min(minPt, orientedPt);
                        maxPt = Vector3.Max(maxPt, orientedPt);
                    }
                }
            }
        }

        Vector3 orientedCenter = (minPt + maxPt) * 0.5f;
        result.localSize = maxPt - minPt;
        if (result.localSize.x < 0.1f) result.localSize.x = 2.44f;
        if (result.localSize.y < 0.1f) result.localSize.y = 2.59f;
        if (result.localSize.z < 0.1f) result.localSize.z = 6.06f;

        result.worldCenter = rootPos + (rootT.rotation * orientedCenter);
        result.worldMaxY = maxY;

        return result;
    }

    public Bounds GetContainerBounds(GameObject go)
    {
        MeshRenderer[] rends = go.GetComponentsInChildren<MeshRenderer>();
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one * 3f);

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    void OnGUI()
    {
        float barWidth = barSize.x;
        float barHeight = barSize.y;
        float x = (Screen.width - barWidth) / 2f;
        float y = Screen.height - barHeight - bottomMargin;

        Rect barRect = new Rect(x, y, barWidth, barHeight);

        // 1. Gambar Background Bar
        if (searchBarBg != null)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(barRect, searchBarBg, ScaleMode.StretchToFill, true);
        }
        else
        {
            GUI.color = new Color(0.12f, 0.12f, 0.14f, 0.90f);
            GUI.Box(new Rect(x - 5, y - 5, barWidth + 10, barHeight + 10), "");
            GUI.color = Color.white;
        }

        // 2. Icon / Label Search di Kiri
        float curX = x + 15f;
        float centerY = y + (barHeight - 28f) / 2f;

        if (searchIcon != null)
        {
            float iconSize = barHeight * 0.65f;
            Rect iconRect = new Rect(curX, y + (barHeight - iconSize) / 2f, iconSize, iconSize);
            GUI.DrawTexture(iconRect, searchIcon, ScaleMode.ScaleToFit, true);
            curX += iconSize + 10f;
        }
        else
        {
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 13;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.normal.textColor = labelTextColor;
            GUI.Label(new Rect(curX, centerY, 75f, 28f), "🔍 Search:", labelStyle);
            curX += 75f;
        }

        // 3. Input Text Box
        float inputW = inputFieldWidth > 50f ? inputFieldWidth : 170f;
        float inputH = Mathf.Clamp(barHeight * 0.65f, 28f, 38f);
        Rect inputRect = new Rect(curX, y + (barHeight - inputH) / 2f, inputW, inputH);

        // A. Gambar texture kustom jika ada
        if (inputFieldBg != null)
        {
            GUI.DrawTexture(inputRect, inputFieldBg, ScaleMode.StretchToFill, true);
        }
        else if (searchBarBg == null)
        {
            // Hanya gambar background jika tidak ada background bar kustom
            if (inputBgTexture == null || lastInputBgColor != inputBackgroundColor)
            {
                lastInputBgColor = inputBackgroundColor;
                inputBgTexture = MakeSolidTexture(2, 2, inputBackgroundColor);
            }
            GUI.DrawTexture(inputRect, inputBgTexture, ScaleMode.StretchToFill, true);
        }

        // B. Text field 100% transparan (GUIStyle.none) - HANYA teks dan kursor, TANPA kotak gelap bawaan Unity!
        GUIStyle tfStyle = new GUIStyle(GUIStyle.none);
        tfStyle.alignment = TextAnchor.MiddleLeft;
        tfStyle.fontSize = 14;
        tfStyle.padding = new RectOffset(8, 8, 2, 2);
        tfStyle.normal.textColor = inputTextColor;
        tfStyle.focused.textColor = inputTextColor;
        tfStyle.hover.textColor = inputTextColor;
        tfStyle.active.textColor = inputTextColor;

        searchQuery = GUI.TextField(inputRect, searchQuery, 30, tfStyle);
        curX += inputW + 12f;

        // 4. Tombol Find
        float findW = findButtonSize.x;
        float findH = findButtonSize.y;
        Rect findRect = new Rect(curX, y + (barHeight - findH) / 2f, findW, findH);

        bool triggerFind = false;
        if (btnFindIcon != null)
        {
            bool isFindHover = findRect.Contains(Event.current.mousePosition);
            Color prev = GUI.color;
            GUI.color = isFindHover ? Color.white : new Color(0.92f, 0.92f, 0.92f, 0.92f);
            GUI.DrawTexture(findRect, btnFindIcon, ScaleMode.ScaleToFit, true);
            GUI.color = prev;

            if (GUI.Button(findRect, GUIContent.none, GUIStyle.none)) triggerFind = true;
        }
        else
        {
            if (GUI.Button(findRect, "Find")) triggerFind = true;
        }

        if (triggerFind || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && searchQuery != lastSearchQuery))
        {
            lastSearchQuery = searchQuery;
            SearchContainer(searchQuery);
        }
        curX += findW + 8f;

        // 5. Tombol Clear / X
        float clearW = clearButtonSize.x;
        float clearH = clearButtonSize.y;
        Rect clearRect = new Rect(curX, y + (barHeight - clearH) / 2f, clearW, clearH);

        if (btnClearIcon != null)
        {
            bool isClearHover = clearRect.Contains(Event.current.mousePosition);
            Color prev = GUI.color;
            GUI.color = isClearHover ? Color.white : new Color(0.92f, 0.92f, 0.92f, 0.92f);
            GUI.DrawTexture(clearRect, btnClearIcon, ScaleMode.ScaleToFit, true);
            GUI.color = prev;

            if (GUI.Button(clearRect, GUIContent.none, GUIStyle.none))
            {
                ClearSearch();
            }
        }
        else
        {
            if (GUI.Button(clearRect, "X"))
            {
                ClearSearch();
            }
        }

        // Display Found Target Banner if active
        if (currentTargetContainer != null)
        {
            GUI.color = Color.yellow;
            GUI.Box(new Rect(15, Screen.height - 70, 280, 50), "🎯 TARGET FOUND:\n" + currentTargetContainer.name);
            GUI.color = Color.white;
        }
    }
}
