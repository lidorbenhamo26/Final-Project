// DoorFixup.cs
// Makes every procedurally-built doorway passable and labeled, without
// rebuilding the whole environment. For each gated wall it:
//   1. Disables the gate's mesh colliders (the rounded frame + bottom sill,
//      whose non-convex MeshCollider snagged the astronaut's capsule).
//   2. Hides the closed door slab that covered ~half the opening.
//   3. Adds clean jamb + lintel BoxColliders, leaving a flush, threshold-free
//      opening (~2 m wide x 2.4 m tall) that the 1.2 m-wide player clears easily.
//   4. Stamps a color-matched station name sign above the doorway.
//
// Idempotent: re-running replaces the fix cleanly. Runs over whatever scene is
// loaded (menu: "Setup/3 - Fix Doors"), and is also called at the end of
// EnvironmentBuilder.BuildEnvironment so fresh builds come out correct.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FinalProject.EditorTools.Setup
{
    public static class DoorFixup
    {
        // ---- Opening geometry (local to a Wall_<side> group) ----------------
        private const float HalfOpening   = 1.0f;   // clear half-width  -> 2.0 m wide
        private const float OpeningHeight = 2.4f;   // clear height      -> head clearance
        private const float SlotHalf      = 2.0f;   // gate slot is 4 m wide
        private const float WallHeight    = 3f;
        private const float JambThickness = 0.35f;
        private const string FixPrefix    = "DoorFix_";
        private const string EnvironmentLayerName = "Environment";

        // =====================================================================
        [MenuItem("Setup/3 - Fix Doors (widen + unstick + label)")]
        public static void FixDoorsMenu()
        {
            int n = FixAllDoorsInLoadedScenes();
            Debug.Log($"[DoorFixup] Fixed {n} doorway(s).");
            if (n > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        /// <summary>Find and fix every gated doorway in the loaded scene(s).</summary>
        public static int FixAllDoorsInLoadedScenes()
        {
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);

            // Clear any objects from a previous fix pass anywhere in the scene,
            // so re-runs never accumulate duplicate colliders or signs.
            var stale = new List<GameObject>();
            foreach (var t in all)
                if (t != null && t.name.StartsWith(FixPrefix)) stale.Add(t.gameObject);
            foreach (var go in stale) if (go != null) Undo.DestroyObjectImmediate(go);

            // Collect only top-level gate roots. The gate prefab's LOD mesh
            // children also carry "_Gates" in their names, so skip any transform
            // whose parent is itself a gate.
            var gates = new List<Transform>();
            foreach (var t in all)
            {
                if (t == null) continue;
                if (t.name.IndexOf("_Gates", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (t.parent != null && t.parent.name.IndexOf("_Gates", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                gates.Add(t);
            }

            int count = 0;
            foreach (var gate in gates)
                if (gate != null && FixDoorSlot(gate)) count++;
            return count;
        }

        // =====================================================================
        private static bool FixDoorSlot(Transform gate)
        {
            Transform wallSide = gate.parent;
            if (wallSide == null) return false;

            // 1) Kill the gate's snag colliders (keep the visual frame).
            foreach (var mc in gate.GetComponentsInChildren<MeshCollider>(true))
            {
                Undo.RecordObject(mc, "Disable gate collider");
                mc.enabled = false;
            }

            // 2) Hide the closed door slab that blocked the opening.
            foreach (Transform sib in wallSide)
            {
                if (sib == gate) continue;
                bool isSlab = sib.name.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0
                              && sib.name.IndexOf("Gates", StringComparison.OrdinalIgnoreCase) < 0;
                if (!isSlab) continue;
                foreach (var mc in sib.GetComponentsInChildren<MeshCollider>(true)) mc.enabled = false;
                Undo.RecordObject(sib.gameObject, "Hide door slab");
                sib.gameObject.SetActive(false);
            }

            // 3) Remove any previous fix children so re-runs stay clean.
            var stale = new List<GameObject>();
            foreach (Transform c in wallSide)
                if (c.name.StartsWith(FixPrefix)) stale.Add(c.gameObject);
            foreach (var go in stale) Undo.DestroyObjectImmediate(go);

            int layer = gate.gameObject.layer;

            // 4) Clean jamb + lintel colliders. No bottom sill => flush floor.
            float jambWidth   = SlotHalf - HalfOpening;            // 1.0 m
            float jambCenterX = (HalfOpening + SlotHalf) * 0.5f;   // 1.5 m
            AddBox(wallSide, FixPrefix + "Jamb_L",
                   new Vector3(-jambCenterX, WallHeight * 0.5f, 0f),
                   new Vector3(jambWidth, WallHeight, JambThickness), layer);
            AddBox(wallSide, FixPrefix + "Jamb_R",
                   new Vector3(jambCenterX, WallHeight * 0.5f, 0f),
                   new Vector3(jambWidth, WallHeight, JambThickness), layer);
            AddBox(wallSide, FixPrefix + "Lintel",
                   new Vector3(0f, (OpeningHeight + WallHeight) * 0.5f, 0f),
                   new Vector3(HalfOpening * 2f, WallHeight - OpeningHeight, JambThickness), layer);

            // 5) Station sign above the doorway, color-matched.
            ResolveStation(gate, out string label, out Color color);
            if (!string.IsNullOrEmpty(label))
                BuildSign(wallSide, label, color, layer);

            return true;
        }

        private static void AddBox(Transform parent, string name, Vector3 localCenter, Vector3 size, int layer)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create door collider");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localRotation = Quaternion.identity;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            go.layer = layer;
        }

        // ---------------------------------------------------------------------
        // Map a gate to its station name + accent color. Room doors take the
        // room's identity; hub doors take the destination implied by their side
        // (Engine=N, Navigation=E, LifeSupport=S, Comms=W).
        // ---------------------------------------------------------------------
        private static void ResolveStation(Transform gate, out string label, out Color color)
        {
            label = null;
            color = Color.white;

            string room = null;
            for (Transform t = gate; t != null; t = t.parent)
            {
                if (t.name.StartsWith("Room_") || t.name.StartsWith("Hub")) { room = t.name; break; }
            }
            string side = gate.parent != null ? gate.parent.name : "";

            string key = room ?? "";
            if (key.StartsWith("Hub"))
            {
                if      (side.IndexOf("North", StringComparison.OrdinalIgnoreCase) >= 0) key = "Engine";
                else if (side.IndexOf("East",  StringComparison.OrdinalIgnoreCase) >= 0) key = "Navigation";
                else if (side.IndexOf("South", StringComparison.OrdinalIgnoreCase) >= 0) key = "LifeSupport";
                else if (side.IndexOf("West",  StringComparison.OrdinalIgnoreCase) >= 0) key = "Comms";
            }

            string k = key.ToLowerInvariant();
            if (k.Contains("engine"))           { label = "ENGINE";       color = new Color(1.0f, 0.18f, 0.18f); }
            else if (k.Contains("nav"))         { label = "NAVIGATION";   color = new Color(0.20f, 0.55f, 1.0f); }
            else if (k.Contains("life") || k.Contains("support"))
                                                { label = "LIFE SUPPORT"; color = new Color(0.20f, 1.0f, 0.35f); }
            else if (k.Contains("comm"))        { label = "COMMS";        color = new Color(1.0f, 0.95f, 0.20f); }
        }

        // ---------------------------------------------------------------------
        // ONE eye-level name placard per door, on the solid wall to one side of
        // the opening (never straddling the frame, which split the word into
        // fragments). Full station name on a single line. Kept off the walk path
        // and below the low ceiling, where the third-person camera (low and
        // roughly level) naturally looks.
        // ---------------------------------------------------------------------
        private static void BuildSign(Transform wallSide, string text, Color color, int layer)
        {
            // Perpendicular facing toward the station centre (the approach side).
            Vector3 doorCenter = wallSide.TransformPoint(new Vector3(0f, 1.6f, 0f));
            Vector3 toCenter = new Vector3(-doorCenter.x, 0f, -doorCenter.z);
            if (toCenter.sqrMagnitude < 1e-4f) toCenter = -wallSide.forward;
            toCenter.Normalize();

            // Single sign on the solid panel to the RIGHT of the opening. The
            // opening is x:-1..1 and the rounded frame is x:~1..2, so centring at
            // +2.9 (with a 1.7 m plate => x:2.05..3.75) keeps the whole word off
            // the doorway and frame, on the flat wall beside the door.
            BuildPlacard(wallSide, text, color, layer, toCenter, +2.9f);
        }

        private static void BuildPlacard(Transform wallSide, string text, Color color, int layer, Vector3 toCenter, float sideX)
        {
            const float eyeY = 1.6f;
            Vector3 anchor = wallSide.TransformPoint(new Vector3(sideX, eyeY, 0f));

            var root = new GameObject(FixPrefix + "Sign");
            Undo.RegisterCreatedObjectUndo(root, "Create door sign");
            root.transform.SetParent(wallSide, true);
            root.transform.position = anchor + toCenter * 0.40f; // floated clear of the frame's right edge
            root.transform.rotation = Quaternion.LookRotation(toCenter, Vector3.up);
            SetLayer(root, layer);

            // Dark unlit backing plate (a quad) behind the text so the colored
            // label stays readable against the accent-lit rooms.
            var plate = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plate.name = "Plate";
            var pcol = plate.GetComponent<Collider>();
            if (pcol != null) UnityEngine.Object.DestroyImmediate(pcol);
            plate.transform.SetParent(root.transform, false);
            plate.transform.localPosition = new Vector3(0f, 0f, -0.06f); // behind text (local +Z faces viewer)
            plate.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            plate.transform.localScale = new Vector3(1.7f, 0.5f, 1f); // fits the panel right of the door without spilling
            plate.GetComponent<MeshRenderer>().sharedMaterial = SignPlateMaterial();

            // 3D TextMeshPro label, facing the viewer (toward centre).
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = Vector3.zero;
            labelGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 4.5f;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 1f;
            tmp.fontSizeMax = 6f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.Lerp(color, Color.white, 0.55f); // brightened accent
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = new Color(0f, 0f, 0f, 1f);
            var trt = tmp.GetComponent<RectTransform>();
            trt.sizeDelta = new Vector2(1.7f, 0.5f);
            // Double-sided (so facing can't hide it) + an accent-colored glow so
            // the placard reads from across the room.
            if (tmp.fontSharedMaterial != null)
            {
                var matInstance = new Material(tmp.fontSharedMaterial);
                if (matInstance.HasProperty("_CullMode")) matInstance.SetFloat("_CullMode", 0f); // Cull Off
                if (matInstance.HasProperty("_Cull"))     matInstance.SetFloat("_Cull", 0f);
                matInstance.EnableKeyword("GLOW_ON");
                if (matInstance.HasProperty("_GlowColor")) matInstance.SetColor("_GlowColor", color);
                if (matInstance.HasProperty("_GlowPower")) matInstance.SetFloat("_GlowPower", 0.45f);
                if (matInstance.HasProperty("_GlowOuter")) matInstance.SetFloat("_GlowOuter", 0.4f);
                tmp.fontMaterial = matInstance;
            }
        }

        private static Material _signPlateMat;
        private static Material SignPlateMaterial()
        {
            if (_signPlateMat != null) return _signPlateMat;
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            var m = new Material(sh);
            var c = new Color(0.03f, 0.03f, 0.05f, 1f);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // double-sided
            _signPlateMat = m;
            return m;
        }

        private static void SetLayer(GameObject go, int layer)
        {
            if (layer < 0) return;
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
        }
    }
}
