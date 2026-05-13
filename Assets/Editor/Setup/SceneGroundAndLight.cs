using UnityEditor;
using UnityEngine;

namespace SpaceStation.EditorSetup
{
    /// <summary>
    /// Two corrective passes after scaling Meshy props:
    ///  Setup/11 - Ground Props → drop every prop's bounds.min.y to the floor (Y=0).
    ///             Meshy FBX origin is sometimes the center of the mesh; after we
    ///             scaled by 30-90x the mesh now sits half-buried or floating in air.
    ///             This re-anchors them so they read as standing on the floor.
    ///  Setup/12 - Boost Room Lighting → adds a per-room overhead "ceiling lamp"
    ///             white point light + brightens the existing neon spots so each
    ///             side room is properly lit instead of just the colored accent.
    /// </summary>
    public static class SceneGroundAndLight
    {
        // Props that should sit on the floor (with optional Y offset for "floating" variants)
        private struct GroundDef
        {
            public string Name;
            public float ExtraY;   // additional offset above the floor (e.g. for floating star)
            public bool Float;     // skip grounding (these are intentionally airborne)
        }

        private static readonly GroundDef[] Defs = new[]
        {
            // Hub
            new GroundDef { Name = "AlienBuddy",   ExtraY = 0f,    Float = false },
            new GroundDef { Name = "RobotPet",     ExtraY = 0f,    Float = false },
            new GroundDef { Name = "StarCrystal",  ExtraY = 0f,    Float = true  }, // intentionally floating

            // Engine
            new GroundDef { Name = "RobotHelper",  ExtraY = 0f,    Float = false },
            new GroundDef { Name = "ToolsCaddy",   ExtraY = 0.6f,  Float = false }, // sits on a small surface

            // Navigation
            new GroundDef { Name = "StarMapStand", ExtraY = 0f,    Float = false },
            new GroundDef { Name = "PlanetGlobe",  ExtraY = 0.6f,  Float = false }, // on a desk/stand height

            // Comms
            new GroundDef { Name = "HelmetStand",  ExtraY = 0.6f,  Float = false },

            // LifeSupport
            new GroundDef { Name = "OxygenTank",   ExtraY = 0f,    Float = false },
            new GroundDef { Name = "CoffeeMug",    ExtraY = 0.7f,  Float = false }, // on a counter
        };

        [MenuItem("Setup/11 - Ground Props (drop to floor)")]
        public static void GroundProps()
        {
            int grounded = 0, missing = 0, floated = 0;
            foreach (var d in Defs)
            {
                var go = GameObject.Find(d.Name);
                if (go == null) { Debug.LogWarning($"[Ground] '{d.Name}' not found."); missing++; continue; }
                if (d.Float) { floated++; continue; }

                var rends = go.GetComponentsInChildren<Renderer>(includeInactive: true);
                if (rends.Length == 0) { Debug.LogWarning($"[Ground] '{d.Name}' has no renderers."); missing++; continue; }

                // Aggregate world-space bounds
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

                // Shift so bounds.min.y == ExtraY  (i.e. the bottom of the model rests on Y=ExtraY)
                float currentBottom = b.min.y;
                float targetBottom = d.ExtraY;
                float deltaY = targetBottom - currentBottom;
                go.transform.position += new Vector3(0f, deltaY, 0f);
                grounded++;
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[Ground] Re-anchored {grounded} props to the floor (skipped {floated} intentionally-floating, missing {missing}).");
        }

        [MenuItem("Setup/12 - Boost Room Lighting")]
        public static void BoostRoomLighting()
        {
            // Per-room ceiling lamps (white fill light at room center, ~3m up)
            //   Engine        N at (0, 0,  16)
            //   Navigation    E at (16, 0,  0)
            //   LifeSupport   S at (0, 0, -16)
            //   Comms         W at (-16, 0,  0)
            //   Hub           center
            var rooms = new (string, Vector3, Color)[]
            {
                ("Lamp_Hub",         new Vector3(  0f, 2.6f,   0f), new Color(1.0f, 1.0f, 1.0f)),
                ("Lamp_Engine",      new Vector3(  0f, 2.6f,  16f), new Color(1.0f, 0.85f, 0.7f)), // warm
                ("Lamp_Navigation",  new Vector3( 16f, 2.6f,   0f), new Color(0.85f, 0.95f, 1.0f)), // cool
                ("Lamp_LifeSupport", new Vector3(  0f, 2.6f, -16f), new Color(0.85f, 1.0f, 0.85f)), // green-ish white
                ("Lamp_Comms",       new Vector3(-16f, 2.6f,   0f), new Color(1.0f, 0.95f, 0.7f)), // warm-yellow
            };

            int created = 0, replaced = 0;
            foreach (var (name, pos, color) in rooms)
            {
                var existing = GameObject.Find(name);
                if (existing != null) { Object.DestroyImmediate(existing); replaced++; }

                var go = new GameObject(name);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // point downward
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = color;
                l.intensity = 6f;
                l.range = 9f;
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.4f;
                created++;
            }

            // Slightly raise reflection intensity so metallic floor catches the new lights
            RenderSettings.reflectionIntensity = 0.7f;

            // Boost existing point/spot lights modestly so neon accents still read
            int boosted = 0;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (light.gameObject.name.StartsWith("Lamp_")) continue; // skip the ones we just made
                if (light.type == LightType.Point || light.type == LightType.Spot)
                {
                    light.intensity = Mathf.Clamp(light.intensity + 0.5f, 2f, 8f);
                    light.range = Mathf.Max(light.range, 8f);
                    boosted++;
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[Lights] Created {created} ceiling lamps (replaced {replaced}); boosted {boosted} existing lights.");
        }
    }
}
