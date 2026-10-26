using UnityEngine;

// ============================================================
// DraggableYard.cs
// Komponen yang di-attach ke setiap Yard block yang sudah di-spawn
// Menghandle: drag, selection highlight, toggle orientasi
// ============================================================

public class DraggableYard : MonoBehaviour
{
    [Header("Yard Info")]
    public string blockName;        // Nama block dari API (bisa apapun)
    public bool isHorizontal = false;

    [Header("API Metadata")]
    public int maxRow = 6;
    public int maxSlot = 45;
    public int maxTier = 7;

    [Header("Visual Feedback")]
    public Color normalColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    public Color selectedColor = new Color(1f, 0.92f, 0.2f, 1f);
    public Color draggingColor = new Color(0.2f, 0.9f, 0.4f, 1f);

    // State
    [HideInInspector] public bool isSelected = false;
    [HideInInspector] public bool isDragging = false;

    private Vector3 dragOffset;
    private float groundY = 19.0f;
    private Renderer[] allRenderers;
    private Color[] originalColors;
    private Material[] highlightMaterials;

    // Label overlay
    private GameObject labelObj;

    void Start()
    {
        SetupRenderers();
        CreateLabel();
        FindGroundY();
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

    void SetupRenderers()
    {
        allRenderers = GetComponentsInChildren<Renderer>();
    }

    void CreateLabel()
    {
        // Nama blok sudah ditampilkan secara presisi di lantai dalam kotak kuning [ B ] (seperti SampleScene)
        if (labelObj != null)
        {
            if (Application.isPlaying) Destroy(labelObj);
            else DestroyImmediate(labelObj);
        }
    }

    public void RefreshLabel()
    {
        // Not needed for floor labels
    }

    Bounds GetCombinedBounds()
    {
        if (allRenderers == null || allRenderers.Length == 0) 
            SetupRenderers();

        if (allRenderers.Length == 0)
            return new Bounds(transform.position, Vector3.one * 10f);

        Bounds b = allRenderers[0].bounds;
        for (int i = 1; i < allRenderers.Length; i++)
        {
            if (allRenderers[i] != null)
                b.Encapsulate(allRenderers[i].bounds);
        }
        return b;
    }

    // --- Selection ---

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

    private float initialY = 19.0f;

    public void StartDrag(Vector3 mouseWorldPos)
    {
        isDragging = true;
        initialY = transform.position.y;
        Vector3 mPos = new Vector3(mouseWorldPos.x, initialY, mouseWorldPos.z);
        dragOffset = transform.position - mPos;
        SetHighlight(draggingColor);
    }

    public void UpdateDrag(Vector3 mouseWorldPos)
    {
        if (!isDragging) return;

        Vector3 newPos = mouseWorldPos + dragOffset;
        newPos.y = initialY; // Pertahankan ketinggian awal yard
        Vector3 delta = newPos - transform.position;
        transform.position = newPos;

        if (ContainerManager.Instance != null && !string.IsNullOrEmpty(blockName))
        {
            ContainerManager.Instance.MoveBlockContainers(blockName, delta);
        }

        RefreshLabel();
    }

    public void EndDrag()
    {
        isDragging = false;
        if (isSelected) SetHighlight(selectedColor);
        else ClearHighlight();
    }

    // --- Orientation Toggle ---

    public void ToggleOrientation()
    {
        isHorizontal = !isHorizontal;
        
        float currentY = transform.eulerAngles.y;
        float targetY = isHorizontal ? 90f : 0f;
        float deltaAngle = Mathf.DeltaAngle(currentY, targetY);
        transform.rotation = Quaternion.Euler(0, targetY, 0);

        if (ContainerManager.Instance != null && !string.IsNullOrEmpty(blockName))
        {
            ContainerManager.Instance.RotateBlockContainers(blockName, transform.position, deltaAngle);
        }
        
        RefreshLabel();
        Debug.Log("[YardEditor] Yard " + blockName + " orientasi: " + (isHorizontal ? "Horizontal" : "Vertical"));
    }

    public void SetOrientation(bool horizontal)
    {
        if (isHorizontal == horizontal) return;
        isHorizontal = horizontal;
        float currentY = transform.eulerAngles.y;
        float targetY = isHorizontal ? 90f : 0f;
        float deltaAngle = Mathf.DeltaAngle(currentY, targetY);
        transform.rotation = Quaternion.Euler(0, targetY, 0);

        if (ContainerManager.Instance != null && !string.IsNullOrEmpty(blockName))
        {
            ContainerManager.Instance.RotateBlockContainers(blockName, transform.position, deltaAngle);
        }
        RefreshLabel();
    }

    // --- Visual Highlight ---

    void SetHighlight(Color color)
    {
        Transform platform = transform.Find("YardPlatform");
        if (platform != null)
        {
            Renderer r = platform.GetComponent<Renderer>();
            if (r != null)
            {
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                r.SetPropertyBlock(block);
            }
        }
    }

    void ClearHighlight()
    {
        Transform platform = transform.Find("YardPlatform");
        if (platform != null)
        {
            Renderer r = platform.GetComponent<Renderer>();
            if (r != null)
            {
                r.SetPropertyBlock(null);
            }
        }
    }

    // --- Data Export ---

    public YardPlacement ToPlacement()
    {
        // Hitung ukuran dari child platform
        float w = 0, l = 0;
        Transform platform = transform.Find("YardPlatform");
        if (platform != null)
        {
            w = platform.localScale.x;
            l = platform.localScale.z;
        }
        return new YardPlacement(blockName, transform.position, isHorizontal, w, l);
    }
}
