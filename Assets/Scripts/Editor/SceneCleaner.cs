using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public class SceneCleaner
{
    [MenuItem("Tools/Fix Lag - Bersihkan Kontainer Baked dari Scene")]
    [MenuItem("Tools/Container/Bersihkan Semua Kontainer dari Scene")]
    public static void CleanBakedContainers()
    {
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        List<GameObject> toDestroy = new List<GameObject>();

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;
            string n = t.name;
            if (n.Contains("ft)") || n.StartsWith("Container_") || 
                n.StartsWith("GridNumbering_") || n.Contains("YardNumbering") || 
                n.StartsWith("Numbering_") || n == "ContainerMarkings" ||
                n.StartsWith("ContainerMarkings") || n.StartsWith("Txt_"))
            {
                if (!toDestroy.Contains(t.gameObject))
                {
                    toDestroy.Add(t.gameObject);
                }
            }
        }

        int count = toDestroy.Count;
        foreach (GameObject go in toDestroy)
        {
            if (go != null)
            {
                Undo.DestroyObjectImmediate(go);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log($"[SceneCleaner] Berhasil menghapus {count} kontainer/penomoran yang menumpuk di file Scene! File scene kini bersih dan ringan.");
        EditorUtility.DisplayDialog("Scene Cleaned", 
            $"Berhasil membersihkan {count} kontainer dan teks yang menumpuk di Scene!\n\nHierarchy Unity Anda sekarang sudah bersih, ringan, dan tidak akan ngelag lagi.\nSaat di-Play atau di WebGL, kontainer akan dimuat otomatis secara dinamis dari API.", "OK");
    }
}
