using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpaceStation.EditorSetup
{
    /// <summary>
    /// Places all 13 Meshy-AI generated props into the active scene.
    /// 10 interior props + 3 celestial bodies (Earth / Sun / Moon).
    /// Idempotent: existing MeshyProps_Root and Skybox_Bodies_Root are removed and rebuilt.
    /// </summary>
    public static class PropsPlacer
    {
        private const string PropsRootName = "MeshyProps_Root";
        private const string SkyRootName = "SkyBodies_Root";
        private const string ModelBasePath = "Assets/Models/MeshyProps";

        private struct PropDef
        {
            public string Name;
            public string FbxRelativePath;     // relative to ModelBasePath
            public Vector3 Position;
            public Vector3 EulerRotation;       // degrees
            public float UniformScale;
            public bool RandomYRotation;
            public Color? EmissiveTint;        // when set, applies emission to all child renderers
            public float EmissiveIntensity;
        }

        // ----- Interior props (parented under MeshyProps_Root) -----
        // Logical room assignment:
        //   Engine_N    (+Z, repairs):   ToolsCaddy, RobotHelper
        //   Navigation_E (+X, mapping):  StarMapStand, PlanetGlobe
        //   LifeSupport_S (-Z, crew):    OxygenTank, HelmetStand, CoffeeMug
        //   Comms_W     (-X, signals):   AlienBuddy, StarCrystal
        //   Hub         (center):        RobotPet
        private static readonly PropDef[] InteriorProps = new[]
        {
            // LifeSupport_S (-Z): oxygen storage + EVA helmet + crew coffee.
            new PropDef { Name = "OxygenTank",     FbxRelativePath = "OxygenTank/OxygenTank.fbx",       Position = new Vector3(-2.0f, 0.0f, -17.5f), UniformScale = 1.0f, RandomYRotation = true  },
            new PropDef { Name = "HelmetStand",    FbxRelativePath = "HelmetStand/HelmetStand.fbx",     Position = new Vector3( 2.0f, 1.0f, -17.5f), UniformScale = 0.8f, RandomYRotation = true  },
            new PropDef { Name = "CoffeeMug",      FbxRelativePath = "CoffeeMug/CoffeeMug.fbx",         Position = new Vector3( 1.0f, 0.9f, -15.5f), UniformScale = 0.6f, RandomYRotation = true  },

            // Engine_N (+Z): tools + repair robot.
            new PropDef { Name = "ToolsCaddy",     FbxRelativePath = "ToolsCaddy/ToolsCaddy.fbx",       Position = new Vector3(-2.0f, 0.0f,  17.5f), UniformScale = 1.0f, RandomYRotation = true  },
            new PropDef { Name = "RobotHelper",    FbxRelativePath = "RobotHelper/RobotHelper.fbx",     Position = new Vector3( 2.0f, 0.0f,  17.5f), UniformScale = 1.0f, RandomYRotation = false, EulerRotation = new Vector3(0, 200, 0) },

            // Navigation_E (+X): star map + planetary globe.
            new PropDef { Name = "StarMapStand",   FbxRelativePath = "StarMapStand/StarMapStand.fbx",   Position = new Vector3(17.5f, 0.0f,  2.0f), UniformScale = 1.0f, RandomYRotation = true,  EmissiveTint = new Color(0.3f, 0.7f, 1.0f), EmissiveIntensity = 1.5f },
            new PropDef { Name = "PlanetGlobe",    FbxRelativePath = "PlanetGlobe/PlanetGlobe.fbx",     Position = new Vector3(15.0f, 1.0f, -2.0f), UniformScale = 0.6f, RandomYRotation = true  },

            // Comms_W (-X): signal crystal (alien moved to Hub as companion).
            new PropDef { Name = "StarCrystal",    FbxRelativePath = "StarCrystal/StarCrystal.fbx",     Position = new Vector3(-15.0f, 1.5f,  2.0f), UniformScale = 0.5f, RandomYRotation = true,  EmissiveTint = new Color(1.0f, 0.9f, 0.4f), EmissiveIntensity = 2.5f },

            // Hub_Central: friendly mascots that share the entry space.
            // Y offsets compensate for Meshy FBX center-pivot at scale ~42 (PropsFixup ×60).
            // HoverBob amplitude is ±0.12m, so Y=0.6 keeps the robot's feet above floor even at the dip.
            new PropDef { Name = "AlienBuddy",     FbxRelativePath = "AlienBuddy/AlienBuddy.fbx",       Position = new Vector3(-1.5f, 0.0f,  0.0f), UniformScale = 1.0f, RandomYRotation = false, EulerRotation = new Vector3(0,  35, 0) },
            new PropDef { Name = "RobotPet",       FbxRelativePath = "RobotPet/RobotPet.fbx",           Position = new Vector3( 1.5f, 0.6f,  0.0f), UniformScale = 0.7f, RandomYRotation = false, EulerRotation = new Vector3(0, 180, 0) },
        };

        // ----- Celestial props (parented under SkyBodies_Root, large scale, far away) -----
        private static readonly PropDef[] CelestialProps = new[]
        {
            // Earth — large, visible from windows on the East / window panels
            // Note: Meshy FBX exports come at micro scale (~2cm), so we use scale 1500 → ~30m sphere
            new PropDef { Name = "Earth",          FbxRelativePath = "Earth/Earth.fbx",                 Position = new Vector3( 80f, 40f,  60f), UniformScale = 1500f, RandomYRotation = false, EulerRotation = new Vector3(20, 30, 0) },
            // Sun — distant, very bright. Place far behind the station so windows can see it
            new PropDef { Name = "Sun",            FbxRelativePath = "Sun/Sun.fbx",                     Position = new Vector3(-120f, 60f, 100f), UniformScale = 2500f, RandomYRotation = false, EmissiveTint = new Color(1.0f, 0.95f, 0.6f), EmissiveIntensity = 5.0f },
            // Moon — opposite side
            new PropDef { Name = "Moon",           FbxRelativePath = "Moon/Moon.fbx",                   Position = new Vector3( 60f, 30f, -90f), UniformScale = 800f,  RandomYRotation = true  },
        };

        [MenuItem("Setup/3 - Place Meshy Props")]
        public static void PlaceAllProps()
        {
            var scene = SceneManager.GetActiveScene();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup: Place Meshy Props");

            try
            {
                // Idempotent: tear down old roots
                TearDownRoot(PropsRootName);
                TearDownRoot(SkyRootName);

                // Build interior root
                GameObject propsRoot = new GameObject(PropsRootName);
                Undo.RegisterCreatedObjectUndo(propsRoot, "Create MeshyProps_Root");

                int placedInterior = 0, missingInterior = 0;
                foreach (var def in InteriorProps)
                {
                    if (TryPlaceProp(def, propsRoot.transform))
                        placedInterior++;
                    else
                        missingInterior++;
                }

                // Build celestial root (no collider needed; these are visual only)
                GameObject skyRoot = new GameObject(SkyRootName);
                Undo.RegisterCreatedObjectUndo(skyRoot, "Create SkyBodies_Root");

                int placedSky = 0, missingSky = 0;
                foreach (var def in CelestialProps)
                {
                    if (TryPlaceProp(def, skyRoot.transform, isCelestial: true))
                        placedSky++;
                    else
                        missingSky++;
                }

                // Add a directional sun-light pointing FROM the Sun model TOWARDS origin
                AddSunDirectionalLight(skyRoot.transform);

                EditorSceneManager.MarkSceneDirty(scene);

                Debug.Log($"[PropsPlacer] Placed {placedInterior}/{InteriorProps.Length} interior props " +
                          $"(missing {missingInterior}). Placed {placedSky}/{CelestialProps.Length} celestial bodies " +
                          $"(missing {missingSky}). Done.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PropsPlacer] FAILED: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private static void TearDownRoot(string rootName)
        {
            var existing = GameObject.Find(rootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }
        }

        private static bool TryPlaceProp(PropDef def, Transform parent, bool isCelestial = false)
        {
            string assetPath = $"{ModelBasePath}/{def.FbxRelativePath}";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[PropsPlacer] FBX not found at '{assetPath}' — skipping '{def.Name}'.");
                return false;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (instance == null)
            {
                // Fallback for non-prefabable assets
                instance = (GameObject)Object.Instantiate(prefab, parent);
            }

            instance.name = def.Name;
            instance.transform.localPosition = def.Position;

            float yRot = def.EulerRotation.y;
            if (def.RandomYRotation)
                yRot = Random.Range(0f, 360f);
            instance.transform.localEulerAngles = new Vector3(def.EulerRotation.x, yRot, def.EulerRotation.z);

            float scale = def.UniformScale > 0f ? def.UniformScale : 1f;
            instance.transform.localScale = Vector3.one * scale;

            // Apply emission tint if requested
            if (def.EmissiveTint.HasValue)
            {
                ApplyEmission(instance, def.EmissiveTint.Value, def.EmissiveIntensity);
            }

            // Disable colliders on celestial bodies — they are far-away skybox decoration
            if (isCelestial)
            {
                foreach (var col in instance.GetComponentsInChildren<Collider>())
                {
                    col.enabled = false;
                }
                // Mark as static for batching
                foreach (var t in instance.GetComponentsInChildren<Transform>())
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                        StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                }
            }

            Undo.RegisterCreatedObjectUndo(instance, $"Place {def.Name}");
            return true;
        }

        private static void ApplyEmission(GameObject root, Color color, float intensity)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            foreach (var rend in renderers)
            {
                // Make a per-instance material clone so we don't mutate the imported asset
                var mats = rend.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var m = new Material(mats[i]);
                    if (m.HasProperty("_EmissionColor"))
                    {
                        m.EnableKeyword("_EMISSION");
                        m.SetColor("_EmissionColor", color * Mathf.Max(0.001f, intensity));
                        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }
                    mats[i] = m;
                }
                rend.sharedMaterials = mats;
            }
        }

        private static void AddSunDirectionalLight(Transform skyRoot)
        {
            var sunLightGO = new GameObject("SunDirectional");
            sunLightGO.transform.SetParent(skyRoot, worldPositionStays: false);
            sunLightGO.transform.localPosition = new Vector3(-120f, 80f, 100f);

            // Aim from sun position toward origin
            sunLightGO.transform.LookAt(Vector3.zero);

            var light = sunLightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.96f, 0.85f);
            light.intensity = 0.8f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.6f;

            Undo.RegisterCreatedObjectUndo(sunLightGO, "Create SunDirectional");
        }
    }
}
