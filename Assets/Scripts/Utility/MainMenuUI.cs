using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    public void OnStartGamePressed()
    {
        SceneLoader.LoadScene("Gameplay");
    }

    public void OnQuitPressed()
    {
        Application.Quit();
    }
}
