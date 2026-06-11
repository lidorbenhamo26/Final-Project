using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Owns the assessor report screen (UI Toolkit overlay).
/// Two entry points:
///   ShowMissionEnd() — replaces the old MissionEndUI at mission complete
///                      (plays the mission_complete music/voice once).
///   ToggleDebug()    — F12 anytime; freezes the mission while open and
///                      restores the prior freeze state on close.
/// Added at runtime by HUDManager.Awake(); no scene wiring required.
/// </summary>
public class AssessmentReportController : MonoBehaviour
{
    private const string PanelSettingsPath = "UI/Report/AssessmentReportPanelSettings";
    private const string ThemePath = "UI/Report/AssessmentReportTheme";
    private const string StyleSheetPath = "UI/Report/AssessmentReport";

    public static AssessmentReportController Instance { get; private set; }

    public bool IsVisible { get; private set; }

    private UIDocument uiDocument;
    private bool missionEndMode;
    private bool missionEndAudioPlayed;

    private CursorLockMode prevCursorLock;
    private bool prevCursorVisible;
    private bool prevFrozen;
    private bool frozeForReport;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnsubscribeChanged();
        // Safety net: IsDebugFrozen is static and survives scene loads — never
        // let a freeze we own outlive this controller.
        if (frozeForReport)
        {
            frozeForReport = false;
            GameManager.SetDebugFrozen(prevFrozen);
        }
    }

    /// <summary>Mission-complete mode: shown automatically by HUDManager when the mission ends.</summary>
    public void ShowMissionEnd()
    {
        missionEndMode = true;
        // Freeze in-flight tasks too: trials silently expiring behind the end
        // screen would be recorded as fake omissions in the participant data.
        if (!frozeForReport)
        {
            prevFrozen = GameManager.IsDebugFrozen;
            GameManager.SetDebugFrozen(true);
            frozeForReport = true;
        }
        if (!missionEndAudioPlayed)
        {
            missionEndAudioPlayed = true;
            AudioManager.Instance.PlayMusic("mission_complete", loop: false);
            AudioManager.Instance.PlayVoice("mission_complete");
        }
        Show();
    }

    /// <summary>F12 debug mode: toggle the report at any time, freezing the mission while open.</summary>
    public void ToggleDebug()
    {
        if (IsVisible) { Hide(); return; }

        // Freeze gameplay (timer, spawns, task drains) while the report is up,
        // remembering whether F11 already had it frozen.
        prevFrozen = GameManager.IsDebugFrozen;
        GameManager.SetDebugFrozen(true);
        frozeForReport = true;

        Show();
    }

    public void Hide()
    {
        if (!IsVisible) return;
        IsVisible = false;

        UnsubscribeChanged();
        if (uiDocument != null)
            uiDocument.rootVisualElement.style.display = DisplayStyle.None;

        if (frozeForReport)
        {
            frozeForReport = false;
            GameManager.SetDebugFrozen(prevFrozen);
        }

        // Restore the pre-report cursor unless the player is docked at a
        // station — docked state owns an unlocked, visible cursor.
        bool docked = StationDockController.Instance != null && StationDockController.Instance.IsDocked;
        if (!docked)
        {
            UnityEngine.Cursor.lockState = prevCursorLock;
            UnityEngine.Cursor.visible = prevCursorVisible;
        }
    }

    private void Show()
    {
        if (IsVisible)
        {
            Rebuild();
            return;
        }
        IsVisible = true;

        prevCursorLock = UnityEngine.Cursor.lockState;
        prevCursorVisible = UnityEngine.Cursor.visible;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;

        EnsureDocument();
        uiDocument.rootVisualElement.style.display = DisplayStyle.Flex;
        Rebuild();
        SubscribeChanged();
    }

    // ------------------------------------------------------------- internals

    private void EnsureDocument()
    {
        if (uiDocument != null) return;

        var panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
        if (panelSettings == null)
        {
            Debug.LogWarning("[AssessmentReport] PanelSettings asset missing (run " +
                "Tools/Mission Focus/Create Report UI Assets). Using a runtime fallback.");
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "AssessmentReportPanelSettings_RuntimeFallback";
            var theme = Resources.Load<ThemeStyleSheet>(ThemePath);
            if (theme != null) panelSettings.themeStyleSheet = theme;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 1f;
            panelSettings.sortingOrder = 1000;
        }

        var go = new GameObject("AssessmentReport_UIDocument");
        go.transform.SetParent(transform, false);
        uiDocument = go.AddComponent<UIDocument>();
        uiDocument.panelSettings = panelSettings;

        var styleSheet = Resources.Load<StyleSheet>(StyleSheetPath);
        if (styleSheet != null)
            uiDocument.rootVisualElement.styleSheets.Add(styleSheet);
        else
            Debug.LogWarning("[AssessmentReport] Stylesheet missing at Resources/" + StyleSheetPath + ".uss");
    }

    private void Rebuild()
    {
        if (uiDocument == null) return;
        var data = ReportData.Capture();
        var callbacks = new ReportCallbacks
        {
            NewParticipant = OnNewParticipant,
            RunAgain = OnRunAgain,
            ExportHtml = OnExportHtml,
            ExportCsv = OnExportCsv,
            OpenDataFolder = OnOpenDataFolder,
            Close = Hide,
            ShowClose = !missionEndMode,
        };
        AssessmentReportView.Build(uiDocument.rootVisualElement, data, callbacks);
        // Live rebuilds (late task resolves) recreate the tree — keep the last
        // export confirmation visible.
        if (!string.IsNullOrEmpty(lastExportStatus))
        {
            var label = uiDocument.rootVisualElement.Q<Label>("export-status");
            if (label != null) label.text = lastExportStatus;
        }
    }

    private bool _changedSubscribed;

    private void SubscribeChanged()
    {
        if (_changedSubscribed) return;
        _changedSubscribed = true;
        AssessmentResults.Instance.Changed += HandleResultsChanged;
    }

    private void UnsubscribeChanged()
    {
        if (!_changedSubscribed) return;
        _changedSubscribed = false;
        var results = AssessmentResults.InstanceOrNull;
        if (results != null)
            results.Changed -= HandleResultsChanged;
    }

    private void HandleResultsChanged()
    {
        // Tasks still in flight when the mission ends resolve late — keep the
        // visible report current.
        if (IsVisible) Rebuild();
    }

    // --------------------------------------------------------------- buttons

    private void OnNewParticipant()
    {
        AudioManager.Instance.PlaySfx("button_click");
        ReleaseBeforeSceneChange();
        if (SessionContext.Instance != null) SessionContext.Instance.Reset();
        if (SessionManager.Instance != null) SessionManager.Instance.ResetForNewMission();
        SceneTransition.LoadStart();
    }

    private void OnRunAgain()
    {
        AudioManager.Instance.PlaySfx("button_click");
        ReleaseBeforeSceneChange();
        if (SessionManager.Instance != null) SessionManager.Instance.ResetForNewMission();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// IsDebugFrozen is static and would leak into the next scene, leaving the
    /// new mission permanently frozen. A fresh run always starts unfrozen.
    /// </summary>
    private void ReleaseBeforeSceneChange()
    {
        Hide();
        GameManager.SetDebugFrozen(false);
    }

    private void OnExportHtml()
    {
        AudioManager.Instance.PlaySfx("button_click");
        try
        {
            string path = ReportHtmlExporter.Export(ReportData.Capture());
            SetExportStatus("Saved: " + path);
            Application.OpenURL(new Uri(path).AbsoluteUri);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AssessmentReport] HTML export failed: " + ex.Message);
            SetExportStatus("HTML export failed: " + ex.Message);
        }
    }

    private void OnExportCsv()
    {
        AudioManager.Instance.PlaySfx("button_click");
        try
        {
            string path = ReportCsvExporter.Export(ReportData.Capture());
            SetExportStatus("Saved: " + path);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AssessmentReport] CSV export failed: " + ex.Message);
            SetExportStatus("CSV export failed: " + ex.Message);
        }
    }

    private void OnOpenDataFolder()
    {
        AudioManager.Instance.PlaySfx("button_click");
        Application.OpenURL(new Uri(Application.persistentDataPath).AbsoluteUri);
    }

    private string lastExportStatus = "";

    private void SetExportStatus(string message)
    {
        lastExportStatus = message;
        var label = uiDocument != null ? uiDocument.rootVisualElement.Q<Label>("export-status") : null;
        if (label != null) label.text = message;
    }
}
