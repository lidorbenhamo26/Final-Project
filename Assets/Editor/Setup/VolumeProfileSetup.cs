using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SpaceStation.EditorSetup
{
    /// <summary>
    /// Adds Tonemapping + Color Adjustments to SpaceStation_VolumeProfile so the
    /// scene reads as brighter and warmer (closer to the Blender material-preview
    /// look). Idempotent: if the components already exist, their values are reset.
    /// </summary>
    public static class VolumeProfileSetup
    {
        private const string ProfilePath = "Assets/Settings/SpaceStation_VolumeProfile.asset";

        [MenuItem("Setup/8 - Brighten Volume Profile")]
        public static void Apply()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogError($"[VolumeProfileSetup] Profile not found at {ProfilePath}");
                return;
            }

            // Tonemapping in Neutral mode compresses highlights and crushes the
            // scene in Play Mode (looked great in Scene View, dark in Game View).
            // Disable it so linear values from the lighting reach the screen.
            if (profile.TryGet<Tonemapping>(out var tone))
            {
                tone.active = false;
            }

            if (!profile.TryGet<ColorAdjustments>(out var ca))
            {
                ca = profile.Add<ColorAdjustments>(overrides: true);
            }
            ca.active = true;
            ca.postExposure.overrideState = true;
            ca.postExposure.value = 1.5f;          // big boost so Game View stops feeling dim
            ca.contrast.overrideState = true;
            ca.contrast.value = -10f;              // less crushing so blacks stay readable
            ca.saturation.overrideState = true;
            ca.saturation.value = 5f;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("[VolumeProfileSetup] Tonemapping disabled, ColorAdjustments set (+0.8 EV, -5 contrast, +5 saturation).");
        }
    }
}
