using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// ============================================================
// YardEditorUI.cs
// Canvas-based UI: sidebar panel, yard/road buttons, controls
//
// FULLY DYNAMIC: Tombol yard dibuat dari API response,
// berapapun jumlahnya, nama apapun.
// ============================================================

public class YardEditorUI : MonoBehaviour
{
    // Canvas references (auto-created)
    private Canvas canvas;
    private GameObject sidebarPanel;
    private GameObject statusBar;

    // Dynamic yard buttons — key = block name
    private Dictionary<string, Button> yardButtons = new Dictionary<string, Button>();
    private Transform yardButtonContainer; // Parent untuk yard buttons agar bisa di-rebuild

    // Fixed buttons
    private Button addRoadBtn;
    private Button saveBtn;
    private Button loadBtn;
    private Button clearBtn;
    private Button deleteBtn;
    private Button rotateBtn;

    // Status text
    private TMPro.TextMeshProUGUI statusText;

    // Loading indicator
    private GameObject loadingLabel;

    // Styling constants
    private Color panelBgColor = new Color(0.08f, 0.08f, 0.12f, 0.92f);
    private Color buttonNormal = new Color(0.15f, 0.15f, 0.22f, 1f);
    private Color buttonRoad = new Color(0.45f, 0.35f, 0.12f, 1f);
    private Color buttonDanger = new Color(0.6f, 0.15f, 0.15f, 1f);
    private Color buttonSave = new Color(0.12f, 0.5f, 0.25f, 1f);
    private Color buttonRotate = new Color(0.5f, 0.3f, 0.6f, 1f);
    private Color accentColor = new Color(0.3f, 0.7f, 1f, 1f);
    private Color textColor = new Color(0.92f, 0.92f, 0.95f, 1f);
    private Color textMuted = new Color(0.6f, 0.6f, 0.65f, 1f);
    private Color spawnedColor = new Color(0.3f, 0.3f, 0.35f, 0.6f);

    private bool uiBuilt = false;

    void Start()
    {
        BuildUI();
    }

    void Update()
    {
        if (!uiBuilt) return;

        YardEditorManager mgr = YardEditorManager.Instance;
        if (mgr == null) return;

        // Toggle visibility
        if (sidebarPanel != null)
            sidebarPanel.SetActive(mgr.isEditorActive);
        if (statusBar != null)
            statusBar.SetActive(mgr.isEditorActive);

        // Update button states
        if (mgr.isEditorActive)
        {
            UpdateButtonStates();
            UpdateStatusText();
            UpdateLoadingState();
        }
    }

    // --- Build Entire UI ---

    void BuildUI()
    {
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
        panelRect.offsetMin = new Vector2(-280, 60);  // Left, Bottom
        panelRect.offsetMax = new Vector2(-10, -60);   // Right, Top

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
        viewportImg.color = new Color(0, 0, 0, 0.01f); // Hampir transparan, tapi perlu Image untuk Mask
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        scrollRect.viewport = viewportRect;

        // Content (scroll area)
        GameObject scrollContent = new GameObject("ScrollContent");
        scrollContent.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = scrollContent.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.offsetMin = new Vector2(12, 0);
        contentRect.offsetMax = new Vector2(-12, 0);

        scrollRect.content = contentRect;

        // Vertical layout
        VerticalLayoutGroup vlg = scrollContent.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.padding = new RectOffset(0, 0, 12, 12);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = scrollContent.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Transform parent = scrollContent.transform;

        // --- Header ---
        CreateLabel(parent, "HeaderLabel", "🏗️  YARD EDITOR", 22, accentColor, TextAnchor.MiddleCenter, 40);

        // Separator
        CreateSeparator(parent);

        // --- Section: Yards (DYNAMIC) ---
        CreateLabel(parent, "YardSection", "📦  YARD BLOCKS", 14, textMuted, TextAnchor.MiddleLeft, 28);

        // Container untuk yard buttons — akan di-rebuild saat API selesai
        GameObject yardBtnContainer = new GameObject("YardButtonContainer");
        yardBtnContainer.transform.SetParent(parent, false);
        RectTransform yardBtnRect = yardBtnContainer.AddComponent<RectTransform>();

        VerticalLayoutGroup yardVlg = yardBtnContainer.AddComponent<VerticalLayoutGroup>();
        yardVlg.spacing = 6;
        yardVlg.childControlWidth = true;
        yardVlg.childControlHeight = false;
        yardVlg.childForceExpandWidth = true;
        yardVlg.childForceExpandHeight = false;

        ContentSizeFitter yardCsf = yardBtnContainer.AddComponent<ContentSizeFitter>();
        yardCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        yardButtonContainer = yardBtnContainer.transform;

        // Loading indicator
        loadingLabel = CreateLabelReturnable(yardButtonContainer, "LoadingLabel", "⏳ Loading yards from API...", 12, textMuted, TextAnchor.MiddleCenter, 30);

        // Separator
        CreateSeparator(parent);

        // --- Section: Roads ---
        CreateLabel(parent, "RoadSection", "🛣️  ROADS", 14, textMuted, TextAnchor.MiddleLeft, 28);

        addRoadBtn = CreateButton(parent, "AddRoadBtn", "+ Add Road", buttonRoad, 38, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.SpawnRoad();
        });

