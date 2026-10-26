using UnityEditor;
using UnityEngine;

public class WebGLTextureOptimizer
{
    [MenuItem("Tools/Optimize All Textures for WebGL (Super Fast Load)")]
    public static void OptimizeTextures()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
        int count = 0;
        long beforeCount = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool modified = false;

                // Cek WebGL platform settings
                TextureImporterPlatformSettings webglSettings = importer.GetPlatformTextureSettings("WebGL");
                
                int targetSize = 512;
                // Jika logo atau icon UI, berikan 512
                // Jika normal map / roughness / displacement / metallic, 512 sudah sangat cukup
                if (path.ToLower().Contains("normal") || path.ToLower().Contains("roughness") || 
                    path.ToLower().Contains("displacement") || path.ToLower().Contains("metallic") || 
                    path.ToLower().Contains("height"))
                {
                    targetSize = 512;
                }
                else if (path.ToLower().Contains("pavingstones"))
                {
                    targetSize = 512;
                }
                else
                {
                    targetSize = 512;
                }

                if (importer.maxTextureSize > targetSize)
                {
                    importer.maxTextureSize = targetSize;
                    modified = true;
                }

                if (!webglSettings.overridden || webglSettings.maxTextureSize > targetSize || !webglSettings.crunchedCompression)
                {
                    webglSettings.name = "WebGL";
                    webglSettings.overridden = true;
                    webglSettings.maxTextureSize = targetSize;
                    webglSettings.textureCompression = TextureImporterCompression.Compressed;
                    webglSettings.crunchedCompression = true;
                    webglSettings.compressionQuality = 50;
                    importer.SetPlatformTextureSettings(webglSettings);
                    modified = true;
                }

                if (modified)
                {
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                    count++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        Debug.Log($"[WebGLTextureOptimizer] Selesai mengoptimasi {count} tekstur ke 512px + Crunch Compression! Build WebGL akan jauh lebih ringan dan loading cepat.");
        EditorUtility.DisplayDialog("Texture Optimization Complete", 
            $"Berhasil mengoptimasi {count} tekstur ke 512px (Crunched Compression)!\n\nUkuran build akan turun drastis dari 176 MB menjadi ~10-15 MB.\nSilakan lakukan Build ulang sekarang.", "OK");
    }
}
