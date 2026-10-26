using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class YardBoundary : MonoBehaviour
{
    [Header("Pengaturan Garis Pinggir")]
    [Tooltip("Centang untuk menampilkan bingkai kuning di sekeliling yard.")]
    public bool tampilkanBingkaiKuning = false;

    [Tooltip("Centang sisi mana saja yang ingin ditampilkan")]
    public bool tampilkanSisiUtara = true;
    public bool tampilkanSisiSelatan = true;
    public bool tampilkanSisiTimur = true;
    public bool tampilkanSisiBarat = true;

    [Tooltip("Ketebalan garis pinggir kuning")]
    public float ketebalanGaris = 0.5f;

    [Tooltip("Offset dorong garis keluar (nilai positif) atau masuk (nilai negatif) dari Cube (meter)")]
    public float offsetGaris = 0.0f;
    
    [Tooltip("Material warna kuning untuk garis")]
    public Material materialKuning;

    [Tooltip("Jarak garis dari permukaan tanah")]
    public float jarakDariLantai = 0.05f;

    [Header("Pengaturan Fisik (Anti Tembus)")]
    [Tooltip("Tinggi tembok gaib agar player VR tidak bisa lewat")]
    public float tinggiTembokPenghalang = 3.0f;

    [Header("Pengaturan Label Blok")]
    [Tooltip("Centang untuk menampilkan label blok ('A', 'B', 'C') di dalam kotak di lantai.")]
    public bool tampilkanLabelBlok = true;

    [Tooltip("Tulis nama blok di sini (misal: A, B, atau C). Kosongkan untuk deteksi otomatis.")]
    public string customBlockName = "";

    [Tooltip("Ukuran kotak bingkai huruf (meter)")]
    public float ukuranKotakLabel = 3.2f;

    [Tooltip("Ukuran font huruf di dalam kotak")]
    public float ukuranFontLabel = 50.0f;

    private Vector3 lastPos;
    private Vector3 lastScale;

    // Alas yard sejajar (coplanar) dengan lantai vr-env -> Z-fighting (garis-garis biru) di WebGL.
    // Solusi: angkat alas yard sedikit saat Run, sebelum kontainer di-spawn (kontainer ikut menempel ke alas).
    private const float yardLift = 0.05f;
    private bool liftApplied = false;

    void Start()
    {
        if (Application.isPlaying && !liftApplied)
        {
            transform.position += Vector3.up * yardLift;
            liftApplied = true;
        }
        BuatGarisBatas();
    }

    void OnDestroy()
    {
        if (instanceYellowMat != null)
        {
            if (Application.isPlaying) Destroy(instanceYellowMat);
            else DestroyImmediate(instanceYellowMat);
        }
    }

    void Update()
    {
        // Di Edit Mode (sebelum di-Run), jika posisi atau skala Cube digeser user di Scene, langsung update garisnya di lantai!
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
#endif
            if (transform.position != lastPos || transform.lossyScale != lastScale)
            {
                lastPos = transform.position;
                lastScale = transform.lossyScale;
                BuatGarisBatas();
            }
        }
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
            UnityEditor.EditorApplication.delayCall -= DelayBuatGaris;
            UnityEditor.EditorApplication.delayCall += DelayBuatGaris;
#endif
        }
    }

#if UNITY_EDITOR
    void DelayBuatGaris()
    {
        if (this != null && gameObject != null)
        {
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
            BuatGarisBatas();
        }
    }
