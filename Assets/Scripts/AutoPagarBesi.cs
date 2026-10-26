using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class AutoPagarBesi : MonoBehaviour
{
    [Header("Pengaturan Prefab")]
    [Tooltip("Masukkan prefab pagar besi ke sini")]
    public GameObject prefabPagar;

    [Tooltip("Panjang satu buah pagar (meter). Sesuaikan agar susunan pagarnya rapat dan tidak menumpuk.")]
    public float panjangPagar = 2.0f;

    [Tooltip("Jika rotasi pagar terbalik atau menghadap arah yang salah, ubah nilai ini (misal: 0, 90, 180, -90)")]
    public float penyesuaianRotasiY = 0f;

    [Header("Pengaturan Sisi")]
    public bool tampilkanSisiUtara = true;
    public bool tampilkanSisiSelatan = true;
    public bool tampilkanSisiTimur = true;
    public bool tampilkanSisiBarat = true;

    [Header("Pengaturan Posisi & Fisik")]
    [Tooltip("Offset dorong pagar keluar (positif) atau masuk (negatif) dari tepi Cube")]
    public float offsetPagar = 0.0f;

    [Tooltip("Opsional: Geser pagar ekstra ke atas/bawah. Script ini sudah OTOMATIS menempelkan pagar ke lantai.")]
    public float jarakDariLantai = 0.0f;

    [Tooltip("Tinggi tembok gaib agar player VR tidak bisa nembus pagar")]
    public float tinggiTembokPenghalang = 3.0f;

    private Vector3 lastPos;
    private Vector3 lastScale;

    void Start()
    {
        GeneratePagar();
    }

    void Update()
    {
        // Update otomatis saat digeser atau di-scale di Edit Mode
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
#endif
            if (transform.position != lastPos || transform.lossyScale != lastScale)
            {
                lastPos = transform.position;
                lastScale = transform.lossyScale;
                GeneratePagar();
            }
        }
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
            UnityEditor.EditorApplication.delayCall -= DelayGenerate;
            UnityEditor.EditorApplication.delayCall += DelayGenerate;
#endif
        }
    }

#if UNITY_EDITOR
    void DelayGenerate()
    {
        if (this != null && gameObject != null)
        {
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
            GeneratePagar();
        }
    }
