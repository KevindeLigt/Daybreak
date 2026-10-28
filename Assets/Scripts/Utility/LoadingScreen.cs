using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class LoadingScreen : MonoBehaviour
{
    public Slider progressBar;

    private void Start()
    {
        StartCoroutine(LoadAsync());
    }

    private IEnumerator LoadAsync()
    {
        if (!SceneLoader.IsLoading || string.IsNullOrWhiteSpace(SceneLoader.NextScene))
        {
            Debug.LogError("Start from a level or MainMenu; LoadingScreen needs a destination queued by SceneLoader.", this);
            yield break;
        }
        AsyncOperation op = null;
        try { op = SceneManager.LoadSceneAsync(SceneLoader.NextScene, LoadSceneMode.Single); }
        catch (System.Exception ex) { SceneLoader.ReportLoadFailure(ex.Message); }
        if (op == null)
        {
            SceneLoader.ReportLoadFailure("Could not start destination loading.");
            yield break;
        }
        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            float progress = Mathf.Clamp01(op.progress / 0.9f);

            if (progressBar != null)
                progressBar.value = progress;

            if (progress >= 1f)
            {
                SceneLoader.RequestDestinationActivation();
                op.allowSceneActivation = SceneLoader.CanActivateDestination;
            }

            yield return null;
        }
    }
}
