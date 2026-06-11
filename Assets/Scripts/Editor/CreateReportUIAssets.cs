using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// One-shot utility: creates the PanelSettings asset the assessor report
/// screen loads from Resources. PanelSettings can't be hand-authored as YAML
/// (theme reference is a guid), so it's built programmatically here.
/// Idempotent — safe to run again.
/// </summary>
public static class CreateReportUIAssets
{
    private const string Dir = "Assets/Resources/UI/Report";
    private const string PanelSettingsPath = Dir + "/AssessmentReportPanelSettings.asset";
    private const string ThemePath = Dir + "/AssessmentReportTheme.tss";

    [MenuItem("Tools/Mission Focus/Create Report UI Assets")]
    public static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath) != null)
        {
            Debug.Log("[CreateReportUIAssets] Already exists: " + PanelSettingsPath);
            return;
        }

        Directory.CreateDirectory(Dir);
        AssetDatabase.Refresh();

        var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
        if (theme == null)
            Debug.LogWarning("[CreateReportUIAssets] Theme not found at " + ThemePath +
                             " — PanelSettings will be created without a theme.");

        var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.themeStyleSheet = theme;
        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(1920, 1080);
        panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        // Match height, mirroring HUDManager's CanvasScaler setup.
        panelSettings.match = 1f;
        // Above the HUD canvas (100) and the old MissionEnd canvas (999).
        panelSettings.sortingOrder = 1000;

        AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[CreateReportUIAssets] Created " + PanelSettingsPath);
    }
}
