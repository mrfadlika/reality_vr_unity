using UnityEngine;

// ============================================================
// DraggableRoad.cs
// Komponen untuk road segment: drag, resize, directional arrow
// ============================================================

public class DraggableRoad : MonoBehaviour
{
    [Header("Road Info")]
    public int roadId;
    public float roadWidth = 12f;
    public float roadLength = 50f;

    [Header("Direction")]
    [Tooltip("Rotasi arah jalan: 0, 90, 180, 270")]
    public float directionAngle = 0f;

    [Header("Visual")]
    public Color normalColor = new Color(0.35f, 0.35f, 0.38f, 1f);
    public Color selectedColor = new Color(0.3f, 0.7f, 1f, 1f);
    public Color draggingColor = new Color(0.2f, 0.9f, 0.4f, 1f);

    // State
    [HideInInspector] public bool isSelected = false;
    [HideInInspector] public bool isDragging = false;

    private Vector3 dragOffset;
    private float groundY = 19.81f;

    // Visual components
    private GameObject roadSurface;
    private GameObject arrowIndicator;
    private GameObject labelObj;
    private Material roadMaterial;

    // Dashed line markings
    private GameObject dashContainer;

    void Start()
    {
        FindGroundY();
        if (transform.position.y < 19.5f || transform.position.y > 19.95f)
        {
            Vector3 pos = transform.position;
            pos.y = groundY;
            transform.position = pos;
        }
        if (roadSurface == null && transform.childCount == 0)
        {
            BuildRoadVisual();
        }
    }

    void Update()
    {
        // Sembunyikan label teks & tanda panah saat keluar dari mode Yard Editor (misal di 3D View atau Walk View)
        bool inEditor = YardEditorManager.Instance != null && YardEditorManager.Instance.isEditorActive;
        if (!inEditor && isSelected)
        {
            Deselect();
        }
        if (labelObj != null && labelObj.activeSelf != inEditor)
        {
            labelObj.SetActive(inEditor);
        }
        if (arrowIndicator != null && arrowIndicator.activeSelf != inEditor)
        {
            arrowIndicator.SetActive(inEditor);
        }
    }

    void FindGroundY()
    {
        if (YardEditorManager.Instance != null && YardEditorManager.Instance.groundY > 19.5f && YardEditorManager.Instance.groundY < 19.95f)
        {
            groundY = YardEditorManager.Instance.groundY;
            return;
        }
        groundY = 19.81f;
    }

    // --- Build Visual ---

