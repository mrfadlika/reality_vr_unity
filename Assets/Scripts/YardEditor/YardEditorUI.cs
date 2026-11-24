using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// ============================================================
// YardEditorUI.cs
// Canvas-based UI: sidebar panel, yard/road/accessory buttons, controls
// Mendukung kustomisasi desain PNG penuh dari Inspector [YardEditor].
// ============================================================

public class YardEditorUI : MonoBehaviour
{
    // Canvas references (auto-created)
    private Canvas canvas;
    private GameObject sidebarPanel;
    private GameObject statusBar;
    private RectTransform scrollContentRect;

    // Dynamic yard buttons — key = block name
    private Dictionary<string, Button> yardButtons = new Dictionary<string, Button>();
    private Transform yardButtonContainer; // Parent untuk yard buttons agar bisa di-rebuild

    // Fixed buttons
    private Button addRoadBtn;
    private Button roadWMinusBtn;
    private Button roadWPlusBtn;
    private Button roadLMinusBtn;
    private Button roadLPlusBtn;

    private Button addPagarBtn;
    private Button pagarLMinusBtn;
    private Button pagarLPlusBtn;

    private Button addGedungBtn;
    private Button addGateBtn;
    private Button addReachStackerBtn;
    private Button addTopLoaderBtn;
    private Button accScaleMinusBtn;
    private Button accScalePlusBtn;

    private Button rotateBtn;
    private Button deleteBtn;
    private Button saveBtn;
    private Button loadBtn;
    private Button clearBtn;

    // Cache Texture2D -> Sprite agar ringan
    private Dictionary<Texture2D, Sprite> spriteCache = new Dictionary<Texture2D, Sprite>();

    // Status text
    private TMPro.TextMeshProUGUI statusText;

    // Loading indicator
    private GameObject loadingLabel;

    // References untuk elemen teks/separator agar bisa di-toggle saat background PNG aktif
    private List<GameObject> sectionHeaderObjects = new List<GameObject>();
    private List<GameObject> separatorObjects = new List<GameObject>();
    private GameObject headerLabelObj;
    private GameObject helpLabelObj;

    // References pita judul section dinamis (Ribbon Headers)
    private GameObject ribbonYardObj;
    private GameObject ribbonRoadObj;
    private GameObject ribbonWallObj;
    private GameObject ribbonAccObj;
    private GameObject ribbonToolsObj;
    private GameObject ribbonFileObj;

    // Katalog Modal Dialog (Opsi 3: Jendela Katalog Pop-up)
    private GameObject catalogModalObj;
    private Transform catalogCardsContainer;
    private TMPro.TextMeshProUGUI catalogSubtitleText;
    private Button openCatalogBtn;

    // Cache Sprite kapsul 3D procedural untuk tombol Yard dinamis
    private Sprite proceduralPillSprite;

    // Styling constants
    private Color panelBgColor = new Color(0.08f, 0.09f, 0.13f, 0.95f);
    private Color buttonNormal = new Color(0.16f, 0.17f, 0.24f, 1f);
    private Color buttonRoad = new Color(0.48f, 0.36f, 0.12f, 1f);
    private Color buttonAccessory = new Color(0.16f, 0.38f, 0.52f, 1f);
    private Color buttonDanger = new Color(0.60f, 0.16f, 0.16f, 1f);
    private Color buttonSave = new Color(0.14f, 0.50f, 0.26f, 1f);
    private Color buttonRotate = new Color(0.42f, 0.24f, 0.54f, 1f);
    private Color accentColor = new Color(0.32f, 0.76f, 1f, 1f);
    private Color textColor = new Color(0.94f, 0.95f, 0.98f, 1f);
    private Color textMuted = new Color(0.65f, 0.68f, 0.75f, 1f);
    private Color spawnedColor = new Color(0.28f, 0.28f, 0.32f, 0.6f);

    private bool uiBuilt = false;
    private bool wasEditorActive = false;

    void Start()
    {
        if (!Application.isPlaying) return;
        BuildUI();
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (!uiBuilt) BuildUI();
        if (!uiBuilt) return;

        YardEditorManager mgr = YardEditorManager.Instance;
        if (mgr == null) return;

        // Toggle visibility
        if (sidebarPanel != null && sidebarPanel.activeSelf != mgr.isEditorActive)
        {
            sidebarPanel.SetActive(mgr.isEditorActive);
        }
        if (statusBar != null && statusBar.activeSelf != mgr.isEditorActive)
        {
            statusBar.SetActive(mgr.isEditorActive);
        }

        // Tutup modal katalog jika editor dinonaktifkan
        if (!mgr.isEditorActive && catalogModalObj != null && catalogModalObj.activeSelf)
        {
            CloseYardCatalogModal();
        }

        // Dukungan tombol Escape untuk menutup katalog modal
        if (catalogModalObj != null && catalogModalObj.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseYardCatalogModal();
        }

        // Saat baru dibuka, paksa rebuild layout agar tidak ada elemen yang bertumpuk
        if (mgr.isEditorActive && !wasEditorActive)
        {
            ForceRebuildSidebarLayout();
        }
        wasEditorActive = mgr.isEditorActive;

        // Update button states & PNG sprites
        if (mgr.isEditorActive)
        {
            UpdateLoadingState();
            UpdateButtonStates();
            UpdateStatusText();
        }
    }

    public void ForceRebuildSidebarLayout()
    {
        Canvas.ForceUpdateCanvases();
        if (yardButtonContainer != null)
        {
            RectTransform yrt = yardButtonContainer.GetComponent<RectTransform>();
            if (yrt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(yrt);
        }
        if (scrollContentRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
        }
    }

    Sprite GetOrCreateSprite(Texture2D tex)
    {
        if (tex == null) return null;
        if (spriteCache.TryGetValue(tex, out Sprite cached) && cached != null && cached.texture == tex && Mathf.Approximately(cached.rect.width, tex.width) && Mathf.Approximately(cached.rect.height, tex.height))
        {
            return cached;
        }
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        spriteCache[tex] = sp;
        return sp;
    }

    Sprite GetProceduralPillSprite()
    {
        if (proceduralPillSprite != null) return proceduralPillSprite;
        int w = 128, h = 36, r = 16;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = "ProceduralPillSprite";
        Color[] cols = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = 0;
                if (x < r) dx = r - x;
                else if (x >= w - r) dx = x - (w - r - 1);
                float dy = y < r ? r - y : (y >= h - r ? y - (h - r - 1) : 0);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > r)
                {
                    cols[y * w + x] = Color.clear;
                }
                else
                {
                    float yNorm = (float)y / (float)h;
                    float edgeAlpha = Mathf.Clamp01((r - dist) * 2f);
                    float shade = Mathf.Lerp(0.82f, 1.08f, yNorm);
                    cols[y * w + x] = new Color(shade, shade, shade, edgeAlpha);
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        proceduralPillSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return proceduralPillSprite;
    }

    void ApplyButtonPng(Button btn, Texture2D pngTex, Color defaultBgColor, bool isSpawnedDisabled = false)
    {
        if (btn == null) return;
        YardEditorManager mgr = YardEditorManager.Instance;
        Image img = btn.GetComponent<Image>();
        Transform textTr = btn.transform.Find("Text");
        YardButtonHoverFX fx = btn.GetComponent<YardButtonHoverFX>();

        float globalScale = (mgr != null && mgr.globalButtonScale > 0.01f) ? mgr.globalButtonScale : 1f;
        float sideScale = (mgr != null && mgr.sidebarButtonScale > 0.01f) ? mgr.sidebarButtonScale : 1f;
        float totalScale = globalScale * sideScale;

        // Cek apakah button berada di row (horizontal row: 2 kolom atau 3 kolom)
        bool isRowChild = (btn.transform.parent != null &&
                           btn.transform.parent != (scrollContentRect != null ? scrollContentRect.transform : null) &&
                           btn.transform.parent != yardButtonContainer);

        float targetH;
        if (isRowChild)
        {
            LayoutElement parentLe = btn.transform.parent.GetComponent<LayoutElement>();
            targetH = (parentLe != null ? parentLe.preferredHeight : 32f) * totalScale;
        }
        else if (btn == addRoadBtn || btn == addPagarBtn)
        {
            // Proporsional alami persis gambar 2 mockup user (tidak stretched vertikal)
            targetH = 44f * totalScale;
        }
        else if (btn == clearBtn)
        {
            // Clear All di bawah sendiri panjang
            targetH = 34f * totalScale;
        }
        else if (btn == openCatalogBtn)
        {
            targetH = 38f * totalScale;
        }
        else
        {
            targetH = 34f * totalScale;
        }

        LayoutElement le = btn.GetComponent<LayoutElement>();
        if (le != null)
        {
            if (Mathf.Abs(le.preferredHeight - targetH) > 0.5f)
            {
                le.preferredHeight = targetH;
                le.minHeight = targetH;
                if (isRowChild && btn.transform.parent != null)
                {
                    LayoutElement parentLe = btn.transform.parent.GetComponent<LayoutElement>();
                    if (parentLe != null && targetH > parentLe.preferredHeight)
                    {
                        parentLe.preferredHeight = targetH;
                        parentLe.minHeight = targetH;
                    }
                }
            }
        }

        if (pngTex != null && img != null)
        {
            img.sprite = GetOrCreateSprite(pngTex);
            img.type = Image.Type.Simple;
            img.preserveAspect = true; // WAJIB: Mempertahankan rasio aspek alami asli gambar agar tombol tidak gepeng
            img.color = Color.white;

            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            // Saat tombol disabled (misal belum ada yang diselect untuk Rotate & Delete), gambar tetap terlihat jelas hanya sedikit redup
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.65f);
            btn.colors = colors;

            if (fx != null) fx.SetHasCustomPng(true);

            // Selalu sembunyikan text bawaan jika tombol sudah memiliki gambar PNG desain sendiri
            if (textTr != null)
            {
                textTr.gameObject.SetActive(false);
            }
        }
        else
        {
            if (img != null)
            {
                img.sprite = GetProceduralPillSprite();
                img.type = Image.Type.Sliced;
                img.preserveAspect = false;
                img.color = isSpawnedDisabled ? spawnedColor : defaultBgColor;
            }

            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            btn.colors = colors;

            if (fx != null) fx.SetHasCustomPng(false);

            if (textTr != null && !textTr.gameObject.activeSelf)
            {
                textTr.gameObject.SetActive(true);
            }
        }
    }

