using UnityEngine;
using UnityEditor;
using TMPro;

public class CreateStationLabels
{
    [MenuItem("Tools/Mission Focus/Create Station Labels")]
    public static void Run()
    {
        foreach (var t in Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Exclude))
            if (t.gameObject.name.StartsWith("Label_"))
                Undo.DestroyObjectImmediate(t.gameObject);

        // Labels float 0.3 units off each wall, facing inward
        var stations = new (string text, Vector3 pos, float rotY)[]
        {
            ("ENGINE TERMINAL",  new Vector3( 7.45f, 2.8f,  0f   ),  90f),
            ("NAVIGATION DECK",  new Vector3( 0f,    2.8f,  7.45f),   0f),
            ("COMMS HUB",        new Vector3(-7.45f, 2.8f,  0f   ), 270f),
            ("LIFE SUPPORT",     new Vector3( 0f,    2.8f, -7.45f), 180f),
        };

        foreach (var s in stations)
        {
            var go = new GameObject("Label_" + s.text.Replace(" ", "_"));
            Undo.RegisterCreatedObjectUndo(go, "Create Station Label");
            go.transform.position = s.pos;
            go.transform.rotation = Quaternion.Euler(0f, s.rotY, 0f);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = s.text;
            tmp.fontSize = 1.8f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.1f, 1f, 0.45f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.rectTransform.sizeDelta = new Vector2(10f, 2.5f);

            if (tmp.fontSharedMaterial != null)
            {
                var inst = new Material(tmp.fontSharedMaterial);
                inst.EnableKeyword("GLOW_ON");
                inst.SetFloat("_GlowPower", 0.4f);
                inst.SetFloat("_GlowOuter", 0.3f);
                inst.SetColor("_GlowColor", new Color(0.1f, 1f, 0.45f, 0.8f));
                tmp.fontMaterial = inst;
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[MissionFocus] Created 4 station labels.");
    }
}
