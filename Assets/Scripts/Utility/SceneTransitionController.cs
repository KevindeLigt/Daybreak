using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Automatically created, persistent fade runner. No scene actors are retained.</summary>
public sealed class SceneTransitionController : MonoBehaviour
{
    public static SceneTransitionController Instance { get; private set; }
    public bool IsBusy { get; private set; }
    public bool IsCovered { get; private set; }
    private bool activationRequested;
    private string failure;
    private Image fadeImage;
    private float previousTimeScale;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static SceneTransitionController GetOrCreate()
    {
        if (Instance == null) new GameObject("SceneTransitionController").AddComponent<SceneTransitionController>();
        return Instance;
    }

    public void Begin(string destination, bool gameplay, float duration)
    {
        if (IsBusy) return;
        IsBusy = true;
        IsCovered = false;
        activationRequested = false;
        failure = null;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        EnsureFadeImage();
        StartCoroutine(Transition(destination, gameplay, duration));
    }

    public void RequestActivation() => activationRequested = true;
    public void ReportFailure(string error) => failure = error;

    private IEnumerator Transition(string destination, bool gameplay, float duration)
    {
        yield return Fade(1f, duration);
        AsyncOperation loading = null;
        try { loading = SceneManager.LoadSceneAsync(SceneLoader.LoadingScene, LoadSceneMode.Single); }
        catch (System.Exception ex) { failure = ex.Message; }
        if (loading == null) { Abort(failure ?? "Could not load LoadingScreen.", true); yield break; }
        while (!loading.isDone) yield return null;
        LoadingScreen screen = FindObjectOfType<LoadingScreen>();
        if (screen == null || !screen.isActiveAndEnabled)
        {
            Abort("LoadingScreen scene needs an active LoadingScreen component.", false);
            yield break;
        }
        yield return Fade(0f, 0.15f);
        while (!activationRequested && failure == null) yield return null;
        if (failure != null) { Abort(failure, false); yield break; }
        yield return Fade(1f, 0.15f);
        IsCovered = true;
        while (!SceneLoader.MatchesScene(destination, SceneManager.GetActiveScene()) && failure == null) yield return null;
        if (failure != null) { Abort(failure, false); yield break; }

        if (gameplay)
        {
            float waited = 0f;
            while (LevelSceneSetup.Instance == null || !LevelSceneSetup.Instance.IsReady)
            {
                LevelSceneSetup setup = LevelSceneSetup.Instance;
                if (setup != null && !string.IsNullOrEmpty(setup.InitializationError))
                {
                    Abort(setup.InitializationError, false); yield break;
                }
                waited += Time.unscaledDeltaTime;
                if (waited > 5f)
                {
                    Abort("Destination needs an active LevelSceneSetup with PlayerHealth and a WeaponManager pointing to its shotgun. Encounter remains paused.", false);
                    yield break;
                }
                yield return null;
            }
        }
        else
        {
            yield return null;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        yield return Fade(0f, 0.25f);
        Time.timeScale = 1f;
        IsBusy = false;
        IsCovered = false;
        fadeImage.gameObject.SetActive(false);
    }

    private void Abort(string error, bool sourceStillLoaded)
    {
        Debug.LogError("Scene transition stopped: " + error, this);
        if (fadeImage != null) fadeImage.gameObject.SetActive(false);
        Time.timeScale = sourceStillLoaded ? previousTimeScale : 1f;
        IsBusy = false;
        IsCovered = false;
        // Queued values remain available after destination configuration errors.
    }

    private void EnsureFadeImage()
    {
        if (fadeImage == null)
        {
            GameObject canvasObject = new GameObject("TransitionFade", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            GameObject imageObject = new GameObject("Black", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            fadeImage = imageObject.GetComponent<Image>();
            fadeImage.raycastTarget = false;
        }
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.gameObject.SetActive(true);
    }

    private IEnumerator Fade(float target, float duration)
    {
        float start = fadeImage.color.a;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(start, target, elapsed / Mathf.Max(0.001f, duration)));
            yield return null;
        }
        fadeImage.color = new Color(0f, 0f, 0f, target);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (IsBusy) Time.timeScale = 1f;
        Instance = null;
    }
}