    // --- Build Entire UI ---

    void BuildUI()
    {
        if (uiBuilt) return;

        // Auto-create EventSystem jika belum ada di scene (wajib untuk UI Canvas click)
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[YardEditorUI] Auto-created EventSystem for UI clicks.");
        }

        // Canvas
        GameObject canvasObj = new GameObject("YardEditorCanvas");
        canvasObj.transform.SetParent(transform);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Di atas UI lain

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        // Sidebar Panel
        BuildSidebar(canvasObj.transform);

        // Status Bar (bottom)
        BuildStatusBar(canvasObj.transform);

        // Jendela Modal Dialog Pop-up Katalog Yard (Opsi 3)
        BuildYardCatalogModal(canvasObj.transform);

        uiBuilt = true;
    }

    void BuildSidebar(Transform canvasTransform)
    {
        // Panel container
        sidebarPanel = CreatePanel(canvasTransform, "SidebarPanel", panelBgColor);
        RectTransform panelRect = sidebarPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1, 0);
        panelRect.anchorMax = new Vector2(1, 1);
        panelRect.pivot = new Vector2(1, 1);
        panelRect.offsetMin = new Vector2(-340, 20);  // Left, Bottom
        panelRect.offsetMax = new Vector2(-14, -14);  // Right, Top

        // Scroll view agar bisa scroll jika yard banyak
        GameObject scrollViewObj = new GameObject("ScrollView");
        scrollViewObj.transform.SetParent(sidebarPanel.transform, false);
        RectTransform scrollViewRect = scrollViewObj.AddComponent<RectTransform>();
        scrollViewRect.anchorMin = Vector2.zero;
        scrollViewRect.anchorMax = Vector2.one;
        scrollViewRect.offsetMin = Vector2.zero;
        scrollViewRect.offsetMax = Vector2.zero;

        ScrollRect scrollRect = scrollViewObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        // Viewport (mask)
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollViewObj.transform, false);
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        Image viewportImg = viewport.AddComponent<Image>();
        viewportImg.color = new Color(0, 0, 0, 0.01f);
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        scrollRect.viewport = viewportRect;

        // Content (scroll area)
        GameObject scrollContent = new GameObject("ScrollContent");
        scrollContent.transform.SetParent(viewport.transform, false);
        scrollContentRect = scrollContent.AddComponent<RectTransform>();
        scrollContentRect.anchorMin = new Vector2(0, 1);
        scrollContentRect.anchorMax = new Vector2(1, 1);
        scrollContentRect.pivot = new Vector2(0.5f, 1);
        scrollContentRect.offsetMin = Vector2.zero;
        scrollContentRect.offsetMax = Vector2.zero;

        scrollRect.content = scrollContentRect;

        // Vertical layout — padding atas 122px agar konten mulai tepat di bawah logo & teks "Yard Editor" bawaan bgg.png
        VerticalLayoutGroup vlg = scrollContent.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4;
        vlg.padding = new RectOffset(18, 18, 122, 28);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = scrollContent.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Transform parent = scrollContent.transform;
        YardEditorManager mgr = YardEditorManager.Instance;

        sectionHeaderObjects.Clear();
        separatorObjects.Clear();

        // Header label teks manual dimatikan karena bgg.png sudah memiliki logo & teks "Yard Editor" yang dicetak bagus
        headerLabelObj = CreateLabelReturnable(parent, "HeaderLabel", "", 1, accentColor, TextAnchor.MiddleCenter, 0);
        headerLabelObj.SetActive(false);

        // --- Section 1: Yard Blocks (Opsi 3: Modal Dialog Pop-up Katalog) ---
        ribbonYardObj = CreateSectionRibbon(parent, "YardRibbon", "Yard Blocks", mgr != null ? mgr.pngRibbonYard : null);

        // Tombol kapsul hijau sesuai contoh mockup gambar 2: + Buka Katalog Yard
        openCatalogBtn = CreateButton(parent, "OpenCatalogBtn", "+ Buka Katalog Yard", new Color(0.12f, 0.65f, 0.33f, 1f), 38, () =>
        {
            OpenYardCatalogModal();
        });

        GameObject dummyYardContainer = new GameObject("YardButtonContainerDummy");
        dummyYardContainer.transform.SetParent(parent, false);
        dummyYardContainer.SetActive(false);
        yardButtonContainer = dummyYardContainer.transform;

        CreateSpacer(parent, 18);

        // --- Section 2: Roads ---
        ribbonRoadObj = CreateSectionRibbon(parent, "RoadRibbon", "Roads", mgr != null ? mgr.pngRibbonRoads : null);

        // Add Road proporsional alami (height 44, tidak strech vertikal)
        addRoadBtn = CreateButton(parent, "AddRoadBtn", "+ Add Road", buttonRoad, 44, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.SpawnRoad();
        });

        Transform roadWidthRow = CreateHorizontalRow(parent, "RoadWidthGroup", 32);
        roadWMinusBtn = CreateButton(roadWidthRow, "RoadWMinus", "Width -2m", buttonNormal, 32, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.AdjustSelectedRoadWidth(-2f);
        });
        roadWPlusBtn = CreateButton(roadWidthRow, "RoadWPlus", "Width +2m", buttonNormal, 32, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.AdjustSelectedRoadWidth(2f);
        });

        Transform roadLenRow = CreateHorizontalRow(parent, "RoadLenGroup", 32);
        roadLMinusBtn = CreateButton(roadLenRow, "RoadLMinus", "Length -5m", buttonNormal, 32, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.AdjustSelectedRoadLength(-5f);
        });
        roadLPlusBtn = CreateButton(roadLenRow, "RoadLPlus", "Length +5m", buttonNormal, 32, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.AdjustSelectedRoadLength(5f);
        });

        CreateSpacer(parent, 18);

        // --- Section 3: Pagar / Walls ---
        ribbonWallObj = CreateSectionRibbon(parent, "WallRibbon", "Walls", mgr != null ? mgr.pngRibbonWalls : null);

        // Add Wall proporsional alami (height 44, tidak strech vertikal)
        addPagarBtn = CreateButton(parent, "AddPagarBtn", "+ Add Pagar", buttonRoad, 44, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.SpawnAccessory("Pagar");
        });

        Transform pagarLenRow = CreateHorizontalRow(parent, "PagarLenGroup", 32);
        pagarLMinusBtn = CreateButton(pagarLenRow, "PagarLMinus", "Length -5m", buttonNormal, 32, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.AdjustSelectedPagarLength(-5f);
        });
        pagarLPlusBtn = CreateButton(pagarLenRow, "PagarLPlus", "Length +5m", buttonNormal, 32, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.AdjustSelectedPagarLength(5f);
        });

        CreateSpacer(parent, 18);

        // --- Section 4: Aksesoris FBX (Sleek pill buttons, height 32, anti-gepeng) ---
        ribbonAccObj = CreateSectionRibbon(parent, "AccRibbon", "Accessories", mgr != null ? mgr.pngRibbonAccessories : null);

        Transform accRow1 = CreateHorizontalRow(parent, "AccRow1", 32);
        addGedungBtn = CreateButton(accRow1, "AddGedungBtn", "+ Gedung", buttonAccessory, 32, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.SpawnAccessory("Gedung");
        });
        addGateBtn = CreateButton(accRow1, "AddGateBtn", "+ Gate", buttonAccessory, 32, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.SpawnAccessory("Gate");
        });

        Transform accRow2 = CreateHorizontalRow(parent, "AccRow2", 32);
        addReachStackerBtn = CreateButton(accRow2, "AddReachStackerBtn", "+ Reach Stacker", buttonAccessory, 32, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.SpawnAccessory("ReachStacker");
        });
        addTopLoaderBtn = CreateButton(accRow2, "AddTopLoaderBtn", "+ Top Loader", buttonAccessory, 32, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.SpawnAccessory("TopLoader");
        });

        Transform accScaleRow = CreateHorizontalRow(parent, "AccScaleGroup", 32);
        accScaleMinusBtn = CreateButton(accScaleRow, "AccScaleMinus", "Scale -", buttonNormal, 32, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.AdjustSelectedAccessoryScale(-0.2f);
        });
        accScalePlusBtn = CreateButton(accScaleRow, "AccScalePlus", "Scale +", buttonNormal, 32, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.AdjustSelectedAccessoryScale(0.2f);
        });

        CreateSpacer(parent, 18);

        // --- Section 5: Tools (Grid 1:2 — Berdampingan kiri & kanan, height 34 tidak strech) ---
        ribbonToolsObj = CreateSectionRibbon(parent, "ToolsRibbon", "Tools", mgr != null ? mgr.pngRibbonTools : null);

        Transform toolsRow = CreateHorizontalRow(parent, "ToolsGroup", 34);
        rotateBtn = CreateButton(toolsRow, "RotateBtn", "Rotate", buttonRotate, 34, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.ToggleSelectedOrientation();
        });

        deleteBtn = CreateButton(toolsRow, "DeleteBtn", "Delete", buttonDanger, 34, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.DeleteSelected();
        });

        CreateSpacer(parent, 18);

        // --- Section 6: File Operations (Save & Load samping-sampingan, Clear All di bawah sendiri panjang) ---
        ribbonFileObj = CreateSectionRibbon(parent, "FileRibbon", "File", mgr != null ? mgr.pngRibbonFile : null);

        // Row 1: Save Layout (kiri) & Load Layout (kanan)
        Transform fileRow1 = CreateHorizontalRow(parent, "FileRow1", 32);
        saveBtn = CreateButton(fileRow1, "SaveBtn", "Save", buttonSave, 32, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.SaveLayout();
        });

        loadBtn = CreateButton(fileRow1, "LoadBtn", "Load", buttonNormal, 32, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.LoadLayout();
        });

        // Clear All di bawah sendiri panjang
        clearBtn = CreateButton(parent, "ClearBtn", "Clear All", buttonDanger, 34, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.ClearAll();
        });

        CreateSpacer(parent, 20);

        // --- Help ---
        helpLabelObj = CreateLabelReturnable(parent, "HelpLabel",
            "2D Camera: WASD / Right-Drag pan, Scroll zoom\n" +
            "Object: Click to select, Left-Drag to move\n" +
            "R / Rotate: Rotate yard / road / aksesoris\n" +
            "Del: Delete selected object",
            11, textMuted, TextAnchor.MiddleLeft, 64);

        ForceRebuildSidebarLayout();
        sidebarPanel.SetActive(false);
    }

    Transform CreateHorizontalRow(Transform parent, string name, float height)
    {
        GameObject rowObj = new GameObject(name);
        rowObj.transform.SetParent(parent, false);

        RectTransform rt = rowObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, height);

        LayoutElement le = rowObj.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        HorizontalLayoutGroup hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        return rowObj.transform;
    }

    Transform CreateHorizontalRow3(Transform parent, string name, float height)
    {
        GameObject rowObj = new GameObject(name);
        rowObj.transform.SetParent(parent, false);

        RectTransform rt = rowObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, height);

        LayoutElement le = rowObj.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        HorizontalLayoutGroup hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 5;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        return rowObj.transform;
    }

    GameObject CreateSectionRibbon(Transform parent, string name, string fallbackTitle, Texture2D ribbonTex)
    {
        // Container baris di sidebar
        GameObject ribbonContainer = new GameObject(name);
        ribbonContainer.transform.SetParent(parent, false);

        RectTransform cRt = ribbonContainer.AddComponent<RectTransform>();
        cRt.sizeDelta = new Vector2(0, 26f);

        LayoutElement le = ribbonContainer.AddComponent<LayoutElement>();
        le.preferredHeight = 26f;
        le.minHeight = 26f;

        // Objek RibbonImage rata kiri (sesuai contoh mockup user)
        GameObject imgObj = new GameObject("RibbonImage");
        imgObj.transform.SetParent(ribbonContainer.transform, false);

        RectTransform rt = imgObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(165f, 26f);

        Image img = imgObj.AddComponent<Image>();
        img.type = Image.Type.Simple;
        img.preserveAspect = true;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(imgObj.transform, false);
        RectTransform trt = textObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(10, 0);
        trt.offsetMax = new Vector2(-10, 0);

        TMPro.TextMeshProUGUI tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = fallbackTitle;
        tmp.fontSize = 11;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TMPro.TextAlignmentOptions.MidlineLeft;

        ApplyRibbonTexture(ribbonContainer, ribbonTex, fallbackTitle);
        sectionHeaderObjects.Add(ribbonContainer);
        return ribbonContainer;
    }

    void ApplyRibbonTexture(GameObject ribbonObj, Texture2D ribbonTex, string title)
    {
        if (ribbonObj == null) return;
        Image img = ribbonObj.GetComponentInChildren<Image>();
        Transform textTr = ribbonObj.transform.Find("RibbonImage/Text");
        if (textTr == null) textTr = ribbonObj.transform.Find("Text");

        if (ribbonTex != null)
        {
            if (img != null)
            {
                img.sprite = GetOrCreateSprite(ribbonTex);
                img.color = Color.white;
                float ratio = (float)ribbonTex.width / Mathf.Max(1, ribbonTex.height);
                RectTransform rt = img.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(26f * ratio, 26f);
            }
            if (textTr != null) textTr.gameObject.SetActive(false);
        }
        else
        {
            if (img != null)
            {
                img.sprite = null;
                img.color = new Color(0.92f, 0.20f, 0.20f, 0.95f);
            }
            if (textTr != null)
            {
                textTr.gameObject.SetActive(true);
                TMPro.TextMeshProUGUI tmp = textTr.GetComponent<TMPro.TextMeshProUGUI>();
                if (tmp != null) tmp.text = title;
            }
        }
    }

    GameObject CreateSpacer(Transform parent, float height)
    {
        GameObject spacer = new GameObject("Spacer");
        spacer.transform.SetParent(parent, false);

        RectTransform rt = spacer.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, height);

        LayoutElement le = spacer.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        separatorObjects.Add(spacer);
        return spacer;
    }

    /// <summary>
    /// Dipanggil oleh YardEditorManager setelah API selesai fetch.
    /// Menghapus loading indicator & membuat tombol untuk setiap yard dari API.
    /// Mendukung 1 kolom penuh (jika <= 4 yard) atau 2 kolom grid (jika > 4 yard) agar muat puluhan yard!
    /// </summary>
    public void RebuildYardButtons()
    {
        if (yardButtonContainer == null) return;

        // Bersihkan child lama (termasuk row parent jika 2 kolom)
        for (int c = yardButtonContainer.childCount - 1; c >= 0; c--)
        {
            Destroy(yardButtonContainer.GetChild(c).gameObject);
        }
        yardButtons.Clear();

        if (loadingLabel != null)
        {
            Destroy(loadingLabel);
            loadingLabel = null;
        }

        YardEditorManager mgr = YardEditorManager.Instance;
        if (mgr == null) return;

        List<MasterBlockData> blocks = mgr.GetMasterBlocks();

        if (blocks.Count == 0)
        {
            CreateLabel(yardButtonContainer, "NoDataLabel", "No yards from API", 12,
                new Color(1f, 0.6f, 0.2f, 1f), TextAnchor.MiddleCenter, 26);
            ForceRebuildSidebarLayout();
            return;
        }

        // Urutkan blok secara alfabetis agar teratur: Yard A, Yard B, Yard C, Yard D!
        blocks.Sort((a, b) => string.Compare(a.YARD_BLOCK, b.YARD_BLOCK, System.StringComparison.OrdinalIgnoreCase));

        // TATA LETAK ADAPTIF:
        // Jika yard <= 4: Tampil 1 kolom penuh (Full Width)
        // Jika yard > 4: Tampil 2 kolom (Grid Kiri-Kanan) agar muat puluhan yard tanpa membuat sidebar jebol!
        bool useTwoColumns = (blocks.Count > 4);

        if (useTwoColumns)
        {
            for (int i = 0; i < blocks.Count; i += 2)
            {
                Transform row = CreateHorizontalRow(yardButtonContainer, "YardRow_" + (i / 2), 32);
                AddSingleYardButton(row, blocks[i], i, true);

                if (i + 1 < blocks.Count)
                {
                    AddSingleYardButton(row, blocks[i + 1], i + 1, true);
                }
            }
        }
        else
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                AddSingleYardButton(yardButtonContainer, blocks[i], i, false);
            }
        }

        ForceRebuildSidebarLayout();
        Debug.Log("[YardEditorUI] " + blocks.Count + " yard buttons created from API (Layout: " + (useTwoColumns ? "2 Columns Grid" : "1 Column Full") + ")");
    }

    private void AddSingleYardButton(Transform parent, MasterBlockData block, int index, bool isCompactTwoCol)
    {
        string blockName = block.YARD_BLOCK;
        string displayName = block.YARD_BLOCK_NAME;
        if (string.IsNullOrEmpty(displayName)) displayName = blockName;

        int maxRow = 6, maxSlot = 45, maxTier = 7;
        int.TryParse(block.YARD_ROW, out maxRow);
        int.TryParse(block.YARD_SLOT, out maxSlot);
        int.TryParse(block.YARD_TIER, out maxTier);

        string btnText;
        if (isCompactTwoCol)
        {
            btnText = "+ Yard " + displayName + " (" + maxRow + "x" + maxSlot + ")";
        }
        else
        {
            btnText = "+ Yard " + displayName + "  (R:" + maxRow + " S:" + maxSlot + " T:" + maxTier + ")";
        }

        Color btnColor = GenerateBlockButtonColor(blockName, index);
        string capturedName = blockName;

        float h = isCompactTwoCol ? 32f : 36f;
        Button btn = CreateButton(parent, "YardBtn_" + blockName, btnText, btnColor, h, () =>
        {
            if (YardEditorManager.Instance != null)
            {
                YardEditorManager.Instance.SpawnYard(capturedName);
            }
        });

        Transform textTr = btn.transform.Find("Text");
        if (textTr != null)
        {
            TMPro.TextMeshProUGUI tmp = textTr.GetComponent<TMPro.TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize = isCompactTwoCol ? 11.5f : 12.5f;
                tmp.fontStyle = TMPro.FontStyles.Bold;
                tmp.color = Color.white;
            }
        }

        yardButtons[blockName] = btn;
    }

    // --- Opsi 3: Modal Dialog Pop-up Katalog Yard Blocks ---

    public void OpenYardCatalogModal()
    {
        if (catalogModalObj != null)
        {
            catalogModalObj.SetActive(true);
            RebuildCatalogCards();
        }
    }

    public void CloseYardCatalogModal()
    {
        if (catalogModalObj != null)
        {
            catalogModalObj.SetActive(false);
        }
    }

    void BuildYardCatalogModal(Transform canvasTransform)
    {
        // 1. Overlay Fullscreen Backdrop
        catalogModalObj = new GameObject("YardCatalogModal");
        catalogModalObj.transform.SetParent(canvasTransform, false);

        RectTransform modalRt = catalogModalObj.AddComponent<RectTransform>();
        modalRt.anchorMin = Vector2.zero;
        modalRt.anchorMax = Vector2.one;
        modalRt.offsetMin = Vector2.zero;
        modalRt.offsetMax = Vector2.zero;

        Image bgImage = catalogModalObj.AddComponent<Image>();
        bgImage.color = new Color(0.04f, 0.05f, 0.08f, 0.78f);

        // Klik backdrop luar -> tutup modal
        Button backdropBtn = catalogModalObj.AddComponent<Button>();
        backdropBtn.transition = Selectable.Transition.None;
        backdropBtn.onClick.AddListener(CloseYardCatalogModal);

        // 2. Dialog Window Tengah (Gaya Card Krem Cerah sesuai mockup user)
        GameObject dialog = new GameObject("CatalogDialog");
        dialog.transform.SetParent(catalogModalObj.transform, false);

        RectTransform dialogRt = dialog.AddComponent<RectTransform>();
        dialogRt.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRt.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRt.pivot = new Vector2(0.5f, 0.5f);
        dialogRt.sizeDelta = new Vector2(460, 480);

        Image dialogBg = dialog.AddComponent<Image>();
        dialogBg.sprite = GetProceduralPillSprite();
        dialogBg.type = Image.Type.Sliced;
        dialogBg.color = new Color(0.96f, 0.94f, 0.90f, 0.99f); // Krem cerah elegan
        dialog.AddComponent<GraphicRaycaster>();

        // Outline halus di tepi dialog
        Outline outline = dialog.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.2f, 0.25f, 0.25f);
        outline.effectDistance = new Vector2(2, -2);

        // Header Dialog
        GameObject headerObj = new GameObject("DialogHeader");
        headerObj.transform.SetParent(dialog.transform, false);
        RectTransform headerRt = headerObj.AddComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0, 1);
        headerRt.anchorMax = new Vector2(1, 1);
        headerRt.pivot = new Vector2(0.5f, 1);
        headerRt.sizeDelta = new Vector2(0, 52);
        headerRt.anchoredPosition = Vector2.zero;

        // Judul Header
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(headerObj.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 0);
        titleRt.anchorMax = new Vector2(1, 1);
        titleRt.offsetMin = new Vector2(18, 0);
        titleRt.offsetMax = new Vector2(-110, 0);

        TMPro.TextMeshProUGUI titleText = titleObj.AddComponent<TMPro.TextMeshProUGUI>();
        titleText.text = "Katalog Yard Blocks";
        titleText.fontSize = 17;
        titleText.fontStyle = TMPro.FontStyles.Bold;
        titleText.color = new Color(0.12f, 0.12f, 0.16f, 1f);
        titleText.alignment = TMPro.TextAlignmentOptions.MidlineLeft;

        // Tombol Close "Tutup (X)" di kanan atas (persis mockup user)
        GameObject closeObj = new GameObject("CloseBtn");
        closeObj.transform.SetParent(headerObj.transform, false);
        RectTransform closeRt = closeObj.AddComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1, 0.5f);
        closeRt.anchorMax = new Vector2(1, 0.5f);
        closeRt.pivot = new Vector2(1, 0.5f);
        closeRt.sizeDelta = new Vector2(90, 30);
        closeRt.anchoredPosition = new Vector2(-14, 0);

        Image closeImg = closeObj.AddComponent<Image>();
        closeImg.sprite = GetProceduralPillSprite();
        closeImg.type = Image.Type.Sliced;
        closeImg.color = new Color(0.85f, 0.83f, 0.78f, 1f);
        Button closeBtn = closeObj.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(CloseYardCatalogModal);

        GameObject xObj = new GameObject("XText");
        xObj.transform.SetParent(closeObj.transform, false);
        RectTransform xRt = xObj.AddComponent<RectTransform>();
        xRt.anchorMin = Vector2.zero;
        xRt.anchorMax = Vector2.one;
        TMPro.TextMeshProUGUI xText = xObj.AddComponent<TMPro.TextMeshProUGUI>();
        xText.text = "Tutup (X)";
        xText.fontSize = 12;
        xText.fontStyle = TMPro.FontStyles.Bold;
        xText.color = new Color(0.15f, 0.15f, 0.18f, 1f);
        xText.alignment = TMPro.TextAlignmentOptions.Center;

        // Scroll View Konten
        GameObject scrollObj = new GameObject("CatalogScrollView");
        scrollObj.transform.SetParent(dialog.transform, false);
        RectTransform sRt = scrollObj.AddComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0, 0);
        sRt.anchorMax = new Vector2(1, 1);
        sRt.offsetMin = new Vector2(14, 14);
        sRt.offsetMax = new Vector2(-14, -54);

        ScrollRect sRect = scrollObj.AddComponent<ScrollRect>();
        sRect.horizontal = false;
        sRect.vertical = true;
        sRect.scrollSensitivity = 30f;

        // Viewport
        GameObject vpObj = new GameObject("Viewport");
        vpObj.transform.SetParent(scrollObj.transform, false);
        RectTransform vpRt = vpObj.AddComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = Vector2.zero;
        vpRt.offsetMax = Vector2.zero;

        Image vpImg = vpObj.AddComponent<Image>();
        vpImg.color = new Color(0, 0, 0, 0.01f);
        Mask vpMask = vpObj.AddComponent<Mask>();
        vpMask.showMaskGraphic = false;
        sRect.viewport = vpRt;

        // Content
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(vpObj.transform, false);
        RectTransform cRt = contentObj.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0, 1);
        cRt.anchorMax = new Vector2(1, 1);
        cRt.pivot = new Vector2(0.5f, 1);
        cRt.offsetMin = Vector2.zero;
        cRt.offsetMax = Vector2.zero;
        sRect.content = cRt;

        // Grid Layout untuk Card Yard (3 kolom persis mockup contoh)
        GridLayoutGroup glg = contentObj.AddComponent<GridLayoutGroup>();
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 3;
        glg.cellSize = new Vector2(130, 138);
        glg.spacing = new Vector2(10, 10);
        glg.padding = new RectOffset(6, 6, 8, 8);
        glg.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        catalogCardsContainer = contentObj.transform;
        catalogModalObj.SetActive(false);
    }

    // Cache Sprite Kubus 3D Isometrik berdasar warna blok
    private Dictionary<Color, Sprite> cubeSpriteCache = new Dictionary<Color, Sprite>();

    Sprite GetOrCreateCubeSprite(Color baseColor)
    {
        if (cubeSpriteCache.ContainsKey(baseColor) && cubeSpriteCache[baseColor] != null)
            return cubeSpriteCache[baseColor];

        int size = 96;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        // Warna 3 sisi kubus (top terang, left sedang, right gelap/bayangan)
        Color cTop = Color.Lerp(baseColor, Color.white, 0.40f);
        Color cLeft = baseColor;
        Color cRight = Color.Lerp(baseColor * 0.72f, Color.black, 0.12f);
        cTop.a = 1f;
        cLeft.a = 1f;
        cRight.a = 1f;
        Color cEdge = Color.Lerp(baseColor * 0.45f, new Color(0.12f, 0.12f, 0.15f, 1f), 0.55f);

        float cx = size * 0.5f;
        float cy = size * 0.56f;
        float w = size * 0.38f;
        float hTop = size * 0.20f;
        float hCube = size * 0.34f;

        Vector2 pLeft = new Vector2(cx - w, cy);
        Vector2 pCenter = new Vector2(cx, cy - hTop);
        Vector2 pRight = new Vector2(cx + w, cy);
        Vector2 pTop = new Vector2(cx, cy + hTop);

        Vector2 pCubeBottomLeft = new Vector2(cx - w, cy - hCube);
        Vector2 pCubeBottomCenter = new Vector2(cx, cy - hTop - hCube);
        Vector2 pCubeBottomRight = new Vector2(cx + w, cy - hCube);

        // Render 3 sisi kubus isometrik
        DrawConvexQuad(pixels, size, pLeft, pCenter, pRight, pTop, cTop, cEdge); // Atas
        DrawConvexQuad(pixels, size, pCubeBottomLeft, pCubeBottomCenter, pCenter, pLeft, cLeft, cEdge); // Kiri
        DrawConvexQuad(pixels, size, pCubeBottomCenter, pCubeBottomRight, pRight, pCenter, cRight, cEdge); // Kanan

        tex.SetPixels(pixels);
        tex.Apply();

        Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        cubeSpriteCache[baseColor] = sp;
        return sp;
    }

    void DrawConvexQuad(Color[] pixels, int size, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color fillColor, Color edgeColor)
    {
        int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(Mathf.Min(p0.x, p1.x), Mathf.Min(p2.x, p3.x))), 0, size - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(Mathf.Max(p0.x, p1.x), Mathf.Max(p2.x, p3.x))), 0, size - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(Mathf.Min(p0.y, p1.y), Mathf.Min(p2.y, p3.y))), 0, size - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(Mathf.Max(p0.y, p1.y), Mathf.Max(p2.y, p3.y))), 0, size - 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 pt = new Vector2(x + 0.5f, y + 0.5f);
                float c0 = (p1.x - p0.x) * (pt.y - p0.y) - (p1.y - p0.y) * (pt.x - p0.x);
                float c1 = (p2.x - p1.x) * (pt.y - p1.y) - (p2.y - p1.y) * (pt.x - p1.x);
                float c2 = (p3.x - p2.x) * (pt.y - p2.y) - (p3.y - p2.y) * (pt.x - p2.x);
                float c3 = (p0.x - p3.x) * (pt.y - p3.y) - (p0.y - p3.y) * (pt.x - p3.x);

                if ((c0 >= 0 && c1 >= 0 && c2 >= 0 && c3 >= 0) || (c0 <= 0 && c1 <= 0 && c2 <= 0 && c3 <= 0))
                {
                    pixels[y * size + x] = fillColor;
                }
            }
        }
    }

    void RebuildCatalogCards()
    {
        if (catalogCardsContainer == null) return;
        for (int i = catalogCardsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(catalogCardsContainer.GetChild(i).gameObject);
        }

        YardEditorManager mgr = YardEditorManager.Instance;
        if (mgr == null) return;

        List<MasterBlockData> blocks = mgr.GetMasterBlocks();
        blocks.Sort((a, b) => string.Compare(a.YARD_BLOCK, b.YARD_BLOCK, System.StringComparison.OrdinalIgnoreCase));

        int spawnedCount = 0;
        for (int i = 0; i < blocks.Count; i++)
        {
            if (mgr.IsYardSpawned(blocks[i].YARD_BLOCK)) spawnedCount++;
        }

        for (int i = 0; i < blocks.Count; i++)
        {
            MasterBlockData block = blocks[i];
            string blockName = block.YARD_BLOCK;
            string displayName = block.YARD_BLOCK_NAME;
            if (string.IsNullOrEmpty(displayName)) displayName = blockName;

            bool spawned = mgr.IsYardSpawned(blockName);
            Color blockCol = GenerateBlockButtonColor(blockName, i);

            // Card Object (Krem keabuan sesuai mockup user)
            GameObject card = new GameObject("Card_" + blockName);
            card.transform.SetParent(catalogCardsContainer, false);
            RectTransform cardRt = card.AddComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(130, 138);

            Image cardBg = card.AddComponent<Image>();
            cardBg.sprite = GetProceduralPillSprite();
            cardBg.type = Image.Type.Sliced;
            cardBg.color = spawned ? new Color(0.82f, 0.81f, 0.79f, 0.75f) : new Color(0.88f, 0.86f, 0.82f, 1f);

            // 1. Gambar Kubus 3D Isometrik di atas
            GameObject cubeObj = new GameObject("Cube");
            cubeObj.transform.SetParent(card.transform, false);
            RectTransform cubeRt = cubeObj.AddComponent<RectTransform>();
            cubeRt.anchorMin = new Vector2(0.5f, 1f);
            cubeRt.anchorMax = new Vector2(0.5f, 1f);
            cubeRt.pivot = new Vector2(0.5f, 1f);
            cubeRt.anchoredPosition = new Vector2(0, -8);
            cubeRt.sizeDelta = new Vector2(52, 52);

            Image cubeImg = cubeObj.AddComponent<Image>();
            cubeImg.sprite = GetOrCreateCubeSprite(blockCol);
            cubeImg.type = Image.Type.Simple;
            cubeImg.preserveAspect = true;
            cubeImg.color = spawned ? new Color(0.65f, 0.65f, 0.65f, 0.70f) : Color.white;

            // 2. Teks Nama Yard (contoh: "Yard A", "Yard B")
            GameObject nameObj = new GameObject("YardName");
            nameObj.transform.SetParent(card.transform, false);
            RectTransform nameRt = nameObj.AddComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0, 0);
            nameRt.anchorMax = new Vector2(1, 1);
            nameRt.offsetMin = new Vector2(4, 34);
            nameRt.offsetMax = new Vector2(-4, -62);

            TMPro.TextMeshProUGUI nameText = nameObj.AddComponent<TMPro.TextMeshProUGUI>();
            nameText.text = "Yard " + displayName;
            nameText.fontSize = 13;
            nameText.fontStyle = TMPro.FontStyles.Bold;
            nameText.color = spawned ? new Color(0.50f, 0.50f, 0.55f, 1f) : new Color(0.12f, 0.12f, 0.15f, 1f);
            nameText.alignment = TMPro.TextAlignmentOptions.Center;

            // 3. Tombol Kapsul "Pilih" di bawah (sesuai contoh mockup user)
            GameObject actBtnObj = new GameObject("ActionBtn");
            actBtnObj.transform.SetParent(card.transform, false);
            RectTransform actRt = actBtnObj.AddComponent<RectTransform>();
            actRt.anchorMin = new Vector2(0.5f, 0);
            actRt.anchorMax = new Vector2(0.5f, 0);
            actRt.pivot = new Vector2(0.5f, 0);
            actRt.sizeDelta = new Vector2(92, 26);
            actRt.anchoredPosition = new Vector2(0, 8);

            Image actImg = actBtnObj.AddComponent<Image>();
            actImg.sprite = GetProceduralPillSprite();
            actImg.type = Image.Type.Sliced;
            Button actBtn = actBtnObj.AddComponent<Button>();
            actBtn.targetGraphic = actImg;

            GameObject actTextObj = new GameObject("Text");
            actTextObj.transform.SetParent(actBtnObj.transform, false);
            RectTransform atRt = actTextObj.AddComponent<RectTransform>();
            atRt.anchorMin = Vector2.zero;
            atRt.anchorMax = Vector2.one;
            TMPro.TextMeshProUGUI atText = actTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            atText.alignment = TMPro.TextAlignmentOptions.Center;
            atText.fontSize = 11.5f;
            atText.fontStyle = TMPro.FontStyles.Bold;

            if (spawned)
            {
                actImg.color = new Color(0.78f, 0.78f, 0.80f, 0.7f);
                actBtn.interactable = false;
                atText.text = "Terpasang";
                atText.color = new Color(0.45f, 0.45f, 0.50f, 1f);
            }
            else
            {
                actImg.color = new Color(0.68f, 0.70f, 0.76f, 1f); // Abu-abu kapsul sesuai mockup
                actBtn.interactable = true;
                atText.text = "Pilih";
                atText.color = new Color(0.12f, 0.12f, 0.15f, 1f);

                string capturedName = blockName;
                actBtn.onClick.AddListener(() =>
                {
                    if (YardEditorManager.Instance != null)
                    {
                        YardEditorManager.Instance.SpawnYard(capturedName);
                    }
                    CloseYardCatalogModal();
                    UpdateCatalogOpenButton();
                });
            }
        }
    }

    void UpdateCatalogOpenButton()
    {
        if (openCatalogBtn == null) return;
        YardEditorManager mgr = YardEditorManager.Instance;
        if (mgr == null) return;

        Image img = openCatalogBtn.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = GetProceduralPillSprite();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.12f, 0.65f, 0.33f, 1f); // Kapsul Hijau cerah sesuai mockup
        }

        Transform textTr = openCatalogBtn.transform.Find("Text");
        if (textTr != null)
        {
            TMPro.TextMeshProUGUI tmp = textTr.GetComponent<TMPro.TextMeshProUGUI>();
            if (tmp != null)
            {
                if (!mgr.IsApiLoaded())
                {
                    tmp.text = "Memuat Yard...";
                    tmp.color = new Color(0.9f, 0.9f, 0.9f, 0.7f);
                }
                else
                {
                    tmp.text = "+ Buka Katalog Yard";
                    tmp.color = Color.white;
                    tmp.fontSize = 13.5f;
                    tmp.fontStyle = TMPro.FontStyles.Bold;
                }
            }
        }
    }

    void BuildStatusBar(Transform canvasTransform)
    {
        statusBar = CreatePanel(canvasTransform, "StatusBar", new Color(0.05f, 0.05f, 0.08f, 0.88f));
        RectTransform statusRect = statusBar.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 0);
        statusRect.anchorMax = new Vector2(1, 0);
        statusRect.pivot = new Vector2(0.5f, 0);
        statusRect.offsetMin = new Vector2(10, 10);
        statusRect.offsetMax = new Vector2(-315, 42);

        GameObject textObj = new GameObject("StatusText");
        textObj.transform.SetParent(statusBar.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12, 2);
        textRect.offsetMax = new Vector2(-12, -2);

        statusText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        statusText.text = "Yard Editor Ready";
        statusText.fontSize = 13;
        statusText.color = textMuted;
        statusText.alignment = TMPro.TextAlignmentOptions.Left;

        statusBar.SetActive(false);
    }

    // --- Update Logic ---

    void UpdateLoadingState()
    {
        YardEditorManager mgr = YardEditorManager.Instance;
        if (mgr != null && mgr.IsApiLoaded() && yardButtons.Count == 0 && loadingLabel != null)
        {
            RebuildYardButtons();
        }
    }

    void UpdateButtonStates()
    {
        YardEditorManager mgr = YardEditorManager.Instance;
        if (mgr == null) return;

        bool hasCustomBg = (mgr.pngSidebarBackground != null);

        // Background sidebar PNG (opsional)
        if (sidebarPanel != null)
        {
            Image panelImg = sidebarPanel.GetComponent<Image>();
            if (panelImg != null)
            {
                if (hasCustomBg)
                {
                    panelImg.sprite = GetOrCreateSprite(mgr.pngSidebarBackground);
                    panelImg.type = Image.Type.Simple;
                    panelImg.preserveAspect = false;
                    panelImg.color = Color.white;
                }
                else
                {
                    panelImg.sprite = null;
                    panelImg.color = panelBgColor;
                }
            }
        }

        // Matikan header teks manual jika background bgg.png aktif (sudah ada tulisan Yard Editor di gambar)
        if (headerLabelObj != null) headerLabelObj.SetActive(false);
        if (helpLabelObj != null) helpLabelObj.SetActive(!hasCustomBg);

        // Update Ribbon Section Headers dinamis
        ApplyRibbonTexture(ribbonYardObj, mgr.pngRibbonYard, "Yard Blocks");
        ApplyRibbonTexture(ribbonRoadObj, mgr.pngRibbonRoads, "Roads");
        ApplyRibbonTexture(ribbonWallObj, mgr.pngRibbonWalls, "Walls");
        ApplyRibbonTexture(ribbonAccObj, mgr.pngRibbonAccessories, "Accessories");
        ApplyRibbonTexture(ribbonToolsObj, mgr.pngRibbonTools, "Tools");
        ApplyRibbonTexture(ribbonFileObj, mgr.pngRibbonFile, "File");

        // Dynamic Yard Block Buttons
        foreach (var kvp in yardButtons)
        {
            string blockName = kvp.Key;
            Button btn = kvp.Value;
            if (btn == null) continue;

            bool spawned = mgr.IsYardSpawned(blockName);
            btn.interactable = true; // Selalu aktif agar user bisa klik untuk menyeleksi atau spawn!

            Texture2D yardPng = mgr.GetYardButtonPng(blockName);
            Color blockCol = GetCachedBlockColor(blockName);
            ApplyButtonPng(btn, yardPng, blockCol, false);

            // Jika mode dinamis/procedural: update teks status terpasang
            if (yardPng == null)
            {
                Transform textTr = btn.transform.Find("Text");
                if (textTr != null)
                {
                    TMPro.TextMeshProUGUI tmp = textTr.GetComponent<TMPro.TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        if (spawned)
                        {
                            tmp.text = blockName + " (Pilih/Edit)";
                            tmp.color = new Color(1f, 0.95f, 0.5f, 1f);
                        }
                        else
                        {
                            tmp.text = "+ Yard " + blockName;
                            tmp.color = Color.white;
                        }
                    }
                }
            }
        }

        // Fixed Buttons PNG & State
        ApplyButtonPng(addRoadBtn, mgr.pngAddRoad, buttonRoad);
        ApplyButtonPng(roadWMinusBtn, mgr.pngRoadWidthMinus, buttonNormal);
        ApplyButtonPng(roadWPlusBtn, mgr.pngRoadWidthPlus, buttonNormal);
        ApplyButtonPng(roadLMinusBtn, mgr.pngRoadLengthMinus, buttonNormal);
        ApplyButtonPng(roadLPlusBtn, mgr.pngRoadLengthPlus, buttonNormal);

        ApplyButtonPng(addPagarBtn, mgr.pngAddPagar, buttonRoad);
        ApplyButtonPng(pagarLMinusBtn, mgr.pngPagarLengthMinus, buttonNormal);
        ApplyButtonPng(pagarLPlusBtn, mgr.pngPagarLengthPlus, buttonNormal);

        ApplyButtonPng(addGedungBtn, mgr.pngAddGedung, buttonAccessory);
        ApplyButtonPng(addGateBtn, mgr.pngAddGate, buttonAccessory);
        ApplyButtonPng(addReachStackerBtn, mgr.pngAddReachStacker, buttonAccessory);
        ApplyButtonPng(addTopLoaderBtn, mgr.pngAddTopLoader, buttonAccessory);
        ApplyButtonPng(accScaleMinusBtn, mgr.pngScaleMinus, buttonNormal);
        ApplyButtonPng(accScalePlusBtn, mgr.pngScalePlus, buttonNormal);

        bool hasSelection = (mgr.GetSelectedYard() != null || mgr.GetSelectedRoad() != null || mgr.GetSelectedAccessory() != null);
        if (rotateBtn != null) rotateBtn.interactable = hasSelection;
        if (deleteBtn != null) deleteBtn.interactable = hasSelection;

        ApplyButtonPng(rotateBtn, mgr.pngRotate, buttonRotate, false);
        ApplyButtonPng(deleteBtn, mgr.pngDeleteSelected, buttonDanger, false);
        ApplyButtonPng(saveBtn, mgr.pngSaveLayout, buttonSave);
        ApplyButtonPng(loadBtn, mgr.pngLoadLayout, buttonNormal);
        ApplyButtonPng(clearBtn, mgr.pngClearAll, buttonDanger);
    }

    private Dictionary<string, Color> blockColorCache = new Dictionary<string, Color>();

    Color GetCachedBlockColor(string blockName)
    {
        if (blockColorCache.ContainsKey(blockName)) return blockColorCache[blockName];
        return buttonNormal;
    }

    Color GenerateBlockButtonColor(string blockName, int index)
    {
        string clean = blockName.Replace("Yard", "").Replace("YARD", "").Trim().ToUpper();
        Color color;
        switch (clean)
        {
            case "A": color = new Color(0.18f, 0.68f, 0.32f, 1f); break; // Emerald Green
            case "B": color = new Color(0.92f, 0.50f, 0.12f, 1f); break; // Warm Amber Orange
            case "C": color = new Color(0.10f, 0.62f, 0.65f, 1f); break; // Cyan Ocean
            case "D": color = new Color(0.58f, 0.34f, 0.74f, 1f); break; // Amethyst Purple
            case "E": color = new Color(0.82f, 0.22f, 0.22f, 1f); break; // Crimson
            case "F": color = new Color(0.18f, 0.48f, 0.82f, 1f); break; // Cobalt Blue
            default:
                float hue = ((float)index * 0.28f + 0.15f) % 1f;
                color = Color.HSVToRGB(hue, 0.70f, 0.62f);
                break;
        }
        blockColorCache[blockName] = color;
        return color;
    }

    private string toastMessage = "";
    private float toastEndTime = 0f;

    public void ShowToastNotification(string message, float duration = 3.5f)
    {
        toastMessage = message;
        toastEndTime = Time.unscaledTime + duration;
    }

    void DrawToastNotification()
    {
        // Dinonaktifkan sesuai permintaan: hilangkan kotak hitam toast pop-up di bawah tombol view
        return;
    }

    void UpdateStatusText()
    {
        if (statusText == null) return;
        YardEditorManager mgr = YardEditorManager.Instance;
        if (mgr == null) return;

        if (Time.unscaledTime < toastEndTime && !string.IsNullOrEmpty(toastMessage))
        {
            statusText.text = toastMessage;
            statusText.color = new Color(0.25f, 0.95f, 0.45f, 1f);
            return;
        }
        else
        {
            statusText.color = textMuted;
        }

        string status = "Yard Editor (2D View)  |  ";

        if (!mgr.IsApiLoaded())
        {
            status += "Loading API...";
            statusText.text = status;
            return;
        }

        status += "Placed: " + mgr.GetActiveYards().Count + " yards, " +
                  mgr.GetActiveRoads().Count + " roads, " +
                  mgr.GetActiveAccessories().Count + " aksesoris";

        DraggableYard sy = mgr.GetSelectedYard();
        DraggableRoad sr = mgr.GetSelectedRoad();
        DraggableAccessory sa = mgr.GetSelectedAccessory();

        if (sy != null)
        {
            status += "  |  Selected: Yard " + sy.blockName;
            status += " (" + (sy.isHorizontal ? "Horizontal" : "Vertical") + ")";
            status += " [R:" + sy.maxRow + " S:" + sy.maxSlot + " T:" + sy.maxTier + "]";
            if (sy.isDragging) status += " [DRAGGING]";
        }
        else if (sr != null)
        {
            status += "  |  Selected: Road " + sr.roadId;
            status += " (" + sr.directionAngle + " deg)";
            if (sr.isDragging) status += " [DRAGGING]";
        }
        else if (sa != null)
        {
            status += "  |  Selected: " + sa.GetDisplayName() + " #" + sa.accessoryId;
            if (sa.accessoryType == "Pagar")
            {
                status += " (" + sa.directionAngle + " deg, Length: " + Mathf.RoundToInt(sa.fenceLength) + "m)";
            }
            else
            {
                status += " (" + sa.directionAngle + " deg, Scale: " + sa.scaleMultiplier.ToString("F1") + "x)";
            }
            if (sa.isDragging) status += " [DRAGGING]";
        }

        statusText.text = status;
    }

    // --- UI Factory Helpers ---

    GameObject CreatePanel(Transform parent, string name, Color bgColor)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);

        panel.AddComponent<RectTransform>();
        Image img = panel.AddComponent<Image>();
        img.color = bgColor;

        return panel;
    }

    void CreateLabel(Transform parent, string name, string text, int fontSize, Color color, TextAnchor alignment, float height)
    {
        CreateLabelReturnable(parent, name, text, fontSize, color, alignment, height);
    }

    GameObject CreateLabelReturnable(Transform parent, string name, string text, int fontSize, Color color, TextAnchor alignment, float height)
    {
        GameObject labelObj = new GameObject(name);
        labelObj.transform.SetParent(parent, false);

        RectTransform rt = labelObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, height);

        LayoutElement le = labelObj.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        TMPro.TextMeshProUGUI tmp = labelObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = alignment == TextAnchor.MiddleCenter ?
            TMPro.TextAlignmentOptions.Center : TMPro.TextAlignmentOptions.Left;
        tmp.textWrappingMode = TMPro.TextWrappingModes.Normal;

        return labelObj;
    }

    Button CreateButton(Transform parent, string name, string text, Color bgColor, float height, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, height);

        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.90f, 0.90f, 0.90f, 0.95f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        btn.colors = colors;

        btn.onClick.AddListener(onClick);
        btnObj.AddComponent<YardButtonHoverFX>();

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(6, 2);
        textRt.offsetMax = new Vector2(-6, -2);

        TMPro.TextMeshProUGUI tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 13;
        tmp.color = textColor;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;

        return btn;
    }

    GameObject CreateSeparator(Transform parent)
    {
        GameObject sep = new GameObject("Separator");
        sep.transform.SetParent(parent, false);

        RectTransform rt = sep.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 1);

        LayoutElement le = sep.AddComponent<LayoutElement>();
        le.preferredHeight = 1;
        le.minHeight = 1;

        Image img = sep.AddComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.35f, 0.5f);
        separatorObjects.Add(sep);
        return sep;
    }

    // Direct OnGUI Launcher Button & Container Info Popup
    void OnGUI()
    {
        if (!Application.isPlaying) return;

        // Sembunyikan tombol Yard Editor saat di HP (Viewer Mode) agar layar VR kacamata bersih
        if (VRRemoteSyncManager.Instance != null && VRRemoteSyncManager.Instance.isViewer)
            return;

        YardEditorManager mgr = YardEditorManager.Instance;
        if (mgr == null) return;

        // Saat di luar Yard Editor: tampilkan popup Info Kontainer jika ada container yang diklik
        if (!mgr.isEditorActive)
        {
            ContainerLocData selectedData = mgr.GetClickedContainerData();
            if (selectedData != null)
            {
                Rect cardRect = mgr.GetInfoCardRect();
                Texture2D bgTex = mgr.pngInfoKontainerBg != null ? mgr.pngInfoKontainerBg : (ContainerManager.Instance != null ? ContainerManager.Instance.pngInfoKontainerBg : null);
                Texture2D closeTex = mgr.pngInfoKontainerClose != null ? mgr.pngInfoKontainerClose : (ContainerManager.Instance != null ? ContainerManager.Instance.pngInfoKontainerClose : null);

                float cardScale = mgr.infoCardScale;
                if (bgTex != null)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(cardRect, bgTex, ScaleMode.StretchToFill, true);
                }
                else
                {
                    Color prevC = GUI.color;
                    GUI.color = new Color(0.10f, 0.12f, 0.16f, 0.95f);
                    GUI.DrawTexture(cardRect, ContainerManager.GetRoundedBoxTexture(), ScaleMode.StretchToFill, true);
                    GUI.color = prevC;
                }

                // Tombol Close (X) dengan Margin/Padding Rapi & Tanpa Efek Hover
                float btnW = mgr.closeButtonSize.x * cardScale;
                float btnH = mgr.closeButtonSize.y * cardScale;
                float closeX = cardRect.x + cardRect.width - btnW - (mgr.closeButtonMarginRight * cardScale) + (mgr.closeButtonOffset.x * cardScale);
                float closeY = cardRect.y + (mgr.closeButtonMarginTop * cardScale) + (mgr.closeButtonOffset.y * cardScale);
                Rect closeRect = new Rect(closeX, closeY, btnW, btnH);

                if (closeTex != null)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(closeRect, closeTex, ScaleMode.ScaleToFit, true);

                    if (GUI.Button(closeRect, GUIContent.none, GUIStyle.none))
                    {
                        mgr.ClearClickedContainerData();
                    }
                }
                else
                {
                    GUIStyle closeBtnStyle = new GUIStyle(GUI.skin.button);
                    closeBtnStyle.fontSize = Mathf.RoundToInt(12 * cardScale);
                    closeBtnStyle.fontStyle = FontStyle.Bold;
                    if (GUI.Button(closeRect, "X", closeBtnStyle))
                    {
                        mgr.ClearClickedContainerData();
                    }
                }

                bool useModern = mgr.useModernCardLayout;
                CardTextTheme theme = mgr.cardTextTheme;

                if (useModern)
                {
                    float s = cardScale;
                    bool isDarkCard = (theme == CardTextTheme.LightText) || 
                                      (theme == CardTextTheme.Auto && bgTex == null);

                    Color titleColor = isDarkCard ? Color.white : new Color(0.12f, 0.14f, 0.18f, 1f);
                    Color dividerColor = isDarkCard ? new Color(1f, 1f, 1f, 0.18f) : new Color(0.10f, 0.12f, 0.16f, 0.16f);

                    Color plateBg = isDarkCard ? new Color(1f, 1f, 1f, 0.12f) : new Color(0.10f, 0.13f, 0.18f, 0.94f);
                    Color plateTagBg = new Color(0.95f, 0.72f, 0.15f, 1f);
                    Color plateTagText = new Color(0.12f, 0.10f, 0.05f, 1f);
                    Color plateNoText = Color.white;

                    Color chipBg = isDarkCard ? new Color(1f, 1f, 1f, 0.10f) : new Color(0.10f, 0.12f, 0.16f, 0.08f);
                    Color chipText = isDarkCard ? new Color(0.92f, 0.94f, 0.98f, 1f) : new Color(0.12f, 0.15f, 0.20f, 1f);

                    Color gridBoxBg = isDarkCard ? new Color(1f, 1f, 1f, 0.08f) : new Color(0.10f, 0.12f, 0.16f, 0.07f);
                    Color gridLabelColor = isDarkCard ? new Color(0.70f, 0.73f, 0.80f, 1f) : new Color(0.40f, 0.38f, 0.35f, 1f);
                    Color gridValColor = isDarkCard ? Color.white : new Color(0.08f, 0.10f, 0.14f, 1f);

                    Color footerColor = isDarkCard ? new Color(0.80f, 0.83f, 0.90f, 1f) : new Color(0.25f, 0.26f, 0.32f, 1f);

                    float contentX = cardRect.x + mgr.contentPadding.x * s;
                    float contentW = cardRect.width - (mgr.contentPadding.x * 2f * s);
                    float currY = cardRect.y + (mgr.contentPadding.y > 0 ? (mgr.contentPadding.y * s) : 10f * s);

                    // 1. Header
                    if (mgr.showHeaderTitle && !string.IsNullOrEmpty(mgr.headerTitle))
                    {
                        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
                        titleStyle.fontSize = Mathf.Max(11, Mathf.RoundToInt(13f * s));
                        titleStyle.fontStyle = FontStyle.Bold;
                        titleStyle.alignment = TextAnchor.MiddleCenter;
                        ContainerManager.SetStyleTextColor(titleStyle, titleColor);

                        GUI.Label(new Rect(cardRect.x, currY, cardRect.width, 20f * s), mgr.headerTitle.ToUpper(), titleStyle);
                        currY += 22f * s;

                        Color prev = GUI.color;
                        GUI.color = dividerColor;
                        GUI.DrawTexture(new Rect(contentX, currY, contentW, 1.5f), Texture2D.whiteTexture);
                        GUI.color = prev;
                        currY += 6f * s;
                    }
                    else
                    {
                        currY += 8f * s;
                    }

                    // 2. Plat CTR NO (Rectangle bersih dengan sudut rounded 5px)
                    float plateH = 34f * s;
                    Rect plateRect = new Rect(contentX, currY, contentW, plateH);
                    ContainerManager.DrawRoundedRect(plateRect, plateBg, 5);

                    float tagW = 50f * s;
                    float tagH = 20f * s;
                    Rect tagRect = new Rect(plateRect.x + 6f * s, plateRect.y + (plateH - tagH) * 0.5f, tagW, tagH);
                    ContainerManager.DrawRoundedRect(tagRect, plateTagBg, 4);

                    GUIStyle tagStyle = new GUIStyle(GUI.skin.label);
                    tagStyle.fontSize = Mathf.Max(9, Mathf.RoundToInt(9.5f * s));
                    tagStyle.fontStyle = FontStyle.Bold;
                    tagStyle.alignment = TextAnchor.MiddleCenter;
                    ContainerManager.SetStyleTextColor(tagStyle, plateTagText);
                    GUI.Label(tagRect, "CTR NO", tagStyle);

                    string rawNo = selectedData.CTR_NO ?? "";
                    string formattedNo = (rawNo.Length >= 7 && char.IsLetter(rawNo[0]) && char.IsLetter(rawNo[1]) && char.IsLetter(rawNo[2]) && char.IsLetter(rawNo[3]))
                        ? rawNo.Substring(0, 4) + " " + rawNo.Substring(4) : rawNo;

                    GUIStyle noStyle = new GUIStyle(GUI.skin.label);
                    noStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(14.5f * s));
                    noStyle.fontStyle = FontStyle.Bold;
                    noStyle.alignment = TextAnchor.MiddleLeft;
                    ContainerManager.SetStyleTextColor(noStyle, plateNoText);
                    float noX = tagRect.x + tagW + 8f * s;
                    GUI.Label(new Rect(noX, plateRect.y, plateRect.width - (noX - plateRect.x) - 4f * s, plateH), formattedNo, noStyle);

                    currY += plateH + 7f * s;

                    // 3. Chips Spesifikasi
                    string sizeStr = (string.IsNullOrEmpty(selectedData.CTR_SIZE) ? "40" : selectedData.CTR_SIZE) + " FT";
                    string isoStr = string.IsNullOrEmpty(selectedData.ISO_CODE) ? "-" : selectedData.ISO_CODE;
                    string agentStr = string.IsNullOrEmpty(selectedData.AGENT) ? (string.IsNullOrEmpty(selectedData.CTR_MERK) ? "-" : selectedData.CTR_MERK) : selectedData.AGENT;

                    float chipGap = 5f * s;
                    float chipW = (contentW - (chipGap * 2f)) / 3f;
                    float chipH = 22f * s;

                    GUIStyle chipStyle = new GUIStyle(GUI.skin.label);
                    chipStyle.fontSize = Mathf.Max(9, Mathf.RoundToInt(10.5f * s));
                    chipStyle.fontStyle = FontStyle.Bold;
                    chipStyle.alignment = TextAnchor.MiddleCenter;
                    ContainerManager.SetStyleTextColor(chipStyle, chipText);

                    void DrawChip(Rect r, string text)
                    {
                        ContainerManager.DrawRoundedRect(r, chipBg, 4);
                        GUI.Label(r, text, chipStyle);
                    }

                    DrawChip(new Rect(contentX, currY, chipW, chipH), sizeStr);
                    DrawChip(new Rect(contentX + chipW + chipGap, currY, chipW, chipH), "ISO: " + isoStr);
                    DrawChip(new Rect(contentX + (chipW + chipGap) * 2f, currY, chipW, chipH), agentStr);

                    currY += chipH + 8f * s;

                    // 4. Grid Lokasi (4 Kolom)
                    float gridGap = 5f * s;
                    float boxW = (contentW - (gridGap * 3f)) / 4f;
                    float boxH = 48f * s;

                    GUIStyle gridLblStyle = new GUIStyle(GUI.skin.label);
                    gridLblStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(9.5f * s));
                    gridLblStyle.fontStyle = FontStyle.Bold;
                    gridLblStyle.alignment = TextAnchor.MiddleCenter;
                    ContainerManager.SetStyleTextColor(gridLblStyle, gridLabelColor);

                    GUIStyle gridValStyle = new GUIStyle(GUI.skin.label);
                    gridValStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(15.5f * s));
                    gridValStyle.fontStyle = FontStyle.Bold;
                    gridValStyle.alignment = TextAnchor.MiddleCenter;
                    ContainerManager.SetStyleTextColor(gridValStyle, gridValColor);

                    string Pad2(string val)
                    {
                        if (string.IsNullOrEmpty(val)) return "-";
                        int num;
                        if (int.TryParse(val, out num) && num >= 0 && num < 10) return num.ToString("D2");
                        return val;
                    }

                    void DrawLocationBox(int idx, string label, string val)
                    {
                        Rect bRect = new Rect(contentX + idx * (boxW + gridGap), currY, boxW, boxH);
                        ContainerManager.DrawRoundedRect(bRect, gridBoxBg, 4);

                        GUI.Label(new Rect(bRect.x, bRect.y + 4f * s, bRect.width, 15f * s), label, gridLblStyle);
                        GUI.Label(new Rect(bRect.x, bRect.y + 18f * s, bRect.width, 26f * s), val, gridValStyle);
                    }

                    DrawLocationBox(0, "BLOCK", string.IsNullOrEmpty(selectedData.YARD_BLOCK) ? "-" : selectedData.YARD_BLOCK.ToUpper());
                    DrawLocationBox(1, "SLOT", Pad2(selectedData.YARD_SLOT));
                    DrawLocationBox(2, "ROW", Pad2(selectedData.YARD_ROW));
                    DrawLocationBox(3, "TIER", Pad2(selectedData.YARD_TIER));

                    currY += boxH + 8f * s;

                    // 5. Divider
                    Color prevDiv = GUI.color;
                    GUI.color = dividerColor;
                    GUI.DrawTexture(new Rect(contentX, currY, contentW, 1.5f), Texture2D.whiteTexture);
                    GUI.color = prevDiv;
                    currY += 5f * s;

                    // 6. Footer Tare
                    GUIStyle footerStyle = new GUIStyle(GUI.skin.label);
                    footerStyle.fontSize = Mathf.Max(9, Mathf.RoundToInt(10.5f * s));
                    footerStyle.fontStyle = FontStyle.Bold;
                    footerStyle.alignment = TextAnchor.MiddleLeft;
                    ContainerManager.SetStyleTextColor(footerStyle, footerColor);

                    string tareRaw = selectedData.TARE ?? "";
                    tareRaw = tareRaw.Replace("kg", "").Replace("KG", "").Replace(",", "").Trim();
                    int tareVal;
                    string tareStr = int.TryParse(tareRaw, out tareVal) ? string.Format("{0:N0} KG", tareVal) : (string.IsNullOrEmpty(tareRaw) ? "-" : tareRaw + " KG");

                    GUI.Label(new Rect(contentX + 2f * s, currY, contentW - 4f * s, 18f * s), "TARE WEIGHT : " + tareStr, footerStyle);
                }
                else
                {
                    float startX = cardRect.x + mgr.contentPadding.x * cardScale;
                    float startY = cardRect.y + mgr.contentPadding.y * cardScale;
                    float lineH = 20f * cardScale;
                    float labelW = cardRect.width - (mgr.contentPadding.x * 2f * cardScale);

                    GUIStyle rowStyle = new GUIStyle(GUI.skin.label);
                    rowStyle.fontSize = Mathf.Max(10, Mathf.RoundToInt(mgr.infoFontSize * cardScale));
                    rowStyle.fontStyle = FontStyle.Bold;
                    Color rowCol = (theme == CardTextTheme.Auto || theme == CardTextTheme.DarkText) ? new Color(0.12f, 0.14f, 0.18f, 1f) : mgr.infoTextColor;
                    ContainerManager.SetStyleTextColor(rowStyle, rowCol);
                    rowStyle.alignment = TextAnchor.MiddleLeft;

                    void DrawRow(string label, string val)
                    {
                        GUI.Label(new Rect(startX, startY, labelW, lineH), string.Format("{0,-9}: {1}", label, val), rowStyle);
                        startY += lineH;
                    }

                    DrawRow("CTR NO", selectedData.CTR_NO);
                    DrawRow("SIZE", selectedData.CTR_SIZE + "ft");
                    DrawRow("AGENT", selectedData.AGENT);
                    DrawRow("BLOCK", selectedData.YARD_BLOCK);
                    DrawRow("SLOT", selectedData.YARD_SLOT);
                    DrawRow("ROW", selectedData.YARD_ROW);
                    DrawRow("TIER", selectedData.YARD_TIER);
                    if (!string.IsNullOrEmpty(selectedData.ISO_CODE)) DrawRow("ISO", selectedData.ISO_CODE);
                    if (!string.IsNullOrEmpty(selectedData.TARE)) DrawRow("TARE", selectedData.TARE + " kg");
                }
            }
        }

        // Tombol Toggle Yard Editor di kanan atas
        float globalScale = (mgr.globalButtonScale > 0.01f) ? mgr.globalButtonScale : 1f;
        float toggleScale = (mgr.toggleButtonScale > 0.01f) ? mgr.toggleButtonScale : 0.5f;
        float effectiveScale = globalScale * toggleScale;

        if (mgr.pngToggleYardEditor != null)
        {
            // Dimensi dihitung langsung dari pixel asli PNG dikalikan scale (tidak perlu tebak X/Y dan dijamin tidak gepeng)
            float btnWidth = mgr.pngToggleYardEditor.width * effectiveScale;
            float btnHeight = mgr.pngToggleYardEditor.height * effectiveScale;
            float btnX = mgr.isEditorActive ? (Screen.width - 295f - btnWidth - 20f) : (Screen.width - btnWidth - 20f);
            float btnY = 20f;

            Rect rect = new Rect(btnX, btnY, btnWidth, btnHeight);
            bool isActive = mgr.isEditorActive;
            bool isHovered = rect.Contains(Event.current.mousePosition);
            bool isPressed = isHovered && Input.GetMouseButton(0);

            // Ukuran: tidak kepilih/hover = ukuran normal (blm gede), pas dipilih = membesar
            Rect drawRect = rect;
            if (isPressed)
            {
                drawRect = new Rect(rect.x + 2f, rect.y + 1f, rect.width - 4f, rect.height - 2f);
            }
            else if (isActive)
            {
                // Pas dipilih membesar (persis tombol 2D View saat aktif)
                float grow = 5f * effectiveScale;
                drawRect = new Rect(rect.x - grow, rect.y - (grow * 0.5f), rect.width + (grow * 2f), rect.height + grow);
            }

            Color prevColor = GUI.color;

            // Efek Warna: gak kepilih gelap (dimmed), di hover nyala (100%), pas dipilih nyala (100%)
            if (isPressed)
            {
                GUI.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            }
            else if (isActive)
            {
                GUI.color = Color.white; // 100% Nyala terang & membesar
            }
            else if (isHovered)
            {
                GUI.color = Color.white; // 100% Nyala terang (ukuran belum membesar)
            }
            else
            {
                // Saat tidak dipilih: gelap/redup (persis seperti tombol 3D/Walk View yang tidak aktif)
                GUI.color = new Color(0.60f, 0.60f, 0.60f, 0.72f);
            }
            // Gambar PNG utama (100% proporsional, tidak pernah gepeng)
            GUI.DrawTexture(drawRect, mgr.pngToggleYardEditor, ScaleMode.ScaleToFit, true);

            // Tombol klik transparan di atasnya
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                mgr.ToggleEditor();
            }

            GUI.color = prevColor;
        }
        else
        {
            float btnWidth = 140f * effectiveScale;
            float btnHeight = 35f * effectiveScale;
            float btnX = mgr.isEditorActive ? (Screen.width - 295f - btnWidth - 30f) : (Screen.width - btnWidth - 20f);
            float btnY = 20f;

            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            GUI.Box(new Rect(btnX - 10, btnY - 5, btnWidth + 20, btnHeight + 10), "");

            GUI.color = mgr.isEditorActive ? new Color(0.3f, 0.85f, 1f, 1f) : Color.white;
            if (GUI.Button(new Rect(btnX, btnY, btnWidth, btnHeight), "🏗️ Yard Editor"))
            {
                mgr.ToggleEditor();
            }
            GUI.color = Color.white;
        }

        // Tampilkan notifikasi toast pop-up jika ada
        DrawToastNotification();
    }
}

