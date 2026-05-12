using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialPanelController : MonoBehaviour
{
    private static readonly (string Title, string Body)[] Slides = new (string, string)[]
    {
        ("WELCOME",
         "You're the sole astronaut on a 10-minute shift. Keep four ship systems running while a cognitive task spawns at each station."),
        ("MOVEMENT",
         "WASD or arrow keys to walk.\nShift to sprint.\nSpace to jump.\nMouse to look around."),
        ("DOCKING WITH A STATION",
         "Walk up to a station and press E to dock. Press E or Esc to leave once you're done."),
        ("THE FOUR STATIONS",
         "Engine, Navigation, Comms, and Life Support.\nEach runs a different cognitive task — Stroop, N-Back, Pattern Match, or Code Memory."),
        ("COGNITIVE TASKS",
         "Each task has a short time limit. Both accuracy and reaction time are recorded. Don't let too many tasks pile up — failed tasks count against you."),
        ("TIMING & SCORING",
         "The mission runs for 10 minutes. Your full session is logged to CSV: passed and failed counts, average reaction time, plus per-task timing."),
        ("READY",
         "Take a breath. When you press Continue, you'll move on to the ship briefing."),
    };

    private static readonly Color PanelColor = new Color(0.08f, 0.10f, 0.14f, 0.92f);
    private static readonly Color OkColor = new Color(0.20f, 0.40f, 0.85f, 1f);
    private static readonly Color DisabledColor = new Color(0.20f, 0.40f, 0.85f, 0.35f);
    private static readonly Color SkipColor = new Color(0.30f, 0.34f, 0.40f, 1f);
    private static readonly Color BodyColor = new Color(0.92f, 0.94f, 0.97f, 1f);
    private static readonly Color PageColor = new Color(0.55f, 0.60f, 0.68f, 1f);

    public Action OnFinish;

    private TMP_Text titleLbl, bodyLbl, pageLbl, nextLbl;
    private Button prevBtn, nextBtn;
    private Image prevImg, nextImg;
    private RectTransform panel;
    private int index;

    public RectTransform BuildUI(Transform parent)
    {
        panel = NewRect("TutorialPanel", parent, new Vector2(960f, 640f), Vector2.zero);
        var panelImg = panel.gameObject.AddComponent<Image>();
        panelImg.color = PanelColor;

        titleLbl = SpawnLabel(panel, "", 36, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0f, 250f), new Vector2(900f, 60f), Color.white);

        bodyLbl = SpawnLabel(panel, "", 24, FontStyles.Normal, TextAlignmentOptions.Center,
            new Vector2(0f, 40f), new Vector2(820f, 320f), BodyColor);
        bodyLbl.enableWordWrapping = true;

        pageLbl = SpawnLabel(panel, "", 18, FontStyles.Normal, TextAlignmentOptions.Center,
            new Vector2(0f, -200f), new Vector2(200f, 24f), PageColor);

        (prevBtn, prevImg, _) = BuildButton(panel, "PREV",
            new Vector2(-360f, -270f), new Vector2(200f, 56f), OkColor, OnPrev);
        (_, _, _) = BuildButton(panel, "SKIP",
            new Vector2(0f, -270f), new Vector2(160f, 56f), SkipColor, OnSkip);
        (nextBtn, nextImg, nextLbl) = BuildButton(panel, "NEXT",
            new Vector2(360f, -270f), new Vector2(200f, 56f), OkColor, OnNext);

        Refresh();
        return panel;
    }

    public void Show() { panel.gameObject.SetActive(true); index = 0; Refresh(); }
    public void Hide() => panel.gameObject.SetActive(false);

    private void OnPrev()
    {
        if (index > 0) { index--; Refresh(); }
    }

    private void OnNext()
    {
        if (index < Slides.Length - 1) { index++; Refresh(); }
        else { Finish(); }
    }

    private void OnSkip() => Finish();

    private void Refresh()
    {
        var slide = Slides[index];
        titleLbl.text = slide.Title;
        bodyLbl.text = slide.Body;
        pageLbl.text = (index + 1) + " / " + Slides.Length;

        bool atFirst = index == 0;
        bool atLast = index == Slides.Length - 1;
        prevBtn.interactable = !atFirst;
        prevImg.color = atFirst ? DisabledColor : OkColor;
        nextLbl.text = atLast ? "CONTINUE" : "NEXT";
    }

    private void Finish()
    {
        SessionContext.Instance.TutorialCompleted = true;
        OnFinish?.Invoke();
    }

    private (Button btn, Image img, TMP_Text label) BuildButton(Transform parent, string text,
        Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var rt = NewRect("Btn_" + text, parent, size, pos);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var lblRT = NewRect("Label", rt, Vector2.zero, Vector2.zero);
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero; lblRT.offsetMax = Vector2.zero;
        var lbl = lblRT.gameObject.AddComponent<TextMeshProUGUI>();
        lbl.text = text;
        lbl.fontSize = 24;
        lbl.fontStyle = FontStyles.Bold;
        lbl.color = Color.white;
        lbl.alignment = TextAlignmentOptions.Center;
        return (btn, img, lbl);
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 anchoredPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        return rt;
    }

    private static TMP_Text SpawnLabel(Transform parent, string text, int size, FontStyles style,
        TextAlignmentOptions align, Vector2 pos, Vector2 sizeDelta, Color color)
    {
        var rt = NewRect("Label", parent, sizeDelta, pos);
        var lbl = rt.gameObject.AddComponent<TextMeshProUGUI>();
        lbl.text = text;
        lbl.fontSize = size;
        lbl.fontStyle = style;
        lbl.color = color;
        lbl.alignment = align;
        lbl.richText = true;
        return lbl;
    }
}
