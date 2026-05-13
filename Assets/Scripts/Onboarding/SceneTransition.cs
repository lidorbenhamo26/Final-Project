using UnityEngine.SceneManagement;

public static class SceneTransition
{
    public const string MainSceneName = "MainScene";
    public const string StartSceneName = "StartScene";
    public const string TutorialSceneName = "TutorialScene";

    public static void LoadMain() => SceneManager.LoadScene(MainSceneName);
    public static void LoadStart() => SceneManager.LoadScene(StartSceneName);
    public static void LoadTutorial() => SceneManager.LoadScene(TutorialSceneName);
}
