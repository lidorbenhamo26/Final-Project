using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpaceStation.EditorSetup
{
    /// <summary>
    /// Two finishing passes for MainScene:
    ///  Setup/8 - Apply Wall Materials → re-skin Environment_Root with the real
    ///            Mat_ScifiWall / Mat_ScifiFloor textures (the pack ships HDRP-only
    ///            materials, so the imported prefabs render flat white in URP).
    ///  Setup/9 - Re-Place Props by Theme → distribute Meshy props per room theme:
    ///            companions in hub, engineering tools in Engine room, navigation
    ///            globes in Navigation room, helmet & crystal in Comms, oxygen
    ///            tank & coffee in LifeSupport.
    /// </summary>
    public static class SceneFinalPolish
    {
        private const string MatWall  = "Assets/Materials/Mat_ScifiWall.mat";
        private const string MatFloor = "Assets/Materials/Mat_ScifiFloor.mat";

        // Side rooms (per EnvironmentBuilder layout):
        //   Engine        N at (0, 0,  16)
        //   Navigation    E at (16, 0,  0)
        //   LifeSupport   S at (0, 0, -16)
        //   Comms         W at (-16, 0,  0)
        // Each side room is ~6x6m so props go in a 4x4 area around the center.

        private struct PropPlacement
        {
            public string Name;
            public Vector3 Position;
            public float YawDeg;
            public float ScaleMultiplier;
        }

        private static readonly PropPlacement[] Placements = new[]
        {
            // ── Hub (center) — friendly companions next to the player ──
            new PropPlacement { Name = "AlienBuddy",   Position = new Vector3(-2.0f, 0.0f, -1.5f), YawDeg =  35f, ScaleMultiplier = 1f },
            new PropPlacement { Name = "RobotPet",     Position = new Vector3( 2.5f, 0.0f, -1.0f), YawDeg = 200f, ScaleMultiplier = 1f },
            new PropPlacement { Name = "StarCrystal",  Position = new Vector3( 0.0f, 2.5f,  0.0f), YawDeg =   0f, ScaleMultiplier = 1f },

            // ── Engine room (N, +Z) — broken bot + tool caddy by the wall ──
            new PropPlacement { Name = "RobotHelper",  Position = new Vector3( 1.5f, 0.0f, 17.0f), YawDeg = 200f, ScaleMultiplier = 1f },
            new PropPlacement { Name = "ToolsCaddy",   Position = new Vector3(-1.8f, 0.6f, 17.5f), YawDeg =  60f, ScaleMultiplier = 1f },

            // ── Navigation room (E, +X) — star map and globe ──
            new PropPlacement { Name = "StarMapStand", Position = new Vector3(17.5f, 0.0f, -1.0f), YawDeg =  90f, ScaleMultiplier = 1f },
            new PropPlacement { Name = "PlanetGlobe",  Position = new Vector3(15.0f, 0.6f,  1.5f), YawDeg = 180f, ScaleMultiplier = 1f },

            // ── Comms room (W, -X) — helmet on display + nothing else ──
            new PropPlacement { Name = "HelmetStand",  Position = new Vector3(-17.0f, 0.6f, 0.0f), YawDeg = 270f, ScaleMultiplier = 1f },

            // ── LifeSupport room (S, -Z) — oxygen tank + coffee mug ──
            new PropPlacement { Name = "OxygenTank",   Position = new Vector3(-1.5f, 0.0f, -17.5f), YawDeg = 0f,   ScaleMultiplier = 1f },
            new PropPlacement { Name = "CoffeeMug",    Position = new Vector3( 1.5f, 0.6f, -16.0f), YawDeg = 145f, ScaleMultiplier = 1f },
        };

        [MenuItem("Setup/8 - Apply Wall Materials")]
        public static void ApplyWallMaterials()
        {
            var wallMat  = AssetDatabase.LoadAssetAtPath<Material>(MatWall);
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>(MatFloor);
            if (wallMat == null || floorMat == null)
            {
                Debug.LogError($"[SceneFinalPolish] Missing wall/floor materials. Run 'Tools/Mission Focus/Upgrade Rendering Quality' first.\n  wall={wallMat}\n  floor={floorMat}");
                return;
            }

            var envRoot = GameObject.Find("Environment_Root");
            if (envRoot == null) { Debug.LogError("[SceneFinalPolish] Environment_Root not found."); return; }

            int wallCount = 0, floorCount = 0, ceilingCount = 0, otherCount = 0;
            var renderers = envRoot.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            foreach (var r in renderers)
            {
                string n = r.gameObject.name.ToLowerInvariant();
                Material assign = null;

                if (n.Contains("floor"))         { assign = floorMat;  floorCount++; }
                else if (n.Contains("ceiling"))  { assign = wallMat;   ceilingCount++; }
                else if (n.Contains("wall") || n.Contains("trim") || n.Contains("door")) { assign = wallMat; wallCount++; }
                else                              { assign = wallMat;  otherCount++; }

                if (assign == null) continue;
                var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < arr.Length; i++) arr[i] = assign;
                r.sharedMaterials = arr;
            }
            Debug.Log($"[SceneFinalPolish] Applied wall mat to {wallCount} walls + {ceilingCount} ceilings + {otherCount} other; floor mat to {floorCount} floors. Total={renderers.Length}");
        }

        [MenuItem("Setup/9 - Re-Place Props by Theme")]
        public static void RePlaceProps()
        {
            int placed = 0, missing = 0;
            foreach (var p in Placements)
            {
                var go = GameObject.Find(p.Name);
                if (go == null) { Debug.LogWarning($"[SceneFinalPolish] '{p.Name}' not found."); missing++; continue; }

                // Preserve the existing scale (PropsFixup already corrected scale to ~real-world)
                go.transform.position = p.Position;
                go.transform.rotation = Quaternion.Euler(0f, p.YawDeg, 0f);
                if (Mathf.Abs(p.ScaleMultiplier - 1f) > 0.001f)
                    go.transform.localScale *= p.ScaleMultiplier;
                placed++;
            }
            Debug.Log($"[SceneFinalPolish] Re-placed {placed} props (missing {missing}).");
        }

        [MenuItem("Setup/10 - Restore Ambient (after UpgradeRenderingQuality)")]
        public static void RestoreAmbient()
        {
            // UpgradeRenderingQuality sets ambient to 0.015 (near-black).
            // For our scene we want enough light to see indoors without lights.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor    = new Color(0.30f, 0.36f, 0.50f);
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.26f, 0.35f);
            RenderSettings.ambientGroundColor  = new Color(0.10f, 0.11f, 0.15f);
            RenderSettings.ambientIntensity   = 0.6f;
            RenderSettings.reflectionIntensity = 0.5f;
            Debug.Log("[SceneFinalPolish] Ambient restored to moderate trilight.");
        }
    }
}