#endif

    [ContextMenu("Generate Pagar Besi")]
    public void GeneratePagar()
    {
#if UNITY_EDITOR
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
#endif
        // 1. Bersihkan pagar lama
        List<GameObject> listHapus = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (child != null && child.name == "ContainerPagarBesi")
            {
                listHapus.Add(child.gameObject);
            }
        }
        foreach (GameObject go in listHapus)
        {
            if (go == null) continue;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        if (prefabPagar == null) return; // Hentikan jika belum ada prefab

        // 2. Cari semua renderer asli pembentuk lantai/cube (Sistem sama seperti YardBoundary)
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

                // Abaikan kontainer dan objek buatan script lain
                if (rName.Contains("ft)")) continue; 
                if (rName.Contains("Bingkai") || rName.Contains("Sisi") || rName.Contains("Label") || 
                    rName.Contains("Top") || rName.Contains("Bottom") || rName.Contains("Left") || 
                    rName.Contains("Right") || rName.Contains("Text_") || rName.Contains("ContainerPagar")) continue;

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

        // 4. Buat Container penampung
        GameObject container = new GameObject("ContainerPagarBesi");
        container.transform.SetParent(this.transform);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;

        float minX = batas.min.x - offsetPagar;
        float maxX = batas.max.x + offsetPagar;
        float minZ = batas.min.z - offsetPagar;
        float maxZ = batas.max.z + offsetPagar;

        float floorY = GetTargetFloorY(batas) + jarakDariLantai;

        // 5. Generate Pagar di Sisi yang Diaktifkan
        // Menggunakan alur jarum jam/berlawanan jarum jam agar orientasi konsisten
        if (tampilkanSisiUtara) SusunPagar(container.transform, "PenghalangUtara", new Vector3(minX, floorY, maxZ), new Vector3(maxX, floorY, maxZ));
        if (tampilkanSisiTimur) SusunPagar(container.transform, "PenghalangTimur", new Vector3(maxX, floorY, maxZ), new Vector3(maxX, floorY, minZ));
        if (tampilkanSisiSelatan) SusunPagar(container.transform, "PenghalangSelatan", new Vector3(maxX, floorY, minZ), new Vector3(minX, floorY, minZ));
        if (tampilkanSisiBarat) SusunPagar(container.transform, "PenghalangBarat", new Vector3(minX, floorY, minZ), new Vector3(minX, floorY, maxZ));

        Debug.Log("Barisan Pagar Besi berhasil diperbarui untuk: " + gameObject.name);
    }

    void SusunPagar(Transform parent, string namaPenghalang, Vector3 titikMulai, Vector3 titikAkhir)
    {
        float jarak = Vector3.Distance(titikMulai, titikAkhir);
        if (jarak <= 0.01f || prefabPagar == null) return;

        // Trik Jitu: Cari offset asli prefab & ukur panjang aslinya secara otomatis
        float purePivotOffset = 0f;
        float otomatisPanjangPagar = panjangPagar; // Default ke inspector

        // Gunakan Quaternion.identity agar rotasi sejajar dengan kalkulasi bounds standar.
        GameObject dummy = Instantiate(prefabPagar, Vector3.zero, Quaternion.identity);
        
        // Gunakan 'true' agar LOD/Mesh yang non-aktif secara default tetap terdeteksi bounds-nya
        Renderer[] rends = dummy.GetComponentsInChildren<Renderer>(true);
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int r = 1; r < rends.Length; r++) b.Encapsulate(rends[r].bounds);
            
            // Dapatkan offset lokal murni (bebas dari skala prefab)
            float dummyScaleY = dummy.transform.lossyScale.y;
            purePivotOffset = b.min.y / (dummyScaleY == 0f ? 1f : dummyScaleY);

            // Deteksi otomatis panjang pagar yang paling panjang (X atau Z) dari bounding box
            float maxBoundsSize = Mathf.Max(b.size.x, b.size.z);
            if (maxBoundsSize > 0.1f) 
            {
                otomatisPanjangPagar = maxBoundsSize;
            }
        }
        if (Application.isPlaying) Destroy(dummy);
        else DestroyImmediate(dummy);

        if (otomatisPanjangPagar <= 0.1f) return;

        // --- BUAT TEMBOK GAIB (COLLIDER) UNTUK SISI INI ---
        GameObject tembokGaib = new GameObject(namaPenghalang);
        tembokGaib.transform.SetParent(parent);
        Vector3 centerPos = (titikMulai + titikAkhir) / 2f;
        tembokGaib.transform.position = centerPos;
        
        Vector3 arahGaris = (titikAkhir - titikMulai).normalized;
        tembokGaib.transform.rotation = Quaternion.LookRotation(arahGaris);
        
        BoxCollider col = tembokGaib.AddComponent<BoxCollider>();
        col.size = new Vector3(0.2f, tinggiTembokPenghalang, jarak);
        col.center = new Vector3(0, tinggiTembokPenghalang / 2f, 0);

        int jumlahPagar = Mathf.RoundToInt(jarak / otomatisPanjangPagar);
        if (jumlahPagar == 0) jumlahPagar = 1;

        float panjangAktual = jarak / jumlahPagar;
        float skalaStretch = panjangAktual / otomatisPanjangPagar;
        Vector3 posisiAwal = titikMulai + (arahGaris * (panjangAktual * 0.5f));

        for (int i = 0; i < jumlahPagar; i++)
        {
            Vector3 posisiSpawn = posisiAwal + (arahGaris * (panjangAktual * i));

#if UNITY_EDITOR
            GameObject pagar = null;
            if (Application.isPlaying) {
                pagar = Instantiate(prefabPagar);
            } else {
                pagar = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefabPagar);
            }
#else
            GameObject pagar = Instantiate(prefabPagar);