#endif

    [ContextMenu("Buat Garis Batas")]
    public void BuatGarisBatas()
    {
#if UNITY_EDITOR
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
#endif
        // 1. Bersihkan sisa script versi lama & semua bingkai buatan sebelumnya (termasuk objek yatim di scene)
        LineRenderer lr = GetComponent<LineRenderer>();
        if (lr != null) 
        {
            if (Application.isPlaying) Destroy(lr);
            else DestroyImmediate(lr);
        }

        List<GameObject> listHapus = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (child != null && (child.name == "BingkaiKuning" || child.name.StartsWith("Bingkai")))
            {
                listHapus.Add(child.gameObject);
            }
        }

        if (gameObject.scene.isLoaded)
        {
            GameObject[] rootObjects = gameObject.scene.GetRootGameObjects();
            foreach (GameObject rootObj in rootObjects)
            {
                if (rootObj != null && rootObj.name == "BingkaiKuning")
                {
                    listHapus.Add(rootObj);
                }
            }
        }

        foreach (GameObject go in listHapus)
        {
            if (go == null) continue;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        // 2. Cari semua renderer asli pembentuk lantai/cube (Abaikan bingkai, label, dan marking buatan)
        List<Renderer> lantaiRends = new List<Renderer>();
        Renderer selfRend = GetComponent<Renderer>();

        if (selfRend != null && transform.childCount == 0)
        {
            lantaiRends.Add(selfRend);
        }
        else
        {
            Renderer[] semuaRenderer = GetComponentsInChildren<Renderer>();
            foreach (Renderer r in semuaRenderer)
            {
                if (r == null) continue;
                string rName = r.gameObject.name;

                // Abaikan kontainer dan semua objek bingkai/label buatan
                if (rName.Contains("ft)")) continue; 
                if (rName.Contains("Bingkai") || rName.Contains("Sisi") || rName.Contains("Label") || 
                    rName.Contains("Top") || rName.Contains("Bottom") || rName.Contains("Left") || 
                    rName.Contains("Right") || rName.Contains("Text_")) continue;

                if (rName == "Cube" && r.gameObject != gameObject) continue;

                lantaiRends.Add(r);
            }
        }

        if (lantaiRends.Count == 0)
        {
            if (selfRend != null) lantaiRends.Add(selfRend);
            else return;
        }

        // 3. Gabungkan ukuran (Bounds)
        Bounds batas = lantaiRends[0].bounds;
        for (int i = 1; i < lantaiRends.Count; i++)
        {
            batas.Encapsulate(lantaiRends[i].bounds);
        }

        // 4. Buat objek induk pembungkus
        GameObject bingkai = new GameObject("BingkaiKuning");
        bingkai.transform.SetParent(this.transform);
        bingkai.transform.localPosition = Vector3.zero;

        float minX = batas.min.x - offsetGaris;
        float maxX = batas.max.x + offsetGaris;
        float minZ = batas.min.z - offsetGaris;
        float maxZ = batas.max.z + offsetGaris;

        float pX = maxX - minX;
        float pZ = maxZ - minZ;
        float cX = (minX + maxX) / 2f;
        float cZ = (minZ + maxZ) / 2f;
        
        float floorY = GetTargetFloorY(batas);
        float tinggiCurb = 0.02f; // Tipis menempel di lantai
        float posYVisual = floorY + (tinggiCurb / 2f) + 0.02f;
        float tebal = Mathf.Max(ketebalanGaris, 0.3f); 

        // 5. Buat Bingkai Luar (Jika Diaktifkan)
        if (tampilkanBingkaiKuning)
        {
            if (tampilkanSisiUtara) BuatSisi(bingkai, "SisiUtara", new Vector3(cX, posYVisual, maxZ), new Vector3(pX + tebal, tinggiCurb, tebal));
            if (tampilkanSisiSelatan) BuatSisi(bingkai, "SisiSelatan", new Vector3(cX, posYVisual, minZ), new Vector3(pX + tebal, tinggiCurb, tebal));
            if (tampilkanSisiTimur) BuatSisi(bingkai, "SisiTimur", new Vector3(maxX, posYVisual, cZ), new Vector3(tebal, tinggiCurb, pZ + tebal));
            if (tampilkanSisiBarat) BuatSisi(bingkai, "SisiBarat", new Vector3(minX, posYVisual, cZ), new Vector3(tebal, tinggiCurb, pZ + tebal));
        }

        // 6. Penentuan Nama Blok (Hanya jika customBlockName diisi atau bukan Cube biasa)
        string labelText = customBlockName;
        if (string.IsNullOrEmpty(labelText)) 
        {
            string n = gameObject.name.ToLower();
            if (n.Contains("gedung") || n == "cube") return; // Jika Cube biasa / Gedung dan customBlockName kosong, JANGAN buat label blok!
            n = n.Replace("yard", "").Replace("bawahnya", "").Replace("blok", "").Replace("gedung", "").Replace("cube", "").Replace(" ", "").Trim();
            labelText = n.ToUpper();
        }

        if (string.IsNullOrEmpty(labelText)) return; 

        // 7. Buat Label Blok Dibalut Kotak di Atas Beton (Utara & Selatan)
        if (tampilkanLabelBlok)
        {
            BuatLabelDalamKotak(bingkai.transform, "LabelUtara", new Vector3(cX, floorY + 0.02f, maxZ - 2.5f), Quaternion.identity, labelText);
            BuatLabelDalamKotak(bingkai.transform, "LabelSelatan", new Vector3(cX, floorY + 0.02f, minZ + 2.5f), Quaternion.identity, labelText);
        }

        Debug.Log("Garis batas & Label Blok berhasil diperbarui untuk: " + gameObject.name);
    }

    float GetTargetFloorY(Bounds bounds)
    {
        float envTopY = 19.0f;
        GameObject vrEnv = GameObject.Find("vr-env");
        if (vrEnv != null)
        {
            Renderer rend = vrEnv.GetComponentInChildren<Renderer>();
            envTopY = (rend != null) ? rend.bounds.max.y : vrEnv.transform.position.y;
        }

        // Jika Cube berada di bawah atau sejajar lantai vr-env, kunci ketinggian garis di permukaan vr-env!
        if (bounds.max.y <= envTopY + 0.2f)
        {
            return envTopY + (liftApplied ? yardLift : 0f);
        }

        return bounds.max.y;
    }

    private Material instanceYellowMat;

    private Material GetYellowMaterial()
    {
        if (materialKuning != null) return materialKuning;
        
        if (instanceYellowMat == null)
        {
            // Gunakan Unlit shader agar warna kuning selalu terang, tajam, dan tidak kehijauan akibat lighting scene
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Standard");
            
            instanceYellowMat = new Material(s);
            Color brightYellow = new Color(1.0f, 0.88f, 0.0f, 1.0f); // Bright Industrial Yellow
            instanceYellowMat.color = brightYellow;
            if (instanceYellowMat.HasProperty("_BaseColor")) instanceYellowMat.SetColor("_BaseColor", brightYellow);
            if (instanceYellowMat.HasProperty("_Color")) instanceYellowMat.SetColor("_Color", brightYellow);
            instanceYellowMat.name = "YellowMat_" + gameObject.name;
        }
        return instanceYellowMat;
    }

    void BuatLabelDalamKotak(Transform parent, string namaObjek, Vector3 centerPos, Quaternion rotation, string letter)
    {
        GameObject containerObj = new GameObject(namaObjek);
        containerObj.transform.SetParent(parent);
        containerObj.transform.position = centerPos;
        containerObj.transform.rotation = rotation;

        // 1. Buat 4 sisi kotak garis pembungkus di lantai
        float boxSize = Mathf.Max(ukuranKotakLabel, 1.0f);
        float lineThick = 0.2f;
        float half = boxSize / 2f;

        Material targetMat = GetYellowMaterial();
        Color boxColor = targetMat.HasProperty("_BaseColor") ? targetMat.GetColor("_BaseColor") : (targetMat.HasProperty("_Color") ? targetMat.color : Color.yellow);

        // Sisi Atas, Bawah, Kiri, Kanan (Top & Bottom dipanjangkan + lineThick agar sudut siku rapi tanpa takik/lekukan)
        CreateBoxSide(containerObj.transform, "Top", new Vector3(0, 0.01f, half), new Vector3(boxSize + lineThick, 0.02f, lineThick), boxColor);
        CreateBoxSide(containerObj.transform, "Bottom", new Vector3(0, 0.01f, -half), new Vector3(boxSize + lineThick, 0.02f, lineThick), boxColor);
        CreateBoxSide(containerObj.transform, "Left", new Vector3(-half, 0.01f, 0), new Vector3(lineThick, 0.02f, boxSize), boxColor);
        CreateBoxSide(containerObj.transform, "Right", new Vector3(half, 0.01f, 0), new Vector3(lineThick, 0.02f, boxSize), boxColor);

        // 2. Buat Teks Huruf ("A", "B", "C") di tengah-tengah kotak dengan proporsi rapi
        GameObject textObj = new GameObject("Text_" + letter);
        textObj.transform.SetParent(containerObj.transform);
        textObj.transform.localPosition = new Vector3(0, 0.02f, 0);
        textObj.transform.localRotation = Quaternion.Euler(90, 0, 0);

        // Kompensasi skala parent agar teks tidak mengecil jika Cube parent di-scale
        Vector3 pScale = containerObj.transform.lossyScale;
        textObj.transform.localScale = new Vector3(
            pScale.x != 0 ? 1.0f / pScale.x : 1.0f,
            pScale.y != 0 ? 1.0f / pScale.y : 1.0f,
            pScale.z != 0 ? 1.0f / pScale.z : 1.0f
        );

        TMPro.TextMeshPro tmp = textObj.AddComponent<TMPro.TextMeshPro>();
        tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(15f, 15f);
        tmp.text = letter;
        tmp.fontSize = Mathf.Max(ukuranFontLabel, 1.0f);
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = boxColor;
        if (Application.isPlaying)
        {
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = Color.black;
        }
    }

    void CreateBoxSide(Transform parent, string sideName, Vector3 localPos, Vector3 localScale, Color color)
    {
        GameObject side = GameObject.CreatePrimitive(PrimitiveType.Cube);
        side.name = sideName;
        side.transform.SetParent(parent);
        side.transform.localPosition = localPos;

        // Kompensasi skala induk agar tidak terdistorsi pada Cube yang di-scale di Inspector
        Vector3 pScale = parent.lossyScale;
        side.transform.localScale = new Vector3(
            pScale.x != 0 ? localScale.x / pScale.x : localScale.x,
            pScale.y != 0 ? localScale.y / pScale.y : localScale.y,
            pScale.z != 0 ? localScale.z / pScale.z : localScale.z
        );
        
        Collider col = side.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);

        Renderer r = side.GetComponent<Renderer>();
        if (r != null)
        {
            r.sharedMaterial = GetYellowMaterial();
        }
    }

    void BuatSisi(GameObject parent, string namaSisi, Vector3 posisi, Vector3 skala)
    {
        GameObject sisi = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sisi.name = namaSisi;
        sisi.transform.SetParent(parent.transform);
        
        sisi.transform.position = posisi;

        // Kompensasi skala induk agar garis tidak menggelembung/hilang karena skala parent Cube
        Vector3 pScale = parent.transform.lossyScale;
        sisi.transform.localScale = new Vector3(
            pScale.x != 0 ? skala.x / pScale.x : skala.x,
            pScale.y != 0 ? skala.y / pScale.y : skala.y,
            pScale.z != 0 ? skala.z / pScale.z : skala.z
        );

        Renderer r = sisi.GetComponent<Renderer>();
        if (r != null)
        {
            r.sharedMaterial = GetYellowMaterial();
        }

        BoxCollider col = sisi.GetComponent<BoxCollider>();
        if (col != null)
        {
            Vector3 colSize = col.size;
            colSize.y = tinggiTembokPenghalang / sisi.transform.localScale.y; 
            Vector3 colCenter = col.center;
            colCenter.y = (colSize.y / 2f) - 0.5f; 

            col.size = colSize;
            col.center = colCenter;
        }
    }
}
