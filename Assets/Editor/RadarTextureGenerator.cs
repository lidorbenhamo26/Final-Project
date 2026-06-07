using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot generator for the radar background sprite used by RadarScanTask.
/// Produces Assets/Resources/Sprites/radar_disc.png — a circular tactical
/// radar display: radial cyan glow, concentric range rings, crosshair, and
/// fine grid, with transparency outside the circle.
/// Run via menu: Mission Focus > Generate Radar Disc Texture.
/// </summary>
public static class RadarTextureGenerator
{
    private const string OutputPath = "Assets/Resources/Sprites/radar_disc.png";
    private const int Size = 512;

    [MenuItem("Mission Focus/Generate Radar Disc Texture")]
    public static void Generate()
    {
        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        Color32[] px = new Color32[Size * Size];

        Vector2 center = new Vector2(Size * 0.5f, Size * 0.5f);
        float maxR = Size * 0.5f - 4f;             // outer radar radius
        float innerR = Size * 0.5f;                // mask radius

        // Palette
        Color edge   = new Color(0.02f, 0.10f, 0.22f, 1f);
        Color midDk  = new Color(0.05f, 0.18f, 0.32f, 1f);
        Color glow   = new Color(0.18f, 0.85f, 0.78f, 1f);
        Color gridC  = new Color(0.20f, 0.90f, 0.85f, 0.55f);
        Color ringC  = new Color(0.30f, 1.00f, 0.95f, 0.85f);
        Color crossC = new Color(0.55f, 1.00f, 0.95f, 0.95f);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Vector2.Distance(p, center);
                int i = y * Size + x;

                if (d > innerR)
                {
                    px[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                // Base: radial gradient from glow at center to edge color.
                float t = Mathf.Clamp01(d / maxR);
                Color c;
                if (t < 0.4f) c = Color.Lerp(glow, midDk, t / 0.4f);
                else          c = Color.Lerp(midDk, edge, (t - 0.4f) / 0.6f);

                // Fine grid (every 32 px), faint cyan.
                if ((x % 32) == 0 || (y % 32) == 0)
                    c = Color.Lerp(c, gridC, 0.55f);

                // Concentric range rings at 25%, 50%, 75%, 95% of radius.
                float[] ringRadii = { maxR * 0.25f, maxR * 0.50f, maxR * 0.75f, maxR * 0.95f };
                foreach (float rr in ringRadii)
                {
                    float ringDist = Mathf.Abs(d - rr);
                    if (ringDist < 1.5f)
                        c = Color.Lerp(c, ringC, 1f - ringDist / 1.5f);
                }

                // Crosshair (horizontal + vertical 2px wide).
                float dx = Mathf.Abs(x + 0.5f - center.x);
                float dy = Mathf.Abs(y + 0.5f - center.y);
                if (dx < 1.0f || dy < 1.0f)
                    c = Color.Lerp(c, crossC, 0.85f);

                // Soft outer fade (last 12 pixels) for circular vignette.
                if (d > maxR - 2f)
                {
                    float fade = Mathf.Clamp01((innerR - d) / 6f);
                    c.a *= fade;
                }

                px[i] = c;
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);

        byte[] pngBytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        string fullPath = Path.Combine(Application.dataPath, OutputPath.Substring("Assets/".Length));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllBytes(fullPath, pngBytes);

        AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);

        TextureImporter ti = AssetImporter.GetAtPath(OutputPath) as TextureImporter;
        if (ti != null)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.SaveAndReimport();
        }

        Debug.Log("[RadarTextureGenerator] Wrote " + OutputPath + " (" + pngBytes.Length + " bytes)");
    }
}
