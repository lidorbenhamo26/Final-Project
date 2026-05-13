using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Captures a screenshot of the scene from the player's approach
/// angle toward NavigationStation, so we can verify whether the radar dish
/// is actually visible in-game (vs. occluded by walls / ceiling / canvas).
/// Saves to Assets/Models/MeshyProps/NavigationRadarDish/radar_dish_scene.png.
/// Run via Mission Focus > Capture Radar Scene Screenshot.</summary>
public static class RadarSceneCapture
{
    private const string OutPath = "Assets/Models/MeshyProps/NavigationRadarDish/radar_dish_scene.png";
    private const int Width = 1280;
    private const int Height = 720;

    [MenuItem("Mission Focus/Capture Radar Scene Screenshot")]
    public static void Capture()
    {
        // NavigationStation is at world (16, 0, 0). Player approaches from -X.
        // Position camera roughly at player eye height, looking at the dish/canvas area.
        // Position camera at the player dock-approach angle, looking at the new
        // station console (which is now the radar dish replacing the orb).
        Vector3 camPos = new Vector3(14.0f, 1.7f, 0f);
        Vector3 lookAt = new Vector3(16.0f, 1.2f, 0f);

        GameObject camGo = new GameObject("__RadarSceneCam");
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.04f, 0.08f, 1f);
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 200f;
        cam.transform.position = camPos;
        cam.transform.LookAt(lookAt);

        RenderTexture rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        byte[] png = tex.EncodeToPNG();
        string fullPath = Path.Combine(Application.dataPath, OutPath.Substring("Assets/".Length));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllBytes(fullPath, png);

        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(camGo);

        AssetDatabase.ImportAsset(OutPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("[RadarSceneCapture] Wrote " + OutPath + " (" + png.Length + " bytes), cam at " + camPos + " looking at " + lookAt);
    }
}
