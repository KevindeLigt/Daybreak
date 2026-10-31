using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoader
{
    public const string LoadingScene = "LoadingScreen";
    public static string NextScene;
    public static bool IsLoading => SceneTransitionController.Instance != null && SceneTransitionController.Instance.IsBusy;
    public static bool IsInputBlocked => Time.timeScale <= 0f || IsLoading ||
        (LevelSceneSetup.Instance != null && !LevelSceneSetup.Instance.IsReady);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => NextScene = null;

    // Compatibility for existing menu UnityEvents. Level exits use TryContinueRun instead.
    public static void LoadScene(string sceneName)
    {
        if (string.Equals(SceneName(sceneName), "MainMenu", StringComparison.OrdinalIgnoreCase)) ReturnToMenu(sceneName);
        else StartNewRun(sceneName);
    }

    public static bool StartNewRun(string firstLevel)
    {
        if (!ValidateDestination(firstLevel, false, out string error)) { Debug.LogError(error); return false; }
        RunState.GetOrCreate().PrepareNewRun(firstLevel.Trim());
        return Begin(firstLevel.Trim(), true, 0.25f);
    }

    public static bool RestartRun()
    {
        string first = RunState.Instance != null ? RunState.Instance.FirstLevelScene : "Level_01_Church";
        return StartNewRun(first);
    }

    public static bool ReturnToMenu(string sceneName = "MainMenu")
    {
        if (!ValidateDestination(sceneName, false, out string error)) { Debug.LogError(error); return false; }
        RunState.Instance?.EndRun();
        return Begin(sceneName.Trim(), false, 0.2f);
    }

    public static bool TryContinueRun(string destination, PlayerHealth player, float fadeDuration = 0.3f)
    {
        if (IsInputBlocked) return false;
        if (!ValidateDestination(destination, true, out string error)) { Debug.LogError(error); return false; }
        LevelSceneSetup setup = LevelSceneSetup.Instance;
        if (setup == null || !setup.IsReady || setup.player != player)
        {
            Debug.LogError("Level exit: add LevelSceneSetup to the source scene and assign this player.");
            return false;
        }
        GameFlowManager flow = GameFlowManager.Instance;
        MapRegion region = RegionManager.Instance != null ? RegionManager.Instance.CurrentRegion : null;
        if (flow == null || !flow.GameActive || region == null || !region.IsCompleted) return false;
        if (!RunState.GetOrCreate().PrepareContinuation(destination.Trim(), player, setup.weaponManager,
            flow.CurrentWave, player.gameObject.scene.path, out error))
        {
            Debug.LogError("Level exit: " + error);
            return false;
        }
        return Begin(destination.Trim(), true, Mathf.Max(0f, fadeDuration));
    }

    public static bool ValidateDestination(string destination, bool rejectCurrentScene, out string error)
    {
        error = null;
        if (IsLoading) { error = "A scene transition is already running."; return false; }
        if (string.IsNullOrWhiteSpace(destination) ||
            string.Equals(SceneName(destination), LoadingScene, StringComparison.OrdinalIgnoreCase))
        {
            error = "Choose a gameplay/menu destination, not an empty name or LoadingScreen.";
            return false;
        }
        if (rejectCurrentScene && MatchesScene(destination, SceneManager.GetActiveScene()))
        {
            error = "Level exit cannot reload its current scene. Choose the next level.";
            return false;
        }
        if (!Application.CanStreamedLevelBeLoaded(destination.Trim()) || !Application.CanStreamedLevelBeLoaded(LoadingScene))
        {
            error = "Include '" + destination.Trim() + "' and 'LoadingScreen' in the active Build Profile's Scene List.";
            return false;
        }
        return true;
    }

    private static bool Begin(string destination, bool gameplay, float duration)
    {
        NextScene = destination;
        SceneTransitionController.GetOrCreate().Begin(destination, gameplay, duration);
        return true;
    }

    public static bool MatchesScene(string expected, Scene scene)
    {
        if (string.IsNullOrWhiteSpace(expected)) return false;
        string normalized = expected.Trim().Replace('\\', '/');
        if (normalized.IndexOf('/') >= 0)
        {
            if (!normalized.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) normalized += ".unity";
            string path = scene.path.Replace('\\', '/');
            return string.Equals(normalized, path, StringComparison.OrdinalIgnoreCase) ||
                string.Equals("Assets/" + normalized, path, StringComparison.OrdinalIgnoreCase);
        }
        return string.Equals(SceneName(normalized), scene.name, StringComparison.OrdinalIgnoreCase);
    }

    private static string SceneName(string value)
    {
        if (value == null) return string.Empty;
        value = value.Trim().Replace('\\', '/');
        int slash = value.LastIndexOf('/');
        if (slash >= 0) value = value.Substring(slash + 1);
        if (value.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) value = value.Substring(0, value.Length - 6);
        return value;
    }

    public static void RequestDestinationActivation() => SceneTransitionController.Instance?.RequestActivation();
    public static bool CanActivateDestination => SceneTransitionController.Instance != null && SceneTransitionController.Instance.IsCovered;
    public static void ReportLoadFailure(string error) => SceneTransitionController.Instance?.ReportFailure(error);
}
