using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ParticipantFormController : MonoBehaviour
{
    private static readonly Regex IdRegex = new Regex(@"^[A-Za-z0-9_-]+$");

    private static readonly Color OkColor = new Color(0.20f, 0.40f, 0.85f, 1f);
    private static readonly Color DisabledColor = new Color(0.20f, 0.40f, 0.85f, 0.35f);
    private static readonly Color FieldBg = new Color(0.12f, 0.14f, 0.18f, 1f);
    private static readonly Color FieldText = new Color(0.95f, 0.96f, 0.98f, 1f);
    private static readonly Color FieldPlaceholder = new Color(0.45f, 0.50f, 0.58f, 1f);
    private static readonly Color LabelColor = new Color(0.85f, 0.88f, 0.92f, 1f);
    private static readonly Color ErrorColor = new Color(0.96f, 0.45f, 0.45f, 1f);

    private const float LabelToFieldGap = 20f;
    private const float ErrorGap = 18f;

    // Assessment length bounds (whole minutes). Adjust if the team prefers a
    // different range.
    private const int MinMinutes = 1;
    private const int MaxMinutes = 60;
    private const int DefaultMinutes = 10;

    public Action OnSubmit;

    private TMP_InputField nameField, idField, ageField, sessionField, notesField, lengthField;
    private TMP_Text nameError, idError, ageError, sessionError, lengthError;
    private Button continueButton;
    private Image continueButtonImg;

    private readonly int[] quickOptions = { 5, 10, 15 };
    private Image[] quickBtnImgs;
    private int selectedMinutes = DefaultMinutes;

    public RectTransform BuildUI(Transform parent)
    {
        var panel = UIChrome.BuildScifiPanel(parent, new Vector2(700f, 940f), Vector2.zero);
        panel.gameObject.name = "FormPanel";

        SpawnLabel(panel, "PARTICIPANT INFO", 34, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0f, 400f), new Vector2(640f, 48f), Color.white);

        BuildRow(panel, "Full name *",          labelY:  328f, fieldWidth: 580f, fieldHeight: 56f,
            multiline: false, charLimit: 60, contentType: TMP_InputField.ContentType.Standard,
            out nameField, out nameError);
        BuildRow(panel, "Participant ID *",     labelY:  212f, fieldWidth: 580f, fieldHeight: 56f,
            multiline: false, charLimit: 30, contentType: TMP_InputField.ContentType.Standard,
            out idField, out idError);
        BuildRow(panel, "Age (optional)",       labelY:   96f, fieldWidth: 230f, fieldHeight: 56f,
            multiline: false, charLimit: 3, contentType: TMP_InputField.ContentType.IntegerNumber,
            out ageField, out ageError);
        BuildRow(panel, "Session # (optional)", labelY:  -20f, fieldWidth: 230f, fieldHeight: 56f,
            multiline: false, charLimit: 4, contentType: TMP_InputField.ContentType.IntegerNumber,
            out sessionField, out sessionError);
        BuildLengthRow(panel, labelY: -136f);
        BuildRow(panel, "Notes (optional)",     labelY: -252f, fieldWidth: 580f, fieldHeight: 80f,
            multiline: true, charLimit: 500, contentType: TMP_InputField.ContentType.Standard,
            out notesField, out _);

        continueButton = BuildContinueButton(panel);

        nameField.onValueChanged.AddListener(_ => Validate());
        idField.onValueChanged.AddListener(_ => Validate());
        ageField.onValueChanged.AddListener(_ => Validate());
        sessionField.onValueChanged.AddListener(_ => Validate());

        Validate();
        return panel;
    }

    private void BuildRow(Transform parent, string label, float labelY, float fieldWidth,
        float fieldHeight, bool multiline, int charLimit, TMP_InputField.ContentType contentType,
        out TMP_InputField field, out TMP_Text errorLabel)
    {
        float fieldY = labelY - LabelToFieldGap - fieldHeight / 2f;
        float errorY = fieldY - fieldHeight / 2f - ErrorGap;

        SpawnLabel(parent, label, 22, FontStyles.Bold, TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, labelY), new Vector2(fieldWidth, 28f), LabelColor);

        field = BuildInput(parent, "Field_" + label, new Vector2(fieldWidth, fieldHeight),
            new Vector2(0f, fieldY), contentType, charLimit, multiline);

        errorLabel = SpawnLabel(parent, "", 18, FontStyles.Normal, TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, errorY), new Vector2(fieldWidth, 24f), ErrorColor);
    }

    // The "Assessment length" row: an integer-minutes input (source of truth) on
    // the left with 5 / 10 / 15 quick-pick buttons on the right that just fill it.
    private void BuildLengthRow(Transform parent, float labelY)
    {
        const float fieldHeight = 56f;
        float fieldY = labelY - LabelToFieldGap - fieldHeight / 2f;
        float errorY = fieldY - fieldHeight / 2f - ErrorGap;

        SpawnLabel(parent, "Assessment length (minutes) *", 22, FontStyles.Bold, TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, labelY), new Vector2(580f, 28f), LabelColor);

        lengthField = BuildInput(parent, "Field_Length", new Vector2(230f, fieldHeight),
            new Vector2(-175f, fieldY), TMP_InputField.ContentType.IntegerNumber, 3, multiline: false);

        quickBtnImgs = new Image[quickOptions.Length];
        for (int i = 0; i < quickOptions.Length; i++)
        {
            int minutes = quickOptions[i];
            var rt = NewRect("Quick_" + minutes, parent, new Vector2(86f, 46f), new Vector2(20f + i * 105f, fieldY));
            var img = rt.gameObject.AddComponent<Image>();
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            // Quick-pick just fills the number field; onValueChanged re-validates.
            btn.onClick.AddListener(() => lengthField.text = minutes.ToString());
            quickBtnImgs[i] = img;

            var lblRT = NewRect("Label", rt, Vector2.zero, Vector2.zero);
            lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = Vector2.zero; lblRT.offsetMax = Vector2.zero;
            var lbl = lblRT.gameObject.AddComponent<TextMeshProUGUI>();
            lbl.text = minutes + "m";
            lbl.fontSize = 22;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color = Color.white;
            lbl.alignment = TextAlignmentOptions.Center;
        }

        lengthError = SpawnLabel(parent, "", 18, FontStyles.Normal, TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, errorY), new Vector2(580f, 24f), ErrorColor);

        int initial = Mathf.Clamp(PlayerPrefs.GetInt("MissionMinutes", DefaultMinutes), MinMinutes, MaxMinutes);
        selectedMinutes = initial;
        lengthField.text = initial.ToString();
        lengthField.onValueChanged.AddListener(_ => Validate());
    }

    private TMP_InputField BuildInput(Transform parent, string name, Vector2 size, Vector2 pos,
        TMP_InputField.ContentType contentType, int charLimit, bool multiline)
    {
        var fieldRT = NewRect(name, parent, size, pos);
        fieldRT.gameObject.AddComponent<Image>().color = FieldBg;

        var textRT = NewRect("Text", fieldRT, Vector2.zero, Vector2.zero);
        textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(16f, 10f); textRT.offsetMax = new Vector2(-16f, -10f);
        var text = textRT.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = multiline ? 24 : 26;
        text.color = FieldText;
        text.enableAutoSizing = true;
        text.fontSizeMin = multiline ? 16 : 18;
        text.fontSizeMax = multiline ? 24 : 26;
        text.textWrappingMode = multiline ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;

        var phRT = NewRect("Placeholder", fieldRT, Vector2.zero, Vector2.zero);
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(16f, 10f); phRT.offsetMax = new Vector2(-16f, -10f);
        var placeholder = phRT.gameObject.AddComponent<TextMeshProUGUI>();
        placeholder.fontSize = multiline ? 24 : 26;
        placeholder.color = FieldPlaceholder;
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
        placeholder.text = "";

        var field = fieldRT.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = fieldRT;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
        field.contentType = contentType;
        field.characterLimit = charLimit;
        return field;
    }

    private Button BuildContinueButton(Transform parent)
    {
        var rt = NewRect("ContinueButton", parent, new Vector2(340f, 70f), new Vector2(0f, -392f));
        continueButtonImg = rt.gameObject.AddComponent<Image>();
        continueButtonImg.color = OkColor;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = continueButtonImg;
        btn.onClick.AddListener(TrySubmit);

        var lblRT = NewRect("Label", rt, Vector2.zero, Vector2.zero);
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero; lblRT.offsetMax = Vector2.zero;
        var lbl = lblRT.gameObject.AddComponent<TextMeshProUGUI>();
        lbl.text = "CONTINUE";
        lbl.fontSize = 30;
        lbl.enableAutoSizing = true;
        lbl.fontSizeMin = 18;
        lbl.fontSizeMax = 30;
        lbl.fontStyle = FontStyles.Bold;
        lbl.color = Color.white;
        lbl.alignment = TextAlignmentOptions.Center;
        return btn;
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
        lbl.enableAutoSizing = true;
        lbl.fontSizeMin = Mathf.Max(12f, size * 0.65f);
        lbl.fontSizeMax = size;
        lbl.fontStyle = style;
        lbl.color = color;
        lbl.alignment = align;
        lbl.richText = true;
        return lbl;
    }

    private void Validate()
    {
        bool ok = ValidateName() & ValidateId() & ValidateAge() & ValidateSession() & ValidateLength();
        SetContinueEnabled(ok);
    }

    private bool ValidateName()
    {
        string v = (nameField.text ?? "").Trim();
        if (v.Length == 0) { nameError.text = ""; return false; }
        if (v.Length < 2)   { nameError.text = "Please enter at least 2 characters."; return false; }
        nameError.text = "";
        return true;
    }

    private bool ValidateId()
    {
        string v = (idField.text ?? "").Trim();
        if (v.Length == 0) { idError.text = ""; return false; }
        if (v.Length < 3)  { idError.text = "ID must be at least 3 characters."; return false; }
        if (!IdRegex.IsMatch(v)) { idError.text = "Letters, digits, _ and - only."; return false; }
        idError.text = "";
        return true;
    }

    private bool ValidateAge()
    {
        string v = (ageField.text ?? "").Trim();
        if (v.Length == 0) { ageError.text = ""; return true; }
        if (!int.TryParse(v, out int n) || n < 5 || n > 120)
        { ageError.text = "Age must be 5-120."; return false; }
        ageError.text = "";
        return true;
    }

    private bool ValidateSession()
    {
        string v = (sessionField.text ?? "").Trim();
        if (v.Length == 0) { sessionError.text = ""; return true; }
        if (!int.TryParse(v, out int n) || n < 0 || n > 999)
        { sessionError.text = "Session must be 0-999."; return false; }
        sessionError.text = "";
        return true;
    }

    // Required: whole-number minutes within [MinMinutes, MaxMinutes]. The typed
    // value wins; a custom number (e.g. 7) un-highlights the quick-pick buttons.
    private bool ValidateLength()
    {
        if (lengthField == null) return true;
        string v = (lengthField.text ?? "").Trim();
        if (v.Length == 0) { lengthError.text = "Enter minutes (" + MinMinutes + "-" + MaxMinutes + ")."; RefreshQuickButtons(); return false; }
        if (!int.TryParse(v, out int n)) { lengthError.text = "Whole numbers only."; RefreshQuickButtons(); return false; }
        if (n < MinMinutes || n > MaxMinutes) { lengthError.text = "Must be " + MinMinutes + "-" + MaxMinutes + " minutes."; RefreshQuickButtons(); return false; }
        selectedMinutes = n;
        lengthError.text = "";
        RefreshQuickButtons();
        return true;
    }

    private void RefreshQuickButtons()
    {
        if (quickBtnImgs == null) return;
        for (int i = 0; i < quickOptions.Length; i++)
            if (quickBtnImgs[i] != null)
                quickBtnImgs[i].color = quickOptions[i] == selectedMinutes ? OkColor : FieldBg;
    }

    private void SetContinueEnabled(bool enabled)
    {
        continueButton.interactable = enabled;
        continueButtonImg.color = enabled ? OkColor : DisabledColor;
    }

    private void TrySubmit()
    {
        if (!(ValidateName() & ValidateId() & ValidateAge() & ValidateSession() & ValidateLength())) return;

        var ctx = SessionContext.Instance;
        ctx.FullName = nameField.text.Trim();
        ctx.ParticipantId = idField.text.Trim();
        ctx.Age = int.TryParse(ageField.text.Trim(), out int a) ? a : (int?)null;
        ctx.SessionNumber = int.TryParse(sessionField.text.Trim(), out int s) ? s : (int?)null;
        ctx.Notes = string.IsNullOrEmpty(notesField.text) ? null : notesField.text;
        ctx.MissionMinutes = selectedMinutes;
        ctx.StartedUtc = DateTime.UtcNow;

        PlayerPrefs.SetInt("MissionMinutes", selectedMinutes); // remember last choice

        OnSubmit?.Invoke();
    }
}
