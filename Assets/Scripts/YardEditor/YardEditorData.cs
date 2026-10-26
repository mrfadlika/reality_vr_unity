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
public class YardLayout
{
    public List<YardPlacement> yards = new List<YardPlacement>();
    public List<RoadPlacement> roads = new List<RoadPlacement>();
}

// --- Static Utility ---

public static class YardEditorDataUtil
{
    private const string SAVE_KEY = "YardEditorLayout";

    public static void SaveLayout(YardLayout layout)
    {
        string json = JsonUtility.ToJson(layout, true);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
        Debug.Log("[YardEditor] Layout tersimpan! (" + layout.yards.Count + " yards, " + layout.roads.Count + " roads)");
    }

    public static YardLayout LoadLayout()
    {
        if (!PlayerPrefs.HasKey(SAVE_KEY))
        {
            Debug.Log("[YardEditor] Tidak ada layout tersimpan, memuat layout kosong.");
            return new YardLayout();
        }

        string json = PlayerPrefs.GetString(SAVE_KEY);
        YardLayout layout = JsonUtility.FromJson<YardLayout>(json);
        
        if (layout == null) layout = new YardLayout();
        if (layout.yards == null) layout.yards = new List<YardPlacement>();
        if (layout.roads == null) layout.roads = new List<RoadPlacement>();
        
        Debug.Log("[YardEditor] Layout dimuat! (" + layout.yards.Count + " yards, " + layout.roads.Count + " roads)");
        return layout;
    }

    public static bool HasSavedLayout()
    {
        return PlayerPrefs.HasKey(SAVE_KEY);
    }

    public static void ClearLayout()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.Save();
        Debug.Log("[YardEditor] Layout dihapus dari memory.");
    }
}