    public void BuildRoadVisual()
    {
        // Cleanup existing
        ClearChildren();

        float roadThickness = 0.20f; // Tebal 20cm solid slab aspal agar berdiri kokoh di atas paving vr-env dan tidak tenggelam di bawah lantai

        // 0. Pasang BoxCollider utama di root object LEBIH DULU agar raycast click & drag 100% selalu berfungsi di Build maupun Editor
        BoxCollider rootCol = GetComponent<BoxCollider>();
        if (rootCol == null) rootCol = gameObject.AddComponent<BoxCollider>();
        rootCol.center = new Vector3(0, roadThickness / 2f + 0.5f, 0);
        rootCol.size = new Vector3(roadWidth, 2.5f, roadLength);
        rootCol.isTrigger = true;

        GameObject customRoadPrefab = YardEditorManager.Instance != null ? YardEditorManager.Instance.roadPrefab : null;
        if (customRoadPrefab != null)
        {
            GameObject instance = Instantiate(customRoadPrefab, transform);
            instance.name = "CustomRoadModel";
            instance.transform.localPosition = new Vector3(0, 0.02f, 0);
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = new Vector3(roadWidth, 1f, roadLength);

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
        }
        else
        {
            // 1. Road surface (solid 3D Dark Asphalt Cube)
            roadSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roadSurface.name = "RoadSurface";
            roadSurface.transform.SetParent(transform);
            roadSurface.transform.localPosition = new Vector3(0, roadThickness / 2f, 0);
            roadSurface.transform.localRotation = Quaternion.identity;
            roadSurface.transform.localScale = new Vector3(roadWidth, roadThickness, roadLength);

            // Simpan default shader dari primitive Cube sebelum diganti materialnya (dijamin selalu valid di WebGL/Standalone build)
            Renderer surfaceRend = roadSurface.GetComponent<Renderer>();
            if (surfaceRend != null && surfaceRend.sharedMaterial != null && fallbackPrimitiveShader == null)
            {
                fallbackPrimitiveShader = surfaceRend.sharedMaterial.shader;
            }

            // Material aspal gelap
            roadMaterial = (YardEditorManager.Instance != null && YardEditorManager.Instance.roadMaterial != null)
                ? new Material(YardEditorManager.Instance.roadMaterial)
                : CreateRoadMaterial();
            if (surfaceRend != null) surfaceRend.sharedMaterial = roadMaterial;

            // Jadikan collider child sebagai trigger juga (bukan di-destroy) agar raycast klik di Build 100% mengenai jalan
            Collider childCol = roadSurface.GetComponent<Collider>();
            if (childCol != null) childCol.isTrigger = true;

            // 2. Yellow side edge lines tidak diperlukan pada jalan (sesuai permintaan: "jalan itu gak perlu yellowedgemarking")
            // Hanya gunakan aspal gelap polos dengan garis marka putus-putus putih di tengah

            // 3. Dashed center line
            CreateDashedLine(roadThickness);
        }

        // 4. Directional arrow
        CreateArrow(roadThickness);

        // 5. Label
        CreateLabel(roadThickness);

        // Terapkan warna sesuai status seleksi saat ini
        if (isDragging) ApplyColorToSurface(draggingColor);
        else if (isSelected) ApplyColorToSurface(selectedColor);
        else ApplyColorToSurface(normalColor);

        bool inEditor = YardEditorManager.Instance != null && YardEditorManager.Instance.isEditorActive;
        if (labelObj != null) labelObj.SetActive(inEditor);
        if (arrowIndicator != null) arrowIndicator.SetActive(inEditor);

        Physics.SyncTransforms();
    }

    void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    private static Shader fallbackPrimitiveShader;

    Shader GetSafeRoadShader(bool preferUnlit)
    {
        Shader s = null;
        if (preferUnlit)
        {
            s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Unlit/Color");
        }
        if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Sprites/Default");
        if (s == null) s = Shader.Find("Standard");
        if (s == null) s = fallbackPrimitiveShader;
        return s;
    }

    Material CreateSafeRoadMaterial(Color col, bool preferUnlit, int queueOffset, string matName)
    {
        Shader s = GetSafeRoadShader(preferUnlit);
        Material mat;
        if (s != null)
        {
            mat = new Material(s);
        }
        else if (roadSurface != null && roadSurface.GetComponent<Renderer>() != null && roadSurface.GetComponent<Renderer>().sharedMaterial != null)
        {
            mat = new Material(roadSurface.GetComponent<Renderer>().sharedMaterial);
        }
        else
        {
            mat = new Material(Shader.Find("Sprites/Default"));
        }

        mat.name = matName;
        mat.color = col;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
        if (preferUnlit && mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", col * 0.45f);
        }
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + queueOffset;
        return mat;
    }

    Material CreateRoadMaterial()
    {
        Color asphalt = normalColor; // Dark industrial asphalt
        return CreateSafeRoadMaterial(asphalt, false, 10, "RoadAsphalt_" + roadId);
    }

    private Material cachedYellowMat;
    private Material GetYellowBorderMaterial()
    {
        if (cachedYellowMat == null)
        {
            Color brightYellow = new Color(1.0f, 0.88f, 0.0f, 1.0f);
            cachedYellowMat = CreateSafeRoadMaterial(brightYellow, true, 20, "RoadYellowMat");
        }
        return cachedYellowMat;
    }