        // Road Size Adjustments
        GameObject roadWidthGroup = new GameObject("RoadWidthGroup");
        roadWidthGroup.transform.SetParent(parent, false);
        HorizontalLayoutGroup rwhlg = roadWidthGroup.AddComponent<HorizontalLayoutGroup>();
        rwhlg.spacing = 6;
        rwhlg.childControlWidth = true;
        rwhlg.childControlHeight = false;
        rwhlg.childForceExpandWidth = true;

        CreateButton(roadWidthGroup.transform, "RoadWMinus", "Width -2m", buttonNormal, 34, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.AdjustSelectedRoadWidth(-2f);
        });

        CreateButton(roadWidthGroup.transform, "RoadWPlus", "Width +2m", buttonNormal, 34, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.AdjustSelectedRoadWidth(2f);
        });

        GameObject roadLenGroup = new GameObject("RoadLenGroup");
        roadLenGroup.transform.SetParent(parent, false);
        HorizontalLayoutGroup rlhlg = roadLenGroup.AddComponent<HorizontalLayoutGroup>();
        rlhlg.spacing = 6;
        rlhlg.childControlWidth = true;
        rlhlg.childControlHeight = false;
        rlhlg.childForceExpandWidth = true;

        CreateButton(roadLenGroup.transform, "RoadLMinus", "Length -5m", buttonNormal, 34, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.AdjustSelectedRoadLength(-5f);
        });

        CreateButton(roadLenGroup.transform, "RoadLPlus", "Length +5m", buttonNormal, 34, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.AdjustSelectedRoadLength(5f);
        });

        // Separator
        CreateSeparator(parent);

        // --- Section: Tools ---
        CreateLabel(parent, "ToolsSection", "🔧  TOOLS", 14, textMuted, TextAnchor.MiddleLeft, 28);

        rotateBtn = CreateButton(parent, "RotateBtn", "↻ Rotate / Toggle (R)", buttonRotate, 38, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.ToggleSelectedOrientation();
        });

        deleteBtn = CreateButton(parent, "DeleteBtn", "🗑️ Delete Selected (Del)", buttonDanger, 38, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.DeleteSelected();
        });

        // Separator
        CreateSeparator(parent);

        // --- Section: File Operations ---
        CreateLabel(parent, "FileSection", "💾  FILE", 14, textMuted, TextAnchor.MiddleLeft, 28);

        saveBtn = CreateButton(parent, "SaveBtn", "💾 Save Layout", buttonSave, 38, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.SaveLayout();
        });

        loadBtn = CreateButton(parent, "LoadBtn", "📂 Load Layout", buttonNormal, 38, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.LoadLayout();
        });

        clearBtn = CreateButton(parent, "ClearBtn", "🗑️ Clear All", buttonDanger, 38, () =>
        {
            if (YardEditorManager.Instance != null) YardEditorManager.Instance.ClearAll();
        });

        // Separator
        CreateSeparator(parent);

        // --- Help ---
        CreateLabel(parent, "HelpLabel",
            "🎮 Camera: WASD fly, Right-Drag look, Scroll zoom, Q/E height\n" +
            "🖱️ Object: Click to select, Left-Drag to move\n" +
            "🔄 R / Rotate: Rotate selected yard/road\n" +
            "📏 Width/Length: Adjust road size dynamically\n" +
            "🗑️ Del: Delete selected object",
            11, textMuted, TextAnchor.MiddleLeft, 100);

        sidebarPanel.SetActive(false);
    }

    /// <summary>
    /// Dipanggil oleh YardEditorManager setelah API selesai fetch.
    /// Menghapus loading indicator & membuat tombol untuk setiap yard dari API.
    /// </summary>
    public void RebuildYardButtons()
    {
        if (yardButtonContainer == null) return;

        // Hapus tombol lama & loading indicator
        foreach (var kvp in yardButtons)
        {
            if (kvp.Value != null) Destroy(kvp.Value.gameObject);
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
            // Tidak ada data dari API
            CreateLabel(yardButtonContainer, "NoDataLabel", "⚠️ No yards from API", 12, 
                new Color(1f, 0.6f, 0.2f, 1f), TextAnchor.MiddleCenter, 30);
            return;
        }

        // Buat tombol untuk SETIAP yard dari API — berapa pun jumlahnya
        for (int i = 0; i < blocks.Count; i++)
        {
            MasterBlockData block = blocks[i];
            string blockName = block.YARD_BLOCK;
            string displayName = block.YARD_BLOCK_NAME;
            if (string.IsNullOrEmpty(displayName)) displayName = blockName;

            // Info dimensi
            int maxRow = 6, maxSlot = 45, maxTier = 7;
            int.TryParse(block.YARD_ROW, out maxRow);
            int.TryParse(block.YARD_SLOT, out maxSlot);
            int.TryParse(block.YARD_TIER, out maxTier);

            string btnText = "📦 Yard " + displayName + "  (R:" + maxRow + " S:" + maxSlot + " T:" + maxTier + ")";

            // Generate warna unik per block
            Color btnColor = GenerateBlockButtonColor(blockName, i);

            // Capture untuk closure
            string capturedName = blockName;

            Button btn = CreateButton(yardButtonContainer, "YardBtn_" + blockName, btnText, btnColor, 38, () =>
            {
                if (YardEditorManager.Instance != null)
                {
                    YardEditorManager.Instance.SpawnYard(capturedName);
                }
            });

            yardButtons[blockName] = btn;
        }

        Debug.Log("[YardEditorUI] " + blocks.Count + " yard buttons created from API");
    }

    void BuildStatusBar(Transform canvasTransform)
    {
        statusBar = CreatePanel(canvasTransform, "StatusBar", new Color(0.05f, 0.05f, 0.08f, 0.85f));
        RectTransform statusRect = statusBar.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 0);
        statusRect.anchorMax = new Vector2(1, 0);
        statusRect.pivot = new Vector2(0.5f, 0);
        statusRect.offsetMin = new Vector2(10, 10);
        statusRect.offsetMax = new Vector2(-300, 42);

        // Status text
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
        // Jika API sudah loaded tapi tombol belum dibuat, rebuild
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

        // Update yard buttons — grey out if already spawned
        foreach (var kvp in yardButtons)
        {
            string blockName = kvp.Key;
            Button btn = kvp.Value;
            if (btn == null) continue;

            bool spawned = mgr.IsYardSpawned(blockName);
            btn.interactable = !spawned;

            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = spawned ? spawnedColor : GetCachedBlockColor(blockName);
            }
        }

        // Rotate/Delete buttons — active only when something is selected
        bool hasSelection = (mgr.GetSelectedYard() != null || mgr.GetSelectedRoad() != null);
        if (rotateBtn != null) rotateBtn.interactable = hasSelection;
        if (deleteBtn != null) deleteBtn.interactable = hasSelection;
    }

    // Color cache
    private Dictionary<string, Color> blockColorCache = new Dictionary<string, Color>();

    Color GetCachedBlockColor(string blockName)
    {
        if (blockColorCache.ContainsKey(blockName)) return blockColorCache[blockName];
        return buttonNormal;
    }

    /// <summary>
    /// Generate warna unik untuk tombol berdasarkan nama block + index.
    /// Menggunakan HSV dengan hue yang di-distribute merata.
    /// </summary>
    Color GenerateBlockButtonColor(string blockName, int index)
    {
        // Distribute hue evenly across all blocks
        YardEditorManager mgr = YardEditorManager.Instance;
        int totalBlocks = mgr != null ? mgr.GetMasterBlocks().Count : 4;
        
        float hue = (float)index / totalBlocks;
        // Offset agar dimulai dari biru, bukan merah
        hue = (hue + 0.55f) % 1f;

        float saturation = 0.55f;
        float value = 0.50f;

        Color color = Color.HSVToRGB(hue, saturation, value);
        blockColorCache[blockName] = color;
        return color;
    }

    void UpdateStatusText()
    {
        if (statusText == null) return;
        YardEditorManager mgr = YardEditorManager.Instance;
        if (mgr == null) return;

        string status = "🏗️ Yard Editor  |  ";
        
        if (!mgr.IsApiLoaded())
        {
            status += "Loading API...";
            statusText.text = status;
            return;
        }

        status += "Available: " + mgr.GetMasterBlocks().Count + " blocks  |  ";
        status += "Placed: " + mgr.GetActiveYards().Count + " yards, " + mgr.GetActiveRoads().Count + " roads";

        DraggableYard sy = mgr.GetSelectedYard();
        DraggableRoad sr = mgr.GetSelectedRoad();

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
            status += " (" + sr.directionAngle + "°)";
            if (sr.isDragging) status += " [DRAGGING]";
        }

        statusText.text = status;
    }

    // --- UI Factory Helpers ---

    GameObject CreatePanel(Transform parent, string name, Color bgColor)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);

        RectTransform rt = panel.AddComponent<RectTransform>();
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

        // Background
        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        // Button component
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        // Color transitions
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        btn.colors = colors;

        btn.onClick.AddListener(onClick);

        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10, 2);
        textRt.offsetMax = new Vector2(-10, -2);

        TMPro.TextMeshProUGUI tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 14;
        tmp.color = textColor;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;

        return btn;
    }

    void CreateSeparator(Transform parent)
    {
        GameObject sep = new GameObject("Separator");
        sep.transform.SetParent(parent, false);

        RectTransform rt = sep.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 1);

        LayoutElement le = sep.AddComponent<LayoutElement>();
        le.preferredHeight = 1;

        Image img = sep.AddComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.35f, 0.5f);
    }

    // Direct OnGUI Launcher Button (bebas dari ViewModeManager & aman untuk scene Copy)
    void OnGUI()
    {
        YardEditorManager mgr = YardEditorManager.Instance;
        if (mgr == null) return;

        float btnWidth = 140f;
        float btnHeight = 35f;
        
        // Pindahkan ke sebelah kiri sidebar jika sidebar sedang terbuka agar tidak bertumpuk
        float btnX = mgr.isEditorActive ? (Screen.width - 280f - btnWidth - 30f) : (Screen.width - btnWidth - 20f);
        float btnY = 20f;

        // Background box
        GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        GUI.Box(new Rect(btnX - 10, btnY - 5, btnWidth + 20, btnHeight + 10), "");

        // Toggle Button
        GUI.color = mgr.isEditorActive ? new Color(0.3f, 0.85f, 1f, 1f) : Color.white;
        if (GUI.Button(new Rect(btnX, btnY, btnWidth, btnHeight), "🏗️ Yard Editor"))
        {
            mgr.ToggleEditor();
        }
        GUI.color = Color.white;
    }
}
