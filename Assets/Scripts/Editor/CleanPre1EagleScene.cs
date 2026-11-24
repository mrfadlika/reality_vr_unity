#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

[InitializeOnLoad]
public static class CleanPre1EagleScene
{
    static CleanPre1EagleScene()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                CleanScene();
            }
        };

        EditorApplication.playModeStateChanged += (state) =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += () => CleanScene();
            }
        };
    }

    [MenuItem("Tools/Clean Pre1Eagle Scene (Containers & Old Yards)")]
    public static void CleanScene()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "pre1eagle")
        {
            return;
        }

        int deletedCount = 0;

        // 1. Matikan ContainerManager agar tidak pernah auto-spawn di Edit Mode maupun Play Mode
        ContainerManager[] cms = Object.FindObjectsByType<ContainerManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var cm in cms)
        {
            if (cm != null && cm.gameObject.scene == activeScene)
            {
                cm.StopAllCoroutines();
                cm.enabled = false;
                Debug.Log("[CleanPre1Eagle] ContainerManager dinonaktifkan di pre1eagle.");
            }
        }

        // 2. Kumpulkan semua objek kontainer statis dan penomoran lama
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<GameObject> toDestroy = new List<GameObject>();

        foreach (Transform t in allTransforms)
        {
            if (t == null || t.gameObject.scene != activeScene) continue;
            string n = t.name;
            string nLower = n.ToLower();

            // Lindungi sistem lingkungan dan VR
            if (nLower.Contains("vr-env") || nLower.Contains("camera") || nLower.Contains("light") ||
                nLower.Contains("yardeditor") || nLower.Contains("eventsystem") || nLower.Contains("viewmode") ||
                nLower.Contains("xrrig") || nLower.Contains("player") || nLower.Contains("search") ||
                nLower.Contains("daynight") || nLower.Contains("plane") || nLower.Contains("gedung") ||
                nLower.Contains("pagar") || nLower.Contains("markajalan") || nLower.Contains("cube") ||
                nLower.Contains("dashedline") || nLower.Contains("containermanager"))
                continue;

            bool isContainer = n.Contains("ft)") || n.Contains("ft_") || n.StartsWith("Container_") ||
                               n.StartsWith("20feet") || n.StartsWith("40feet") || n == "ContainersFromAPI" ||
                               n.Contains("ContainerMarkings");

            bool isOldNumbering = n == "YardNumbering" || n.StartsWith("Numbering_") || n.StartsWith("GridNumbering_") ||
                                  n.StartsWith("YardNumberingAndLabels") || n.StartsWith("Txt_") ||
                                  n.StartsWith("Text_") || n == "LabelUtara" || n == "LabelSelatan" ||
                                  n.StartsWith("BlockIconBox") || n.StartsWith("YellowBlock") || n.Contains("BingkaiKuning");

            bool isOldYard = n == "YardA" || n == "YardB" || n == "YardC" || n == "bawahnyaA" || n == "bawahnyaB" || n == "bawahnyaC";

            if (isContainer || isOldNumbering || isOldYard)
            {
                if (!toDestroy.Contains(t.gameObject))
                {
                    toDestroy.Add(t.gameObject);
                }
            }
        }

        foreach (var go in toDestroy)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
                deletedCount++;
            }
        }

        if (deletedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log($"[CleanPre1Eagle] Selesai! Sebanyak {deletedCount} kontainer & penomoran statis telah dibersihkan dan scene disimpan.");
        }
    }
}
#endif
