using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public class WebGLPostProcessBuild
{
    [PostProcessBuild(100)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.WebGL) return;

        Debug.Log("[WebGLPostProcessBuild] Running post-process on: " + pathToBuiltProject);

        string templateDir = Path.Combine(Application.dataPath, "WebGLTemplates", "WebXRFullView2020");

        // 1. Copy _redirects (Netlify API proxy & SPA routing)
        string redirectsSrc = Path.Combine(templateDir, "_redirects");
        string redirectsDst = Path.Combine(pathToBuiltProject, "_redirects");
        if (File.Exists(redirectsSrc))
        {
            File.Copy(redirectsSrc, redirectsDst, true);
            Debug.Log("[WebGLPostProcessBuild] Copied _redirects to build output.");
        }
        else
        {
            File.WriteAllText(redirectsDst, "/api/getCtrLoc  https://apps.globalhalalhub.co.id/global-halal-api/api/vr/getCtrLoc  200!\n/api/getMasterBlock  https://apps.globalhalalhub.co.id/global-halal-api/api/vr/getMasterBlock  200!\n");
            Debug.Log("[WebGLPostProcessBuild] Created _redirects in build output.");
        }

        // 2. Copy _headers
        string headersSrc = Path.Combine(templateDir, "_headers");
        string headersDst = Path.Combine(pathToBuiltProject, "_headers");
        if (File.Exists(headersSrc))
        {
            File.Copy(headersSrc, headersDst, true);
            Debug.Log("[WebGLPostProcessBuild] Copied _headers to build output.");
        }

        // 3. Copy netlify.toml
        string tomlSrc = Path.Combine(templateDir, "netlify.toml");
        string tomlDst = Path.Combine(pathToBuiltProject, "netlify.toml");
        if (File.Exists(tomlSrc))
        {
            File.Copy(tomlSrc, tomlDst, true);
            Debug.Log("[WebGLPostProcessBuild] Copied netlify.toml to build output.");
        }

        // 4. Copy vercel.json
        string vercelSrc = Path.Combine(templateDir, "vercel.json");
        string vercelDst = Path.Combine(pathToBuiltProject, "vercel.json");
        if (File.Exists(vercelSrc))
        {
            File.Copy(vercelSrc, vercelDst, true);
            Debug.Log("[WebGLPostProcessBuild] Copied vercel.json to build output.");
        }

        Debug.Log("[WebGLPostProcessBuild] Post-process completed successfully. Build is ready for Netlify / Vercel / Cloudflare!");
    }
}
