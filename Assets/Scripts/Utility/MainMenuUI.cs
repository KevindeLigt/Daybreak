using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    [Tooltip("First scene of a fresh run. Include it in the active Build Profile.")]
    public string firstLevelScene = "Level_01_Church";

    public void OnStartGamePressed()
    {
        SceneLoader.StartNewRun(firstLevelScene);
    }

    public void OnQuitPressed()
    {
        Application.Quit();
    }
}