#endif
            
            if (pagar != null)
            {
                pagar.name = "Pagar_" + i;
                pagar.transform.SetParent(parent);
                
                Vector3 hadapPagar = Vector3.Cross(arahGaris, Vector3.up);
                Quaternion rotasiArahGaris = Quaternion.LookRotation(hadapPagar);
                
                // Rotasi murni dari arah garis ditambah penyesuaian Y dari user
                pagar.transform.rotation = rotasiArahGaris * Quaternion.Euler(0, penyesuaianRotasiY, 0);

                Vector3 parentScale = parent.lossyScale;
                Vector3 skalaAsli = prefabPagar.transform.localScale;
                
                // Kalikan skalaStretch ke X dan Z agar pagar merenggang menutupi celah secara sempurna,
                // tanpa peduli apakah arah panjang pagar aslinya ada di sumbu X atau Z.
                Vector3 finalScale = new Vector3(
                    (skalaAsli.x * skalaStretch) / (parentScale.x == 0 ? 1 : parentScale.x),
                    skalaAsli.y / (parentScale.y == 0 ? 1 : parentScale.y),
                    (skalaAsli.z * skalaStretch) / (parentScale.z == 0 ? 1 : parentScale.z)
                );
                
                pagar.transform.localScale = finalScale;

                // --- RAYCAST LANTAI ---
                // Karena lantai vr-env mungkin miring atau tidak rata di sepanjang garis yang jauh,
                // kita harus menembakkan laser (raycast) ke bawah untuk mencari tinggi lantai (Y) 
                // secara akurat tepat di posisi X,Z pagar ini.
                float lantaiY = titikMulai.y; // fallback awal
                RaycastHit[] hits = Physics.RaycastAll(new Vector3(posisiSpawn.x, 1000f, posisiSpawn.z), Vector3.down, 2000f);
                float highestHit = -9999f;
                bool hitFound = false;

                foreach (var hit in hits)
                {
                    // Cari collider milik vr-env
                    if (hit.collider.gameObject.name.ToLower().Contains("vr-env"))
                    {
                        if (hit.point.y > highestHit)
                        {
                            highestHit = hit.point.y;
                            hitFound = true;
                        }
                    }
                }

                if (hitFound) lantaiY = highestHit;

                // Hitung seberapa jauh kita harus menaikkan/menurunkan pagar ini
                float finalOffset = purePivotOffset * pagar.transform.lossyScale.y;
                
                // Set posisi pagar menggunakan tinggi lantai yang akurat untuk titik ini
                pagar.transform.position = new Vector3(posisiSpawn.x, lantaiY, posisiSpawn.z) - new Vector3(0, finalOffset, 0) + new Vector3(0, jarakDariLantai, 0);
            }
        }
    }

    float GetTargetFloorY(Bounds batasCube)
    {
        GameObject vrEnv = null;

        // 1. Cari vr-env (Bahkan jika objek tersebut sedang Non-Aktif di Play Mode)
        if (gameObject.scene.isLoaded)
        {
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            foreach (GameObject r in roots)
            {
                if (r.name.ToLower() == "vr-env") 
                {
                    vrEnv = r;
                    break;
                }
            }
        }

        if (vrEnv == null) vrEnv = GameObject.Find("vr-env");
        if (vrEnv == null) vrEnv = GameObject.Find("VR-Env");
        
        if (vrEnv != null)
        {
            // Jangan hanya ambil posisinya, tapi ambil titik tertinggi atapnya (karena lantai punya ketebalan)
            Renderer rend = vrEnv.GetComponentInChildren<Renderer>(true);
            float envTopY = (rend != null) ? rend.bounds.max.y : vrEnv.transform.position.y;
            return envTopY;
        }

        // 2. Jika benar-benar tidak ada vr-env, gunakan atap Cube
        Debug.LogWarning("[AutoPagarBesi] Objek 'vr-env' tidak ditemukan sama sekali! Mengunci pagar ke atap Cube.");
        return batasCube.max.y;
    }
}
