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

    public Action OnSubmit;

    private TMP_InputField nameField, idField, ageField, sessionField, notesField;
    private TMP_Text nameError, idError, ageError, sessionError;
    private Button continueButton;
    private Image continueButtonImg;

    public RectTransform BuildUI(Transform parent)
    {
        var panel = UIChrome.BuildScifiPanel(parent, new Vector2(640f, 800f), Vector2.zero);
        panel.gameObject.name = "FormPanel";

        SpawnLabel(panel, "PARTICIPANT INFO", 28, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0f, 360f), new Vector2(600f, 40f), Color.white);

        BuildRow(panel, "Full name *",          labelY:  300f, fieldWidth: 540f, fieldHeight: 44f,
            multiline: false, charLimit: 60, contentType: TMP_InputField.ContentType.Standard,
            out nameField, out nameError);
        BuildRow(panel, "Participant ID *",     labelY:  170f, fieldWidth: 540f, fieldHeight: 44f,
            multiline: false, charLimit: 30, contentType: TMP_InputField.ContentType.Standard,
            out idField, out idError);
        BuildRow(panel, "Age (optional)",       labelY:   40f, fieldWidth: 200f, fieldHeight: 44f,
            multiline: false, charLimit: 3, contentType: TMP_InputField.ContentType.IntegerNumber,
            out ageField, out ageError);
        BuildRow(panel, "Session # (optional)", labelY:  -90f, fieldWidth: 200f, fieldHeight: 44f,
            multiline: false, charLimit: 4, contentType: TMP_InputField.ContentType.IntegerNumber,
            out sessionField, out sessionError);
        BuildRow(panel, "Notes (optional)",     labelY: -220f, fieldWidth: 540f, fieldHeight: 90f,
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
        float fieldY = labelY - 16f - fieldHeight / 2f;
        float errorY = fieldY - fieldHeight / 2f - 14f;

        SpawnLabel(parent, label, 18, FontStyles.Normal, TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, labelY), new Vector2(fieldWidth, 22f), LabelColor);

        var fieldRT = NewRect("Field_" + label, parent,
            new Vector2(fieldWidth, fieldHeight), new Vector2(0f, fieldY));
        var fieldImg = fieldRT.gameObject.AddComponent<Image>();
        fieldImg.color = FieldBg;

        var textRT = NewRect("Text", fieldRT, Vector2.zero, Vector2.zero);
        textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(12f, 8f); textRT.offsetMax = new Vector2(-12f, -8f);
        var text = textRT.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 22; text.color = FieldText;
        text.enableWordWrapping = multiline;
        text.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;

        var phRT = NewRect("Placeholder", fieldRT, Vector2.zero, Vector2.zero);
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(12f, 8f); phRT.offsetMax = new Vector2(-12f, -8f);
        var placeholder = phRT.gameObject.AddComponent<TextMeshProUGUI>();
        placeholder.fontSize = 22; placeholder.color = FieldPlaceholder;
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
        placeholder.text = "";

        field = fieldRT.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = fieldRT;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
        field.contentType = contentType;
        field.characterLimit = charLimit;

        errorLabel = SpawnLabel(parent, "", 16, FontStyles.Normal, TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, errorY), new Vector2(fieldWidth, 20f), ErrorColor);
    }

    private Button BuildContinueButton(Transform parent)
    {
        var rt = NewRect("ContinueButton", parent, new Vector2(320f, 64f), new Vector2(0f, -360f));
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
        lbl.fontSize = 28;
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
        lbl.fontStyle = style;
        lbl.color = color;
        lbl.alignment = align;
        lbl.richText = true;
        return lbl;
    }

    private void Validate()
    {
        bool ok = ValidateName() & ValidateId() & ValidateAge() & ValidateSession();
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

    private void SetContinueEnabled(bool enabled)
    {
        continueButton.interactable = enabled;
        continueButtonImg.color = enabled ? OkColor : DisabledColor;
    }

    private void TrySubmit()
    {
        if (!(ValidateName() & ValidateId() & ValidateAge() & ValidateSession())) return;

        var ctx = SessionContext.Instance;
        ctx.FullName = nameField.text.Trim();
        ctx.ParticipantId = idField.text.Trim();
        ctx.Age = int.TryParse(ageField.text.Trim(), out int a) ? a : (int?)null;
        ctx.SessionNumber = int.TryParse(sessionField.text.Trim(), out int s) ? s : (int?)null;
        ctx.Notes = string.IsNullOrEmpty(notesField.text) ? null : notesField.text;
        ctx.StartedUtc = DateTime.UtcNow;

        OnSubmit?.Invoke();
    }
}
