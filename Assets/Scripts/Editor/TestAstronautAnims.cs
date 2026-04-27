#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class TestAstronautAnims
{
    [MenuItem("Tools/Astronaut/Trigger Wave")]
    public static void Wave() => Trig("Wave");
    [MenuItem("Tools/Astronaut/Trigger Alert")]
    public static void Alert() => Trig("Alert");
    [MenuItem("Tools/Astronaut/Set Speed=2 (Walk)")]
    public static void Walk() { var a = GetAnimator(); if (a != null) a.SetFloat("Speed", 2f); }
    [MenuItem("Tools/Astronaut/Set Speed=6 (Run)")]
    public static void Run()  { var a = GetAnimator(); if (a != null) a.SetFloat("Speed", 6f); }
    [MenuItem("Tools/Astronaut/Set Speed=0 (Idle)")]
    public static void Idle() { var a = GetAnimator(); if (a != null) a.SetFloat("Speed", 0f); }

    static void Trig(string trig) { var a = GetAnimator(); if (a != null) { a.SetBool("Grounded", true); a.SetTrigger(trig); Debug.Log("[AstroTest] triggered " + trig); } else Debug.LogError("[AstroTest] no Astronaut animator found"); }

    static Animator GetAnimator()
    {
        var go = GameObject.Find("Astronaut");
        return go != null ? go.GetComponent<Animator>() : null;
    }
}
#endif
