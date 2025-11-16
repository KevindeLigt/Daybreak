using UnityEngine;

public static class SceneLoader
{
    public static string NextScene;

    public static void LoadScene(string sceneName)
    {
        NextScene = sceneName;
        UnityEngine.SceneManagement.SceneManager.LoadScene("LoadingScreen");
    }
}