    private Material cachedWhiteMat;
    private Material GetWhiteDashMaterial()
    {
        if (cachedWhiteMat == null)
        {
            Color pureWhite = new Color(1.0f, 1.0f, 1.0f, 1.0f);
            cachedWhiteMat = CreateSafeRoadMaterial(pureWhite, true, 30, "RoadWhiteDashMat");
        }
        return cachedWhiteMat;
    }

    void RemoveChildColliderSafe(GameObject obj)
    {
        if (obj == null) return;
        Collider col = obj.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }
    }

    void CreateYellowEdgeMarkings(float roadThickness)
    {
        // Jalan tidak memerlukan garis tepi kuning (yellow edge markings)
    }

    void CreateDashedLine(float roadThickness)
    {
        dashContainer = new GameObject("DashLines");
        dashContainer.transform.SetParent(transform);
        dashContainer.transform.localPosition = Vector3.zero;
        dashContainer.transform.localRotation = Quaternion.identity;

        Material dashMat = GetWhiteDashMaterial();

        float dashLen = 3.5f;   // Panjang strip marka 3.5 meter
        float gapLen = 2.5f;    // Jarak sela antar marka 2.5 meter
        float step = dashLen + gapLen;
        float lineWidth = 0.45f; // Lebar garis putih 45cm (terlihat sangat kontras dan jelas dari jauh)
        float lineHeight = 0.025f; // Tebal strip 2.5cm di atas permukaan aspal
        float lineY = roadThickness + (lineHeight / 2f) + 0.003f;

        for (float dist = -roadLength / 2f + 1.0f; dist < roadLength / 2f - 1.0f; dist += step)
        {
            float actualLen = Mathf.Min(dashLen, (roadLength / 2f - 1.0f) - dist);
            if (actualLen <= 0.1f) break;

            GameObject dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dash.name = "Dash";
            dash.transform.SetParent(dashContainer.transform);
            dash.transform.localPosition = new Vector3(0, lineY, dist + actualLen / 2f);
            dash.transform.localRotation = Quaternion.identity;
            dash.transform.localScale = new Vector3(lineWidth, lineHeight, actualLen);

            RemoveChildColliderSafe(dash);

            Renderer rend = dash.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = dashMat;
        }
    }

    void CreateArrow(float roadThickness)
    {
        arrowIndicator = new GameObject("DirectionArrow");
        arrowIndicator.transform.SetParent(transform);
        arrowIndicator.transform.localRotation = Quaternion.identity;

        float arrowHeight = 0.025f;
        float lineY = roadThickness + (arrowHeight / 2f) + 0.005f;
        arrowIndicator.transform.localPosition = new Vector3(0, lineY, 0);

        Color arrowColor = new Color(0.1f, 0.85f, 0.3f, 1f);
        Material arrowMat = CreateSafeRoadMaterial(arrowColor, true, 40, "ArrowMat_Road_" + roadId);

        // Arrow body
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "ArrowBody";
        body.transform.SetParent(arrowIndicator.transform);
        body.transform.localPosition = new Vector3(0, 0, -1.2f);
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = new Vector3(0.6f, arrowHeight, 3.5f);
        RemoveChildColliderSafe(body);
        body.GetComponent<Renderer>().sharedMaterial = arrowMat;

        // Arrow head - left wing
        GameObject leftWing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftWing.name = "ArrowWingL";
        leftWing.transform.SetParent(arrowIndicator.transform);
        leftWing.transform.localPosition = new Vector3(-0.8f, 0, 0.8f);
        leftWing.transform.localRotation = Quaternion.Euler(0, -40, 0);
        leftWing.transform.localScale = new Vector3(0.6f, arrowHeight, 2.2f);
        RemoveChildColliderSafe(leftWing);
        leftWing.GetComponent<Renderer>().sharedMaterial = arrowMat;

        // Arrow head - right wing
        GameObject rightWing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWing.name = "ArrowWingR";
        rightWing.transform.SetParent(arrowIndicator.transform);
        rightWing.transform.localPosition = new Vector3(0.8f, 0, 0.8f);
        rightWing.transform.localRotation = Quaternion.Euler(0, 40, 0);
        rightWing.transform.localScale = new Vector3(0.6f, arrowHeight, 2.2f);
        RemoveChildColliderSafe(rightWing);
        rightWing.GetComponent<Renderer>().sharedMaterial = arrowMat;
    }

    void CreateLabel(float roadThickness)
    {
        try
        {
            labelObj = new GameObject("RoadLabel_" + roadId);
            labelObj.transform.SetParent(transform);

            float lineY = roadThickness + 0.04f;
            labelObj.transform.localPosition = new Vector3(0, lineY, -roadLength / 2f + 3f);
            labelObj.transform.localRotation = Quaternion.Euler(90, 0, 0);

            TMPro.TextMeshPro tmp = labelObj.AddComponent<TMPro.TextMeshPro>();
            tmp.text = "ROAD " + roadId + " (" + roadWidth + "m x " + roadLength + "m)";
            tmp.fontSize = 12;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = new Color(0.3f, 0.85f, 1f, 1f);
            tmp.rectTransform.sizeDelta = new Vector2(25f, 6f);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[DraggableRoad] Label warning: " + ex.Message);
        }
    }

    // --- Selection ---

    public void Select()
    {
        isSelected = true;
        ApplyColorToSurface(selectedColor);
    }

    public void Deselect()
    {
        isSelected = false;
        isDragging = false;
        ApplyColorToSurface(normalColor);
    }

    // --- Dragging ---

    public void StartDrag(Vector3 mouseWorldPos)
    {
        isDragging = true;
        dragOffset = transform.position - mouseWorldPos;
        ApplyColorToSurface(draggingColor);
    }

    public void UpdateDrag(Vector3 mouseWorldPos)
    {
        if (!isDragging) return;
        FindGroundY();

        Vector3 newPos = mouseWorldPos + dragOffset;
        newPos.y = groundY;
        transform.position = newPos;

        if (labelObj != null)
            labelObj.transform.position = new Vector3(transform.position.x, groundY + 2f, transform.position.z);
    }

    public void EndDrag()
    {
        isDragging = false;
        if (isSelected) ApplyColorToSurface(selectedColor);
        else ApplyColorToSurface(normalColor);
    }

    // --- Direction ---

    public void RotateDirection()
    {
        directionAngle = (directionAngle + 90f) % 360f;
        transform.rotation = Quaternion.Euler(0, directionAngle, 0);
        Debug.Log("[YardEditor] Road " + roadId + " direction: " + directionAngle + "°");
    }

    public void SetDirection(float angle)
    {
        directionAngle = angle;
        transform.rotation = Quaternion.Euler(0, directionAngle, 0);
    }

    // --- Resize ---

    public void SetSize(float width, float length)
    {
        roadWidth = Mathf.Clamp(width, 4f, 60f);
        roadLength = Mathf.Clamp(length, 10f, 400f);
        BuildRoadVisual();
    }

    public void AdjustWidth(float delta)
    {
        SetSize(roadWidth + delta, roadLength);
    }

    public void AdjustLength(float delta)
    {
        SetSize(roadWidth, roadLength + delta);
    }

    // --- Visual ---

    void ApplyColorToSurface(Color color)
    {
        if (roadSurface == null) return;
        Renderer rend = roadSurface.GetComponent<Renderer>();
        if (rend == null) return;

        if (roadMaterial == null) roadMaterial = CreateRoadMaterial();
        
        roadMaterial.color = color;
        if (roadMaterial.HasProperty("_BaseColor")) roadMaterial.SetColor("_BaseColor", color);
        rend.sharedMaterial = roadMaterial;
    }

    // --- Data Export ---

    public RoadPlacement ToPlacement()
    {
        return new RoadPlacement(roadId, transform.position, directionAngle, roadWidth, roadLength);
    }

    void OnDestroy()
    {
        if (roadMaterial != null)
        {
            if (Application.isPlaying) Destroy(roadMaterial);
            else DestroyImmediate(roadMaterial);
        }
    }
}