/// <summary>
/// Komponen efek interaktif Hover, Press, dan Select (Active) untuk semua tombol di Sidebar Yard Editor.
/// </summary>
public class YardButtonHoverFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    private Button btn;
    private Outline outline;
    private bool isHovered = false;
    private bool isPressed = false;
    private bool isSelected = false;
    private bool hasCustomPng = false;
    private Vector3 targetScale = Vector3.one;

    void Awake()
    {
        btn = GetComponent<Button>();
        outline = GetComponent<Outline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
        }
        outline.effectDistance = new Vector2(2f, -2f);
        outline.enabled = false;
    }

    public void SetHasCustomPng(bool hasPng)
    {
        hasCustomPng = hasPng;
        RefreshVisualState();
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * 16f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        RefreshVisualState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
        RefreshVisualState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        RefreshVisualState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        RefreshVisualState();
    }

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        RefreshVisualState();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        RefreshVisualState();
    }

    private void RefreshVisualState()
    {
        if (btn != null && !btn.interactable)
        {
            targetScale = Vector3.one;
            if (outline != null) outline.enabled = false;
            return;
        }

        if (isPressed)
        {
            targetScale = new Vector3(0.95f, 0.95f, 1f);
        }
        else if (isHovered)
        {
            targetScale = new Vector3(1.05f, 1.05f, 1f);
        }
        else if (isSelected)
        {
            targetScale = new Vector3(1.02f, 1.02f, 1f);
        }
        else
        {
            targetScale = Vector3.one;
        }

        if (outline != null)
        {
            if (isHovered)
            {
                outline.enabled = true;
                outline.effectColor = hasCustomPng
                    ? new Color(1f, 0.95f, 0.70f, 0.75f)
                    : new Color(0.35f, 0.88f, 1f, 0.85f);
            }
            else if (isSelected)
            {
                outline.enabled = true;
                outline.effectColor = new Color(0.25f, 0.92f, 1f, 0.90f);
            }
            else
            {
                outline.enabled = false;
            }
        }
    }
}
