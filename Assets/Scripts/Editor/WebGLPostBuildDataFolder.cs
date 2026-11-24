#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Otomatis membuat folder 'data/' di direktori hasil build WebGL
/// yang berisi '.env', 'app.properties', dan 'denah.json' sehingga pengguna
/// dapat mengedit endpoint API dan denah layout setelah build sebelum di-deploy.
/// </summary>
public class WebGLPostBuildDataFolder : IPostprocessBuildWithReport
{
    public int callbackOrder => 100;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;

        string buildOutputDir = report.summary.outputPath;
        if (string.IsNullOrEmpty(buildOutputDir)) return;

        // Jika outputPath berupa file (misal index.html), ambil direktori induknya
        if (File.Exists(buildOutputDir))
        {
            buildOutputDir = Path.GetDirectoryName(buildOutputDir);
        }

        string targetDataDir = Path.Combine(buildOutputDir, "data");
        if (!Directory.Exists(targetDataDir))
        {
            Directory.CreateDirectory(targetDataDir);
        }

        string rootDataDir = Path.Combine(Application.dataPath, "..", "data");
        string streamingDataDir = Path.Combine(Application.dataPath, "StreamingAssets", "data");

        // 1. Salin dari folder data/ root proyek jika ada
        if (Directory.Exists(rootDataDir))
        {
            foreach (string file in Directory.GetFiles(rootDataDir))
            {
                if (file.EndsWith(".meta")) continue;
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDataDir, fileName);
                File.Copy(file, destFile, true);
                Debug.Log($"[WebGLPostBuild] Copied {fileName} from root data/ to {destFile}");

                // Sinkronkan juga ke StreamingAssets/data agar selaras
                if (Directory.Exists(streamingDataDir))
                {
                    try { File.Copy(file, Path.Combine(streamingDataDir, fileName), true); } catch {}
                }
            }
        }
        // 2. Salin dari StreamingAssets/data jika root data tidak ada
        else if (Directory.Exists(streamingDataDir))
        {
            foreach (string file in Directory.GetFiles(streamingDataDir))
            {
                if (file.EndsWith(".meta")) continue;
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDataDir, fileName);
                File.Copy(file, destFile, true);
                Debug.Log($"[WebGLPostBuild] Copied {fileName} from StreamingAssets to {destFile}");
            }
        }
        else
        {
            // Buat file default jika belum ada
            string envFile = Path.Combine(targetDataDir, ".env");
            if (!File.Exists(envFile))
            {
                File.WriteAllText(envFile,
                    "# 1Eagle VR - API Configuration\n" +
                    "API_MASTER_BLOCK=https://apps.globalhalalhub.co.id/global-halal-api/api/vr/getMasterBlock\n" +
                    "API_CONTAINER_LOC=https://apps.globalhalalhub.co.id/global-halal-api/api/vr/getCtrLoc\n");
            }

            string propFile = Path.Combine(targetDataDir, "app.properties");
            if (!File.Exists(propFile))
            {
                File.WriteAllText(propFile,
                    "# 1Eagle VR - API Configuration\n" +
                    "API_MASTER_BLOCK=https://apps.globalhalalhub.co.id/global-halal-api/api/vr/getMasterBlock\n" +
                    "API_CONTAINER_LOC=https://apps.globalhalalhub.co.id/global-halal-api/api/vr/getCtrLoc\n");
            }
        }

        // Hapus duplikat StreamingAssets/data di folder build agar hanya ada YardEditor_WEBGL/data
        string buildStreamingDataDir = Path.Combine(buildOutputDir, "StreamingAssets", "data");
        if (Directory.Exists(buildStreamingDataDir))
        {
            try { Directory.Delete(buildStreamingDataDir, true); } catch {}
        }
        string buildStreamingDir = Path.Combine(buildOutputDir, "StreamingAssets");
        if (Directory.Exists(buildStreamingDir) && Directory.GetFileSystemEntries(buildStreamingDir).Length == 0)
        {
            try { Directory.Delete(buildStreamingDir, true); } catch {}
        }

        Debug.Log($"[WebGLPostBuild] Folder 'data/' berhasil dipersiapkan di: {targetDataDir}");
    }
}
#endif
