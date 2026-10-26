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
    private float groundY = 19.0f;

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
        BuildRoadVisual();
    }

    void FindGroundY()
    {
        if (YardEditorManager.Instance != null)
        {
            groundY = YardEditorManager.Instance.groundY;
            return;
        }
        GameObject vrEnv = GameObject.Find("vr-env");
        if (vrEnv != null)
        {
            Renderer rend = vrEnv.GetComponentInChildren<Renderer>();
            if (rend != null) groundY = rend.bounds.max.y;
        }
    }

    // --- Build Visual ---

    // --- Build Visual ---

    public void BuildRoadVisual()
    {
        // Cleanup existing
        ClearChildren();

        float roadThickness = 0.4f; // Menempel presisi di permukaan

        // 1. Road surface (solid 3D Dark Asphalt Cube)
        roadSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roadSurface.name = "RoadSurface";
        roadSurface.transform.SetParent(transform);
        roadSurface.transform.localPosition = new Vector3(0, roadThickness / 2f, 0);
        roadSurface.transform.localRotation = Quaternion.identity;
        roadSurface.transform.localScale = new Vector3(roadWidth, roadThickness, roadLength);

        // Material aspal gelap
        roadMaterial = CreateRoadMaterial();
        Renderer surfaceRend = roadSurface.GetComponent<Renderer>();
        if (surfaceRend != null) surfaceRend.sharedMaterial = roadMaterial;

        // Clean child collider
        Collider childCol = roadSurface.GetComponent<Collider>();
        if (childCol != null) DestroyImmediate(childCol);

        // Add root BoxCollider for 100% click & drag detection
        BoxCollider rootCol = GetComponent<BoxCollider>();
        if (rootCol == null) rootCol = gameObject.AddComponent<BoxCollider>();
        rootCol.center = new Vector3(0, roadThickness / 2f + 0.5f, 0);
        rootCol.size = new Vector3(roadWidth, 2.0f, roadLength);

        // 2. Yellow side edge lines
        CreateYellowEdgeMarkings(roadThickness);

        // 3. Dashed center line
        CreateDashedLine(roadThickness);

        // 4. Directional arrow
        CreateArrow(roadThickness);

        // 5. Label
        CreateLabel(roadThickness);
    }

    void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    Material CreateRoadMaterial()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Unlit/Color");
        if (s == null) s = Shader.Find("Sprites/Default");
        if (s == null) s = Shader.Find("Standard");

        Material mat = new Material(s);
        Color asphalt = new Color(0.12f, 0.12f, 0.14f, 1f); // Dark industrial asphalt
        mat.color = asphalt;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", asphalt);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", asphalt);
        mat.name = "RoadAsphalt_" + roadId;
        return mat;
    }

    private Material cachedYellowMat;
    private Material GetYellowBorderMaterial()
    {
        if (cachedYellowMat == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Standard");

            cachedYellowMat = new Material(s);
            Color brightYellow = new Color(1.0f, 0.88f, 0.0f, 1.0f);
            cachedYellowMat.color = brightYellow;
            if (cachedYellowMat.HasProperty("_BaseColor")) cachedYellowMat.SetColor("_BaseColor", brightYellow);
        }
        return cachedYellowMat;
    }

    void CreateYellowEdgeMarkings(float roadThickness)
    {
        GameObject sideMarkingsGroup = new GameObject("YellowEdgeMarkings");
        sideMarkingsGroup.transform.SetParent(transform);
        sideMarkingsGroup.transform.localPosition = Vector3.zero;

        Material yellowMat = GetYellowBorderMaterial();

        float lineThick = 0.35f;
        float lineY = roadThickness + 0.02f;
        float halfWidth = roadWidth / 2f - (lineThick / 2f);

        // Garis pinggir kiri (West/Left edge)
        GameObject leftLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftLine.name = "YellowEdgeLeft";
        leftLine.transform.SetParent(sideMarkingsGroup.transform);
        leftLine.transform.localPosition = new Vector3(-halfWidth, lineY, 0);
        leftLine.transform.localScale = new Vector3(lineThick, 0.02f, roadLength);
        DestroyImmediate(leftLine.GetComponent<Collider>());
        leftLine.GetComponent<Renderer>().sharedMaterial = yellowMat;

        // Garis pinggir kanan (East/Right edge)
        GameObject rightLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightLine.name = "YellowEdgeRight";
        rightLine.transform.SetParent(sideMarkingsGroup.transform);
        rightLine.transform.localPosition = new Vector3(halfWidth, lineY, 0);
        rightLine.transform.localScale = new Vector3(lineThick, 0.02f, roadLength);
        DestroyImmediate(rightLine.GetComponent<Collider>());
        rightLine.GetComponent<Renderer>().sharedMaterial = yellowMat;
    }

    void CreateDashedLine(float roadThickness)
    {
        dashContainer = new GameObject("DashLines");
        dashContainer.transform.SetParent(transform);
        dashContainer.transform.localPosition = Vector3.zero;

        // Material garis putih
        Shader s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Unlit/Color");
        if (s == null) s = Shader.Find("Standard");

        Material dashMat = new Material(s);
        Color white = new Color(0.95f, 0.95f, 0.95f, 1f);
        dashMat.color = white;
        if (dashMat.HasProperty("_BaseColor")) dashMat.SetColor("_BaseColor", white);
        dashMat.name = "DashLine_Road_" + roadId;

        float dashLen = 3.0f;
        float gapLen = 3.0f;
        float step = dashLen + gapLen;
        float lineY = roadThickness + 0.02f;

        for (float dist = -roadLength / 2f + 1.0f; dist < roadLength / 2f - 1.0f; dist += step)
        {
            float actualLen = Mathf.Min(dashLen, (roadLength / 2f - 1.0f) - dist);
            if (actualLen <= 0.1f) break;

            GameObject dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dash.name = "Dash";
            dash.transform.SetParent(dashContainer.transform);
            dash.transform.localPosition = new Vector3(0, lineY, dist + actualLen / 2f);
            dash.transform.localRotation = Quaternion.identity;
            dash.transform.localScale = new Vector3(0.3f, 0.02f, actualLen);

            Collider col = dash.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            Renderer rend = dash.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = dashMat;
        }
    }

    void CreateArrow(float roadThickness)
    {
        arrowIndicator = new GameObject("DirectionArrow");
        arrowIndicator.transform.SetParent(transform);
        
        float lineY = roadThickness + 0.04f;
        arrowIndicator.transform.localPosition = new Vector3(0, lineY, 0);

        Shader s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Unlit/Color");
        if (s == null) s = Shader.Find("Standard");

        Material arrowMat = new Material(s);
        Color arrowColor = new Color(0.1f, 0.85f, 0.3f, 1f);
        arrowMat.color = arrowColor;
        if (arrowMat.HasProperty("_BaseColor")) arrowMat.SetColor("_BaseColor", arrowColor);
        arrowMat.name = "ArrowMat_Road_" + roadId;

        // Arrow body
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "ArrowBody";
        body.transform.SetParent(arrowIndicator.transform);
        body.transform.localPosition = new Vector3(0, 0, -1.2f);
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = new Vector3(0.6f, 0.02f, 3.5f);
        Collider bodyCol = body.GetComponent<Collider>();
        if (bodyCol != null) DestroyImmediate(bodyCol);
        body.GetComponent<Renderer>().sharedMaterial = arrowMat;

        // Arrow head - left wing
        GameObject leftWing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftWing.name = "ArrowWingL";
        leftWing.transform.SetParent(arrowIndicator.transform);
        leftWing.transform.localPosition = new Vector3(-0.8f, 0, 0.8f);
        leftWing.transform.localRotation = Quaternion.Euler(0, -40, 0);
        leftWing.transform.localScale = new Vector3(0.6f, 0.02f, 2.2f);
        Collider lwCol = leftWing.GetComponent<Collider>();
        if (lwCol != null) DestroyImmediate(lwCol);
        leftWing.GetComponent<Renderer>().sharedMaterial = arrowMat;

        // Arrow head - right wing
        GameObject rightWing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWing.name = "ArrowWingR";
        rightWing.transform.SetParent(arrowIndicator.transform);
        rightWing.transform.localPosition = new Vector3(0.8f, 0, 0.8f);
        rightWing.transform.localRotation = Quaternion.Euler(0, 40, 0);
        rightWing.transform.localScale = new Vector3(0.6f, 0.02f, 2.2f);
        Collider rwCol = rightWing.GetComponent<Collider>();
        if (rwCol != null) DestroyImmediate(rwCol);
        rightWing.GetComponent<Renderer>().sharedMaterial = arrowMat;
    }

    void CreateLabel(float roadThickness)
    {
        labelObj = new GameObject("RoadLabel_" + roadId);
        labelObj.transform.SetParent(transform);
        
        float lineY = roadThickness + 0.06f;
        labelObj.transform.localPosition = new Vector3(0, lineY, -roadLength / 2f + 3f);
        labelObj.transform.localRotation = Quaternion.Euler(90, 0, 0);

        TMPro.TextMeshPro tmp = labelObj.AddComponent<TMPro.TextMeshPro>();
        tmp.text = "ROAD " + roadId + " (" + roadWidth + "m x " + roadLength + "m)";
        tmp.fontSize = 12;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = new Color(0.3f, 0.85f, 1f, 1f);
        if (Application.isPlaying)
        {
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = Color.black;
        }
        tmp.rectTransform.sizeDelta = new Vector2(25f, 6f);
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
