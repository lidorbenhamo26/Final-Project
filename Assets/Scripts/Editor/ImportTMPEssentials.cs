using UnityEditor;
using UnityEngine;

public class ImportTMPEssentials
{
    [MenuItem("Tools/Mission Focus/Import TMP Essentials")]
    public static void Run()
    {
        const string pkg = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
        Debug.Log("[MissionFocus] Importing TMP Essential Resources from: " + pkg);
        AssetDatabase.ImportPackage(pkg, false);
    }
}
