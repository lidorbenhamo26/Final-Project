using UnityEditor;
using UnityEngine;

namespace SpaceStation.EditorSetup
{
    /// <summary>
    /// Setup/24 — Install the open fuse-box prop next to the Life Support
    /// battery socket, the diegetic anchor for the wire-matching puzzle
    /// (WiringPuzzlePanel): a wall box with its cover swung open and four
    /// colored wires (red/yellow/blue/green — the puzzle palette) exposed
    /// inside. Built procedurally from primitives in the BatterySocket
    /// BuildPad() style; decoration only, so every collider is stripped.
    /// Idempotent — safe to re-run.
    /// </summary>
    public static class InstallWiringPanelProp
    {
        private const string MatFolder = "Assets/Materials/Props";
        private const string PropName = "WiringPanelProp";

        // The puzzle palette (CodeMemoryTask / WiringPuzzlePanel).
        private static readonly Color[] WireColors =
        {
            new Color(0.90f, 0.20f, 0.20f), // red
            new Color(0.95f, 0.85f, 0.15f), // yellow
            new Color(0.20f, 0.45f, 0.95f), // blue
            new Color(0.25f, 0.80f, 0.35f), // green
        };

        [MenuItem("Setup/24 - Install Wiring Panel Prop")]
        public static void Install()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) { Debug.LogError("[WiringProp] URP/Lit shader missing. Aborting."); return; }

            var station = GameObject.Find("LifeSupportStation");
            if (station == null)
            {
                Debug.LogWarning("[WiringProp] LifeSupportStation not found — open the main scene first.");
                return;
            }

            EnsureFolder("Assets/Materials");
            EnsureFolder(MatFolder);

            var old = station.transform.Find(PropName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var root = new GameObject(PropName);
            root.transform.SetParent(station.transform, false);
            // Mounted on the console's front face (face at local z≈0.405,
            // console half-width 0.74), upper-right of the socket pad at
            // local (0, 1.0, 0.5) — the housing back sits flush against it.
            root.transform.localPosition = new Vector3(0.55f, 1.05f, 0.45f);
            root.transform.localRotation = Quaternion.identity;

            var housingMat = SolidMat("WiringPanel_Housing_Mat", lit,
                new Color(0.16f, 0.18f, 0.22f), 0.7f, 0.45f, Color.black, 0f);
            var recessMat = SolidMat("WiringPanel_Recess_Mat", lit,
                new Color(0.045f, 0.055f, 0.075f), 0.4f, 0.3f, Color.black, 0f);
            var lightMat = SolidMat("WiringPanel_Light_Mat", lit,
                new Color(0.3f, 0.85f, 1f), 0.1f, 0.6f, new Color(0.3f, 0.85f, 1f), 1.8f);

            // Wall housing with a darker open recess.
            Prim(root.transform, PrimitiveType.Cube, "Housing", housingMat,
                Vector3.zero, new Vector3(0.46f, 0.62f, 0.10f), Quaternion.identity);
            Prim(root.transform, PrimitiveType.Cube, "Recess", recessMat,
                new Vector3(0f, 0f, 0.026f), new Vector3(0.40f, 0.56f, 0.06f), Quaternion.identity);

            // Exposed wires running across the recess, top to bottom in the
            // same order the puzzle lists its left stubs.
            float[] wireY = { 0.195f, 0.065f, -0.065f, -0.195f };
            for (int i = 0; i < WireColors.Length; i++)
            {
                var wireMat = SolidMat("WiringPanel_Wire" + i + "_Mat", lit,
                    WireColors[i], 0.05f, 0.5f, WireColors[i], 1.4f);
                Prim(root.transform, PrimitiveType.Cylinder, "Wire_" + i, wireMat,
                    new Vector3(0f, wireY[i], 0.062f), new Vector3(0.026f, 0.19f, 0.026f),
                    Quaternion.Euler(0f, 0f, 90f));
            }

            // Cover swung open on its right hinge, away from the console
            // center so it never overlaps the socket or front detailing.
            var hinge = new GameObject("LidHinge");
            hinge.transform.SetParent(root.transform, false);
            hinge.transform.localPosition = new Vector3(0.23f, 0f, 0.05f);
            hinge.transform.localRotation = Quaternion.Euler(0f, 125f, 0f);
            Prim(hinge.transform, PrimitiveType.Cube, "Lid", housingMat,
                new Vector3(0.23f, 0f, 0.01f), new Vector3(0.46f, 0.62f, 0.018f), Quaternion.identity);

            // Status lamp, echoing the socket pad's cyan.
            Prim(root.transform, PrimitiveType.Sphere, "StatusLight", lightMat,
                new Vector3(0.17f, 0.26f, 0.055f), Vector3.one * 0.05f, Quaternion.identity);

            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[WiringProp] Installed " + PropName + " on LifeSupportStation.");
        }

        private static GameObject Prim(Transform parent, PrimitiveType type, string name,
            Material mat, Vector3 localPos, Vector3 localScale, Quaternion localRot)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col); // decoration only
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static Material SolidMat(string name, Shader shader, Color baseColor,
            float metallic, float smoothness, Color emissionColor, float emissionBoost)
        {
            string path = MatFolder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            if (emissionBoost > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor * emissionBoost);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }
    }
}
