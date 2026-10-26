#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System.Collections.Generic;

public static class ContainerDoorDebugger
{
    [MenuItem("Tools/Container/Refresh All Container Markings in Scene")]
    [DidReloadScripts]
    public static void RefreshAllContainerMarkings()
    {
        ContainerManager cm = Object.FindAnyObjectByType<ContainerManager>();
        if (cm == null) return;

        // Kumpulkan list container dulu agar aman dari modifikasi objek saat loop
        List<GameObject> containers = new List<GameObject>();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go == null) continue;
            if (go.name.Contains("ft)") && !go.name.Contains("Prefab") && !go.name.Contains("Clone") && go.activeInHierarchy)
            {
                containers.Add(go);
            }
        }

        int refreshedCount = 0;
        foreach (var go in containers)
        {
            if (go == null) continue;

            Bounds localBounds = GetLocalBounds(go);
            if (localBounds.size.magnitude < 0.1f) continue;

            string iso = "";
            string tare = "";
            if (cm.TryGetContainerData(go, out ContainerData data))
            {
                iso = data.ISO_CODE;
                tare = data.TARE;
            }
            else
            {
                iso = go.name.Contains("40ft") ? "40HC" : "20GP";
                tare = go.name.Contains("40ft") ? "3810" : "2108";
            }

            cm.AddContainerMarkings(go, localBounds, iso, tare);
            refreshedCount++;
        }

        if (refreshedCount > 0)
        {
            Debug.Log($"[ContainerDoorDebugger] Sukses memperbarui tulisan pintu pada {refreshedCount} kontainer di Scene sesuai layout Gambar 2!");
        }
    }

    public static Bounds GetLocalBounds(GameObject go)
    {
        Quaternion origRot = go.transform.rotation;
        Vector3 origPos = go.transform.position;
        go.transform.rotation = Quaternion.identity;
        go.transform.position = Vector3.zero;

        Renderer[] rends = go.GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;
        foreach (var r in rends)
        {
            if (r == null || r.gameObject == null) continue;
            if (r.gameObject.name.Contains("ContainerMarkings") || r.gameObject.name.Contains("LeftCol") || r.gameObject.name.Contains("RightCol") || r.gameObject.name.Contains("CheckDigit") || r.gameObject.name.Contains("Border_"))
                continue;
            if (first) { b = r.bounds; first = false; }
            else { b.Encapsulate(r.bounds); }
        }

        go.transform.position = origPos;
        go.transform.rotation = origRot;
        return b;
    }
}
#endif
