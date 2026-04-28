// EnvironmentBuilder.cs
// Programmatically constructs the interior of a sci-fi space station from
// the modular MinimalScifiPack (Dzeruza). Authored for Unity 6000.4.3f1 + URP 17.4.
//
// Menu path:  Setup/1 - Build Environment
//
// Layout (top-down, +Z = North):
//
//                       [ Engine (N, red) ]
//                              |
//                           corridor
//                              |
//   [ Comms (W, yellow) ]--corridor--[ HUB ]--corridor--[ Navigation (E, blue) ]
//                              |
//                           corridor
//                              |
//                     [ Life Support (S, green) ]
//
// All prefabs are loaded via AssetDatabase using real paths discovered in
// Assets/Dzeruza/MinimalScifiPack/3DModels/. If a prefab is missing, we fall
// back to a simple primitive so the build never aborts.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FinalProject.EditorTools.Setup
{
    public static class EnvironmentBuilder
    {
        // ---- Tunables --------------------------------------------------------
        private const string RootName               = "Environment_Root";
        private const float  HubSize                = 12f;   // 12 x 12 m
        private const float  SideRoomSize           = 6f;    // 6 x 6 m
        private const float  RoomDistance           = 16f;   // hub centre -> side room centre
        private const float  CorridorWidth          = 4f;
        private const float  WallHeight             = 3f;    // pack walls are 3 m tall
        private const float  TileSize               = 8f;    // SM_Trim_Floor_8x8M
        private const string EnvironmentLayerName   = "Environment";

        // ---- Prefab paths (verified to exist in MinimalScifiPack) -----------
        private const string PrefabsRoot = "Assets/Dzeruza/MinimalScifiPack/3DModels";

        private static readonly string FloorTile_8x8         = PrefabsRoot + "/Floor/Prefabs/SM_Trim_Floor_8x8M_A1.prefab";
        private static readonly string FloorTile_8x8_B       = PrefabsRoot + "/Floor/Prefabs/SM_Trim_Floor_8x8M_A2.prefab";
        private static readonly string FloorEdge_1x4         = PrefabsRoot + "/Floor/Prefabs/SM_Floor_Side_1x4M_A.prefab";
        private static readonly string FloorEdge_1x2         = PrefabsRoot + "/Floor/Prefabs/SM_Floor_Side_1x2M_A.prefab";

        private static readonly string CeilingPlate_4x4      = PrefabsRoot + "/Ceilings/Prefabs/SM_Ceiling_Plate_4x4M.prefab";
        private static readonly string CeilingPlate_2x2      = PrefabsRoot + "/Ceilings/Prefabs/SM_Ceiling_Plate_2x2M.prefab";
        private static readonly string CeilingSide_1x4       = PrefabsRoot + "/Ceilings/Prefabs/SM_Ceiling_Side_1x4M_A.prefab";
        private static readonly string CeilingSide_1x2       = PrefabsRoot + "/Ceilings/Prefabs/SM_Ceiling_Side_1x2M_A.prefab";

        private static readonly string Wall_3x4              = PrefabsRoot + "/Walls/Prefabs/SM_Trim_Wall_3x4M_B1.prefab";
        private static readonly string Wall_3x4_Gates        = PrefabsRoot + "/Walls/Prefabs/SM_Trim_Wall_3x4M_B1_Gates.prefab";
        private static readonly string Wall_3x2              = PrefabsRoot + "/Walls/Prefabs/SM_Trim_Wall_3x2M_B1.prefab";
        private static readonly string Wall_3x2_B            = PrefabsRoot + "/Walls/Prefabs/SM_Trim_Wall_3x2M_B2.prefab";
        private static readonly string Wall_3x1              = PrefabsRoot + "/Walls/Prefabs/SM_Trim_Wall_3x1M_B1.prefab";
        private static readonly string Wall_CornerOuter      = PrefabsRoot + "/Walls/Prefabs/SM_Trim_Wall_CO_B1.prefab";
        private static readonly string Wall_CornerInner      = PrefabsRoot + "/Walls/Prefabs/SM_Trim_Wall_CI_B1.prefab";
        private static readonly string Door_3x4              = PrefabsRoot + "/Walls/Prefabs/SM_Trim_Door_3x4M_B1.prefab";
        private static readonly string Door_3x2              = PrefabsRoot + "/Walls/Prefabs/SM_Trim_Door_3x2M_B1.prefab";

        private static readonly string Prop_Box              = PrefabsRoot + "/Props/Prefabs/SM_Box1.prefab";
        private static readonly string Prop_Canister         = PrefabsRoot + "/Props/Prefabs/SM_Canister1.prefab";
        private static readonly string Prop_Container1       = PrefabsRoot + "/Props/Prefabs/SM_Trim_L_Container1.prefab";
        private static readonly string Prop_Container2       = PrefabsRoot + "/Props/Prefabs/SM_Trim_L_Container2.prefab";

        // Cache for fall-back complaints so we only log once per asset.
        private static readonly HashSet<string> _missingLogged = new HashSet<string>();

        // Keep track of what we created so we can place lights / probes later.
        private struct RoomInfo
        {
            public string  Name;
            public Vector3 Center;
            public float   Width;
            public float   Depth;
            public Color   AccentColor;
        }

        // =====================================================================
        // Menu entry
        // =====================================================================
        [MenuItem("Setup/1 - Build Environment")]
        public static void BuildEnvironment()
        {
            try
            {
                // Idempotent: clear out any previous build first.
                var existing = GameObject.Find(RootName);
                if (existing != null)
                {
                    Undo.DestroyObjectImmediate(existing);
                }

                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Build Environment");

                var root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Create Environment_Root");
                ApplyEnvironmentLayer(root);

                // Ambient star-light directional.
                CreateDirectionalLight(root.transform);

                // ---- Hub ----
                var hub = BuildRoom(
                    root.transform,
                    "Hub_Central",
                    Vector3.zero,
                    HubSize,
                    HubSize,
                    new Color(0.6f, 0.6f, 0.7f),
                    pointLightIntensity: 4f,
                    openSides: Direction.North | Direction.East | Direction.South | Direction.West,
                    addWindows: false);

                // ---- Side rooms ----
                var sideRooms = new List<RoomInfo>
                {
                    BuildRoom(root.transform, "Room_Engine_N",       new Vector3(0,  0,  RoomDistance), SideRoomSize, SideRoomSize,
                              new Color(1.0f, 0.18f, 0.18f), pointLightIntensity: 6f, openSides: Direction.South, addWindows: true),
                    BuildRoom(root.transform, "Room_Navigation_E",   new Vector3( RoomDistance, 0, 0), SideRoomSize, SideRoomSize,
                              new Color(0.20f, 0.55f, 1.0f), pointLightIntensity: 6f, openSides: Direction.West,  addWindows: true),
                    BuildRoom(root.transform, "Room_LifeSupport_S",  new Vector3(0,  0, -RoomDistance), SideRoomSize, SideRoomSize,
                              new Color(0.20f, 1.0f, 0.35f), pointLightIntensity: 6f, openSides: Direction.North, addWindows: true),
                    BuildRoom(root.transform, "Room_Comms_W",        new Vector3(-RoomDistance, 0, 0), SideRoomSize, SideRoomSize,
                              new Color(1.0f, 0.95f, 0.20f), pointLightIntensity: 6f, openSides: Direction.East,  addWindows: true),
                };

                // ---- Corridors hub <-> side rooms ----
                BuildCorridor(root.transform, "Corridor_N", hub.Center, sideRooms[0].Center, CorridorWidth);
                BuildCorridor(root.transform, "Corridor_E", hub.Center, sideRooms[1].Center, CorridorWidth);
                BuildCorridor(root.transform, "Corridor_S", hub.Center, sideRooms[2].Center, CorridorWidth);
                BuildCorridor(root.transform, "Corridor_W", hub.Center, sideRooms[3].Center, CorridorWidth);

                // ---- Reflection probes: one per room ----
                CreateReflectionProbe(root.transform, "ReflectionProbe_Hub", hub.Center, HubSize, HubSize);
                foreach (var r in sideRooms)
                {
                    CreateReflectionProbe(root.transform, "ReflectionProbe_" + r.Name, r.Center, r.Width, r.Depth);
                }

                // ---- Skybox ----
                ApplySkybox();

                // ---- Mark scene dirty + finalise undo ----
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

                Debug.Log("[EnvironmentBuilder] Build complete. " +
                          $"Hub + {sideRooms.Count} side rooms + 4 corridors created under '{RootName}'.");
            }
            catch (Exception e)
            {
                Debug.LogError("[EnvironmentBuilder] Build failed: " + e);
            }
        }

        // =====================================================================
        // Room building
        // =====================================================================
        [Flags]
        private enum Direction
        {
            None  = 0,
            North = 1 << 0,  // +Z
            East  = 1 << 1,  // +X
            South = 1 << 2,  // -Z
            West  = 1 << 3,  // -X
        }

        /// <summary>
        /// Builds floor + ceiling + 4 walls. Walls on `openSides` get a doorway
        /// instead of a solid wall section. Returns a RoomInfo describing it.
        /// </summary>
        private static RoomInfo BuildRoom(
            Transform parent,
            string    name,
            Vector3   center,
            float     width,   // along X
            float     depth,   // along Z
            Color     accentColor,
            float     pointLightIntensity,
            Direction openSides,
            bool      addWindows)
        {
            var room = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(room, "Create Room");
            room.transform.SetParent(parent, false);
            room.transform.position = center;
            ApplyEnvironmentLayer(room);

            BuildFloor(room.transform, width, depth);
            BuildCeiling(room.transform, width, depth);
            BuildPerimeterWalls(room.transform, width, depth, openSides, addWindows);

            // Neon point light at room centre, slightly below ceiling.
            var lightGo = new GameObject("PointLight_Accent");
            lightGo.transform.SetParent(room.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, WallHeight - 0.4f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type      = LightType.Point;
            light.color     = accentColor;
            light.intensity = pointLightIntensity;
            light.range     = Mathf.Max(width, depth) * 1.2f;
            light.shadows   = LightShadows.Soft;
            ApplyEnvironmentLayer(lightGo);

            // Decorative props near room corners (skip hub - keep it open).
            if (name != "Hub_Central")
            {
                ScatterProps(room.transform, width, depth, accentColor);
            }
            else
            {
                ScatterHubProps(room.transform, width, depth);
            }

            return new RoomInfo
            {
                Name        = name,
                Center      = center,
                Width       = width,
                Depth       = depth,
                AccentColor = accentColor,
            };
        }

        // ---------------------------------------------------------------------
        // Floor: tile out an N x N grid using the 8x8 trim floor; if the room
        // is smaller than 8 m, we still place one tile and clip via a
        // BoxCollider that matches the room footprint.
        // ---------------------------------------------------------------------
        private static void BuildFloor(Transform parent, float width, float depth)
        {
            var floorGroup = new GameObject("Floor");
            floorGroup.transform.SetParent(parent, false);
            ApplyEnvironmentLayer(floorGroup);

            // Number of 8x8 tiles needed (rounded up).
            int tilesX = Mathf.Max(1, Mathf.CeilToInt(width / TileSize));
            int tilesZ = Mathf.Max(1, Mathf.CeilToInt(depth / TileSize));

            for (int ix = 0; ix < tilesX; ix++)
            {
                for (int iz = 0; iz < tilesZ; iz++)
                {
                    string path = ((ix + iz) % 2 == 0) ? FloorTile_8x8 : FloorTile_8x8_B;

                    Vector3 localPos = new Vector3(
                        (ix - (tilesX - 1) * 0.5f) * TileSize,
                        0f,
                        (iz - (tilesZ - 1) * 0.5f) * TileSize);

                    var go = InstantiatePrefabOrFallback(path, floorGroup.transform, localPos, Quaternion.identity,
                                                         FallbackKind.FloorTile, new Vector3(TileSize, 0.1f, TileSize));
                    if (go != null) go.name = $"FloorTile_{ix}_{iz}";
                }
            }

            // Backup invisible collider that exactly matches the room footprint
            // (some pack tiles do not include large convex floor colliders).
            var col = new GameObject("FloorCollider");
            col.transform.SetParent(floorGroup.transform, false);
            col.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            var bc = col.AddComponent<BoxCollider>();
            bc.size = new Vector3(width, 0.1f, depth);
            ApplyEnvironmentLayer(col);
        }

        // ---------------------------------------------------------------------
        // Ceiling: same tiling strategy, but flipped so the visible side faces
        // down. The pack ceiling plates are authored as flat planes; placing
        // them with a 180-degree X rotation keeps trim details facing the room.
        // ---------------------------------------------------------------------
        private static void BuildCeiling(Transform parent, float width, float depth)
        {
            var group = new GameObject("Ceiling");
            group.transform.SetParent(parent, false);
            group.transform.localPosition = new Vector3(0f, WallHeight, 0f);
            ApplyEnvironmentLayer(group);

            // Use 4x4 plates: count along each axis.
            int tilesX = Mathf.Max(1, Mathf.CeilToInt(width / 4f));
            int tilesZ = Mathf.Max(1, Mathf.CeilToInt(depth / 4f));

            for (int ix = 0; ix < tilesX; ix++)
            {
                for (int iz = 0; iz < tilesZ; iz++)
                {
                    Vector3 localPos = new Vector3(
                        (ix - (tilesX - 1) * 0.5f) * 4f,
                        0f,
                        (iz - (tilesZ - 1) * 0.5f) * 4f);
                    var go = InstantiatePrefabOrFallback(
                        CeilingPlate_4x4, group.transform, localPos,
                        Quaternion.Euler(180f, 0f, 0f),
                        FallbackKind.Ceiling,
                        new Vector3(4f, 0.1f, 4f));
                    if (go != null) go.name = $"CeilingPlate_{ix}_{iz}";
                }
            }
        }

        // ---------------------------------------------------------------------
        // Perimeter walls. Each side runs along an axis; we stamp 4-meter wide
        // wall pieces, swapping in a Gates piece for the doorway side.
        //
        // Local axes for sides (looking down at room from above):
        //   North side  (+Z): walls run along  X, faces  +Z, rotation Y =   0
        //   East  side  (+X): walls run along  Z, faces  +X, rotation Y =  90
        //   South side  (-Z): walls run along  X, faces  -Z, rotation Y = 180
        //   West  side  (-X): walls run along  Z, faces  -X, rotation Y = 270
        // ---------------------------------------------------------------------
        private static void BuildPerimeterWalls(
            Transform parent,
            float     width,
            float     depth,
            Direction openSides,
            bool      addWindows)
        {
            var group = new GameObject("Walls");
            group.transform.SetParent(parent, false);
            ApplyEnvironmentLayer(group);

            BuildWallSide(group.transform, "Wall_North", new Vector3(0f, 0f,  depth * 0.5f), 0f,   width, (openSides & Direction.North) != 0, addWindows);
            BuildWallSide(group.transform, "Wall_East",  new Vector3( width * 0.5f, 0f, 0f), 90f,  depth, (openSides & Direction.East)  != 0, addWindows);
            BuildWallSide(group.transform, "Wall_South", new Vector3(0f, 0f, -depth * 0.5f), 180f, width, (openSides & Direction.South) != 0, addWindows);
            BuildWallSide(group.transform, "Wall_West",  new Vector3(-width * 0.5f, 0f, 0f), 270f, depth, (openSides & Direction.West)  != 0, addWindows);
        }

        private static void BuildWallSide(
            Transform parent,
            string    name,
            Vector3   sideCenterLocal,
            float     yawDegrees,
            float     length,
            bool      hasDoor,
            bool      placeWindow)
        {
            var sideGo = new GameObject(name);
            sideGo.transform.SetParent(parent, false);
            sideGo.transform.localPosition = sideCenterLocal;
            sideGo.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            ApplyEnvironmentLayer(sideGo);

            // Approach: split the side into 4-meter slots; the centre slot becomes
            // the door (if hasDoor). Anything left over at the ends is filled with
            // narrower (2 m or 1 m) wall pieces and corner caps.
            int slots4m       = Mathf.FloorToInt(length / 4f);
            float remainder   = length - slots4m * 4f;            // 0..4
            int doorSlotIndex = hasDoor ? slots4m / 2 : -1;        // centre of the side

            float xCursor = -length * 0.5f + (remainder * 0.5f);   // start so wall is centred
            // The remainder/2 offset spreads any leftover wall length symmetrically.

            for (int i = 0; i < slots4m; i++)
            {
                float xCenter = xCursor + 2f;     // centre of this 4m slot
                Vector3 lp = new Vector3(xCenter, 0f, 0f);

                string prefab;
                if (i == doorSlotIndex)
                {
                    // Doorway: use the gated wall piece (has cutout for door),
                    // and stamp a door prop in front of it.
                    prefab = Wall_3x4_Gates;
                    InstantiatePrefabOrFallback(prefab, sideGo.transform, lp, Quaternion.identity,
                                                FallbackKind.Wall, new Vector3(4f, WallHeight, 0.2f));
                    InstantiatePrefabOrFallback(Door_3x4, sideGo.transform, lp, Quaternion.identity,
                                                FallbackKind.Door, new Vector3(4f, WallHeight, 0.3f));
                }
                else if (placeWindow && (i == 0 || i == slots4m - 1) && slots4m >= 3)
                {
                    // Use the alternate wall variant (B2 has a window-like cutout pattern)
                    // for the very first and last slot of outer walls only.
                    prefab = Wall_3x4;
                    InstantiatePrefabOrFallback(prefab, sideGo.transform, lp, Quaternion.identity,
                                                FallbackKind.Wall, new Vector3(4f, WallHeight, 0.2f));
                    // A small lit emissive trim "window" frame in front of the wall.
                    StampWindowAccent(sideGo.transform, lp);
                }
                else
                {
                    prefab = Wall_3x4;
                    InstantiatePrefabOrFallback(prefab, sideGo.transform, lp, Quaternion.identity,
                                                FallbackKind.Wall, new Vector3(4f, WallHeight, 0.2f));
                }

                xCursor += 4f;
            }

            // Trailing remainder filler (split equally on each end).
            if (remainder > 0.01f)
            {
                float halfRem = remainder * 0.5f;
                float leftCenter  = -length * 0.5f + halfRem * 0.5f;
                float rightCenter =  length * 0.5f - halfRem * 0.5f;

                StampRemainderWall(sideGo.transform, halfRem, new Vector3(leftCenter,  0f, 0f));
                StampRemainderWall(sideGo.transform, halfRem, new Vector3(rightCenter, 0f, 0f));
            }
        }

        private static void StampRemainderWall(Transform parent, float lengthMeters, Vector3 localPos)
        {
            // Pick the closest pack piece that fits.
            string prefab;
            float pieceLen;
            if (lengthMeters >= 1.5f) { prefab = Wall_3x2; pieceLen = 2f; }
            else                      { prefab = Wall_3x1; pieceLen = 1f; }

            InstantiatePrefabOrFallback(prefab, parent, localPos, Quaternion.identity,
                                        FallbackKind.Wall, new Vector3(pieceLen, WallHeight, 0.2f));
        }

        private static void StampWindowAccent(Transform parent, Vector3 localPos)
        {
            // Small emissive panel that reads as a window from inside. We use a
            // primitive Quad with an emissive URP/Lit material so we don't
            // depend on a window prefab the pack may not have.
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "WindowAccent";
            // Strip colliders – purely decorative.
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            // Slightly inset, halfway up the wall.
            go.transform.localPosition = localPos + new Vector3(0f, WallHeight * 0.55f, -0.05f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // face into room
            go.transform.localScale = new Vector3(2.0f, 1.0f, 1f);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var mat = CreateEmissiveMaterial(new Color(0.4f, 0.7f, 1.0f), 1.5f);
                mr.sharedMaterial = mat;
            }
            ApplyEnvironmentLayer(go);
            Undo.RegisterCreatedObjectUndo(go, "Create WindowAccent");
        }

        // =====================================================================
        // Corridors
        // =====================================================================
        private static void BuildCorridor(Transform parent, string name, Vector3 from, Vector3 to, float width)
        {
            // The corridor is a rectangular tube linking the two room edges.
            // It is centred on the segment between the two centres but truncated
            // so it doesn't poke into either room.
            Vector3 dir   = (to - from);
            float   total = dir.magnitude;
            dir.Normalize();

            // Trim so we start at hub edge and stop at side-room edge.
            // hub half-extent in this direction:
            float hubHalf  = HubSize * 0.5f;
            float sideHalf = SideRoomSize * 0.5f;
            float corridorLen = total - hubHalf - sideHalf;
            if (corridorLen <= 0.5f) return;

            Vector3 start  = from + dir * hubHalf;
            Vector3 mid    = start + dir * (corridorLen * 0.5f);

            var corridor = new GameObject(name);
            corridor.transform.SetParent(parent, false);
            corridor.transform.position = mid;
            // Align local +Z with travel direction; corridor "length" runs along Z.
            corridor.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            ApplyEnvironmentLayer(corridor);

            // Floor (single strip).
            BuildFloor(corridor.transform, width, corridorLen);

            // Ceiling.
            BuildCeiling(corridor.transform, width, corridorLen);

            // Side walls along +X and -X (running the corridor's length).
            BuildWallSide(corridor.transform, "Corridor_Wall_East",
                new Vector3( width * 0.5f, 0f, 0f), 90f, corridorLen, false, false);
            BuildWallSide(corridor.transform, "Corridor_Wall_West",
                new Vector3(-width * 0.5f, 0f, 0f), 270f, corridorLen, false, false);

            // Trim lights along the ceiling using emissive accents.
            int trimCount = Mathf.Max(2, Mathf.RoundToInt(corridorLen / 3f));
            for (int i = 0; i < trimCount; i++)
            {
                float t = (i + 0.5f) / trimCount;
                float z = -corridorLen * 0.5f + t * corridorLen;
                StampCeilingTrimLight(corridor.transform, new Vector3(0f, WallHeight - 0.05f, z));
            }
        }

        private static void StampCeilingTrimLight(Transform parent, Vector3 localPos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "CeilingTrimLight";
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // face down
            go.transform.localScale    = new Vector3(1.5f, 0.25f, 1f);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = CreateEmissiveMaterial(new Color(0.85f, 0.95f, 1f), 2.5f);
            }
            ApplyEnvironmentLayer(go);
            Undo.RegisterCreatedObjectUndo(go, "Create CeilingTrimLight");
        }

        // =====================================================================
        // Props
        // =====================================================================
        private static void ScatterProps(Transform parent, float width, float depth, Color accentColor)
        {
            var group = new GameObject("Props");
            group.transform.SetParent(parent, false);
            ApplyEnvironmentLayer(group);

            // Place a couple of containers in opposing corners and a canister/box.
            float hx = width * 0.5f - 0.8f;
            float hz = depth * 0.5f - 0.8f;

            InstantiatePrefabOrFallback(Prop_Container1, group.transform,
                new Vector3(-hx, 0f,  hz), Quaternion.Euler(0f, 45f, 0f),
                FallbackKind.Prop, new Vector3(1f, 1f, 1f));
            InstantiatePrefabOrFallback(Prop_Container2, group.transform,
                new Vector3( hx, 0f, -hz), Quaternion.Euler(0f, -30f, 0f),
                FallbackKind.Prop, new Vector3(1f, 1f, 1f));
            InstantiatePrefabOrFallback(Prop_Box, group.transform,
                new Vector3( hx, 0f,  hz), Quaternion.Euler(0f, 15f, 0f),
                FallbackKind.Prop, new Vector3(0.6f, 0.6f, 0.6f));
            InstantiatePrefabOrFallback(Prop_Canister, group.transform,
                new Vector3(-hx, 0f, -hz), Quaternion.identity,
                FallbackKind.Prop, new Vector3(0.4f, 0.7f, 0.4f));

            // Emissive accent strip along one wall's base, colour-matched to room.
            var accent = GameObject.CreatePrimitive(PrimitiveType.Cube);
            accent.name = "BaseAccentStrip";
            var col = accent.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            accent.transform.SetParent(group.transform, false);
            accent.transform.localPosition = new Vector3(0f, 0.08f, -depth * 0.5f + 0.15f);
            accent.transform.localScale    = new Vector3(width * 0.7f, 0.08f, 0.05f);
            var mr = accent.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = CreateEmissiveMaterial(accentColor, 2.0f);
            ApplyEnvironmentLayer(accent);
            Undo.RegisterCreatedObjectUndo(accent, "Create BaseAccentStrip");
        }

        private static void ScatterHubProps(Transform parent, float width, float depth)
        {
            var group = new GameObject("Props");
            group.transform.SetParent(parent, false);
            ApplyEnvironmentLayer(group);

            // A few light decorations in the hub corners (kept clear in the centre).
            float hx = width * 0.5f - 1.2f;
            float hz = depth * 0.5f - 1.2f;
            InstantiatePrefabOrFallback(Prop_Container1, group.transform,
                new Vector3(-hx, 0f,  hz), Quaternion.Euler(0f, 30f, 0f),
                FallbackKind.Prop, new Vector3(1f, 1f, 1f));
            InstantiatePrefabOrFallback(Prop_Container2, group.transform,
                new Vector3( hx, 0f,  hz), Quaternion.Euler(0f, -30f, 0f),
                FallbackKind.Prop, new Vector3(1f, 1f, 1f));
            InstantiatePrefabOrFallback(Prop_Box, group.transform,
                new Vector3(-hx, 0f, -hz), Quaternion.identity,
                FallbackKind.Prop, new Vector3(0.6f, 0.6f, 0.6f));
            InstantiatePrefabOrFallback(Prop_Canister, group.transform,
                new Vector3( hx, 0f, -hz), Quaternion.identity,
                FallbackKind.Prop, new Vector3(0.4f, 0.7f, 0.4f));
        }

        // =====================================================================
        // Lighting + reflection
        // =====================================================================
        private static void CreateDirectionalLight(Transform parent)
        {
            var go = new GameObject("DirectionalLight_Starlight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 30f, 0f);
            go.transform.localRotation = Quaternion.Euler(50f, 30f, 0f);

            var light = go.AddComponent<Light>();
            light.type      = LightType.Directional;
            light.color     = new Color32(0x4A, 0x6B, 0x8A, 0xFF); // #4A6B8A
            light.intensity = 0.3f;
            light.shadows   = LightShadows.Soft;

            ApplyEnvironmentLayer(go);
            Undo.RegisterCreatedObjectUndo(go, "Create Starlight Directional");
        }

        private static void CreateReflectionProbe(Transform parent, string name, Vector3 center, float width, float depth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center + new Vector3(0f, WallHeight * 0.5f, 0f);

            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode             = ReflectionProbeMode.Baked;
            probe.boxProjection    = true;
            probe.size             = new Vector3(width, WallHeight + 0.5f, depth);
            probe.center           = Vector3.zero;
            probe.intensity        = 1.0f;
            probe.resolution       = 128;
            probe.clearFlags       = ReflectionProbeClearFlags.Skybox;
            probe.cullingMask      = ~0;
            probe.refreshMode      = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode  = ReflectionProbeTimeSlicingMode.NoTimeSlicing;

            ApplyEnvironmentLayer(go);
            Undo.RegisterCreatedObjectUndo(go, "Create Reflection Probe");
        }

        // =====================================================================
        // Skybox
        // =====================================================================
        private static void ApplySkybox()
        {
            // 1) Look for any skybox material in the pack.
            var matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Dzeruza/MinimalScifiPack" });
            Material chosen = null;
            foreach (var guid in matGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat  = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && mat.shader != null && mat.shader.name.IndexOf("Skybox", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    chosen = mat;
                    break;
                }
            }

            if (chosen == null)
            {
                // 2) Generate a procedural starfield material once and stash it.
                chosen = CreateProceduralStarfieldSkybox();
            }

            if (chosen != null)
            {
                RenderSettings.skybox = chosen;
                DynamicGI.UpdateEnvironment();
            }
        }

        private static Material CreateProceduralStarfieldSkybox()
        {
            const string outDir   = "Assets/Editor/Setup/Generated";
            const string texPath  = outDir + "/Starfield_Cubemap.exr";
            const string matPath  = outDir + "/Starfield_Skybox.mat";

            if (!AssetDatabase.IsValidFolder("Assets/Editor"))           AssetDatabase.CreateFolder("Assets", "Editor");
            if (!AssetDatabase.IsValidFolder("Assets/Editor/Setup"))     AssetDatabase.CreateFolder("Assets/Editor", "Setup");
            if (!AssetDatabase.IsValidFolder(outDir))                    AssetDatabase.CreateFolder("Assets/Editor/Setup", "Generated");

            // If we already generated the material previously, reuse it.
            var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existingMat != null) return existingMat;

            // Build a 6-face cubemap by laying out a cross texture: simpler is to
            // generate one 1024x512 latlong-style 2D texture and use the
            // panoramic Skybox shader.
            const int width  = 1024;
            const int height = 512;
            var tex = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true);
            var pixels = new Color[width * height];

            var rng = new System.Random(20260428);
            // Background: very dark blue.
            var bg = new Color(0.005f, 0.008f, 0.02f, 1f);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

            // Sprinkle ~3500 stars; a few are noticeably brighter.
            int starCount = 3500;
            for (int i = 0; i < starCount; i++)
            {
                int x = rng.Next(width);
                int y = rng.Next(height);
                float brightness = (float)Math.Pow(rng.NextDouble(), 8.0); // most dim, few bright
                float v = 0.2f + brightness * 4.5f;
                Color c = new Color(v * 0.95f, v * 0.97f, v, 1f);
                int idx = y * width + x;
                pixels[idx] = c;

                // For the brightest stars, blur a 3x3 neighbourhood.
                if (brightness > 0.6f)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        int nidx = ny * width + nx;
                        Color old = pixels[nidx];
                        float falloff = 1f / (1 + dx * dx + dy * dy);
                        pixels[nidx] = new Color(
                            Mathf.Max(old.r, c.r * falloff * 0.6f),
                            Mathf.Max(old.g, c.g * falloff * 0.6f),
                            Mathf.Max(old.b, c.b * falloff * 0.6f),
                            1f);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            byte[] exr = ImageConversion.EncodeToEXR(tex, Texture2D.EXRFlags.CompressZIP);
            System.IO.File.WriteAllBytes(texPath, exr);
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);

            // Configure the texture as a panorama (latlong).
            var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureShape   = TextureImporterShape.TextureCube;
                importer.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
                importer.sRGBTexture    = false;
                importer.mipmapEnabled  = true;
                importer.filterMode     = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            var skyTex = AssetDatabase.LoadAssetAtPath<Cubemap>(texPath);

            // URP/Built-in compatible: Skybox/Cubemap shader exists in all RP variants.
            var shader = Shader.Find("Skybox/Cubemap");
            if (shader == null) shader = Shader.Find("Skybox/Procedural");

            var mat = new Material(shader);
            if (skyTex != null)
            {
                if (mat.HasProperty("_Tex"))     mat.SetTexture("_Tex", skyTex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", skyTex);
            }
            mat.SetFloat("_Exposure", 1.0f);

            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        // =====================================================================
        // Helpers
        // =====================================================================
        private enum FallbackKind { Wall, Door, FloorTile, Ceiling, Prop }

        private static GameObject InstantiatePrefabOrFallback(
            string assetPath, Transform parent, Vector3 localPos, Quaternion localRot,
            FallbackKind kind, Vector3 fallbackSize)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            GameObject go;
            if (prefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                if (go == null) return null;
                go.transform.localPosition = localPos;
                go.transform.localRotation = localRot;
            }
            else
            {
                if (_missingLogged.Add(assetPath))
                    Debug.LogWarning("[EnvironmentBuilder] Prefab not found, using primitive fallback: " + assetPath);
                go = CreatePrimitiveFallback(kind, fallbackSize);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPos;
                go.transform.localRotation = localRot;
            }
            ApplyEnvironmentLayer(go);
            Undo.RegisterCreatedObjectUndo(go, "Create " + kind);
            return go;
        }

        private static GameObject CreatePrimitiveFallback(FallbackKind kind, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Fallback_" + kind;
            go.transform.localScale = size;
            // Position adjustment per kind so the cube sits where the prefab pivot would.
            switch (kind)
            {
                case FallbackKind.Wall:
                case FallbackKind.Door:
                    // Wall / door: place pivot at floor + walls extend up.
                    go.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
                    break;
                case FallbackKind.Ceiling:
                    go.transform.localPosition = new Vector3(0f, -size.y * 0.5f, 0f);
                    break;
                case FallbackKind.FloorTile:
                    go.transform.localPosition = new Vector3(0f, -size.y * 0.5f, 0f);
                    break;
            }
            return go;
        }

        private static Material CreateEmissiveMaterial(Color color, float intensity)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            // Enable emission.
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", color * Mathf.LinearToGammaSpace(intensity));
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return mat;
        }

        private static void ApplyEnvironmentLayer(GameObject go)
        {
            int layer = LayerMask.NameToLayer(EnvironmentLayerName);
            if (layer < 0) return; // layer not defined - leave as Default (per spec).
            SetLayerRecursive(go, layer);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }
    }
}
