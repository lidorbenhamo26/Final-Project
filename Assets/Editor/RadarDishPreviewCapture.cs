using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Editor utility that captures a high-quality preview screenshot of
/// the NavigationRadarDish from a flattering 3/4 angle, with proper lighting
/// from the scene. Saves to Assets/Models/MeshyProps/NavigationRadarDish/
/// radar_dish_preview.png. Run via Mission Focus > Capture Radar Dish Preview.</summary>
public static class RadarDishPreviewCapture
{
    private const string OutPath = "Assets/Models/MeshyProps/NavigationRadarDish/radar_dish_preview.png";
    private const int Width = 960;
    private const int Height = 720;

    [MenuItem("Mission Focus/Capture Radar Dish Preview")]
    public static void Capture()
    {
        GameObject dish = GameObject.Find("NavigationRadarDish");
        if (dish == null)
        {
            Debug.LogError("[RadarDishPreviewCapture] NavigationRadarDish not found in scene.");
            return;
        }

        // Compute combined bounds across all child renderers.
        Renderer[] rends = dish.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0)
        {
            Debug.LogError("[RadarDishPreviewCapture] No renderers found on NavigationRadarDish.");
            return;
        }
        Bounds bounds = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) bounds.Encapsulate(rends[i].bounds);

        Vector3 center = bounds.center;
        float radius = bounds.extents.magnitude;
        if (radius < 0.01f) radius = 1f;

        // Set up a one-shot capture camera positioned 3/4 view, slightly above.
        GameObject camGo = new GameObject("__RadarPreviewCam");
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.04f, 0.08f, 1f);
        cam.fieldOfView = 28f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 500f;
        cam.transform.position = center + new Vector3(radius * 2.2f, radius * 1.1f, -radius * 2.5f);
        cam.transform.LookAt(center);

        // Optional fill light to ensure visibility even in a dark scene.
        GameObject lightGo = new GameObject("__RadarPreviewLight");
        Light fill = lightGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 1.1f;
        fill.color = new Color(1f, 0.97f, 0.93f);
        lightGo.transform.position = center + new Vector3(radius, radius * 1.5f, -radius * 1.5f);
        lightGo.transform.LookAt(center);

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
        Object.DestroyImmediate(lightGo);

        AssetDatabase.ImportAsset(OutPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("[RadarDishPreviewCapture] Wrote " + OutPath + " (" + png.Length + " bytes)");
    }
}
