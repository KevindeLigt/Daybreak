using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Run values only. Carries one shotgun; scene actors stay in their own scenes.</summary>
public sealed class RunState : MonoBehaviour
{
    public static RunState Instance { get; private set; }
    public string FirstLevelScene { get; private set; } = "Level_01_Church";
    public int GlobalWave { get; private set; }
    public bool AwaitingArrival => !string.IsNullOrEmpty(pendingScene);
    public bool HasContinuation => incoming != null;
    public IReadOnlyList<string> CompletedLevels => completedLevels;

    private string pendingScene;
    private Snapshot incoming;
    private readonly List<string> completedLevels = new List<string>();

    private sealed class Snapshot
    {
        public float health, maxHealth, baseDamage, bonusDamage, ramCooldown;
        public int wave, shells;
        public bool leftBarrelNext;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public static RunState GetOrCreate()
    {
        if (Instance == null) new GameObject("RunState").AddComponent<RunState>();
        return Instance;
    }

    public void PrepareNewRun(string firstLevel)
    {
        PlayerStatsManager.GetOrCreate().BeginRun();
        FirstLevelScene = firstLevel;
        GlobalWave = 0;
        completedLevels.Clear();
        incoming = null;
        pendingScene = firstLevel;
    }

    public void EndRun()
    {
        PlayerStatsManager.Instance?.FinishRun(ScoredRunResult.Abandoned);
        pendingScene = null;
        incoming = null;
        GlobalWave = 0;
        completedLevels.Clear();
    }

    public bool PrepareContinuation(string destination, PlayerHealth player,
        WeaponManager manager, int wave, string completedLevel, out string error)
    {
        error = null;
        DoubleBarrelShotgunController shotgun = manager != null ? manager.Shotgun : null;
        if (player == null || player.currentHealth <= 0f || shotgun == null)
        {
            error = "Level exit needs a living PlayerHealth and a WeaponManager pointing to its shotgun.";
            return false;
        }

        // Optional compatibility with existing Inspector damage tuning.
        // A player without this component uses the shotgun's raw pellet damage.
        PlayerWeaponStats tuning = PlayerWeaponStats.Instance;
        ShoulderRam ram = player.GetComponent<ShoulderRam>();
        incoming = new Snapshot
        {
            health = player.currentHealth,
            maxHealth = player.maxHealth,
            baseDamage = tuning != null ? tuning.baseDamageMultiplier : 1f,
            bonusDamage = tuning != null ? tuning.bonusDamageFlat : 0f,
            ramCooldown = ram != null ? ram.RemainingCooldown : 0f,
            wave = Mathf.Max(0, wave),
            shells = shotgun.LoadedShells,
            leftBarrelNext = shotgun.LeftBarrelNext
        };
        pendingScene = destination;
        if (!string.IsNullOrEmpty(completedLevel) && !completedLevels.Contains(completedLevel))
            completedLevels.Add(completedLevel);
        return true;
    }

    /// <summary>Called after destination Start methods; validates before changing player values.</summary>
    public bool RestoreLevel(Scene scene, string firstLevel, PlayerHealth player,
        WeaponManager manager, out string error)
    {
        error = null;
        DoubleBarrelShotgunController shotgun = manager != null ? manager.Shotgun : null;
        if (player == null || shotgun == null)
        {
            error = "Assign PlayerHealth and a WeaponManager pointing to this player's shotgun.";
            return false;
        }
        if (AwaitingArrival && !SceneLoader.MatchesScene(pendingScene, scene))
        {
            error = "Loaded level does not match the queued run destination: " + pendingScene;
            return false;
        }

        PlayerWeaponStats tuning = PlayerWeaponStats.Instance;
        if (incoming != null)
        {
            // Never silently discard real damage modifiers during migration.
            if (tuning == null && (incoming.baseDamage != 1f || incoming.bonusDamage != 0f))
            {
                error = "This run carries non-default damage tuning. Keep PlayerWeaponStats on the destination player, or start a fresh run with consistent shotgun tuning.";
                return false;
            }

            player.maxHealth = Mathf.Max(1f, incoming.maxHealth);
            player.currentHealth = Mathf.Clamp(incoming.health, 0f, player.maxHealth);
            if (tuning != null)
            {
                tuning.baseDamageMultiplier = incoming.baseDamage;
                tuning.bonusDamageFlat = incoming.bonusDamage;
            }
            GlobalWave = incoming.wave;
            shotgun.RestoreLoadedState(incoming.shells, incoming.leftBarrelNext);
            ShoulderRam ram = player.GetComponent<ShoulderRam>();
            if (ram != null) ram.RestoreCooldown(incoming.ramCooldown);
        }
        else
        {
            // Direct Editor Play is fresh; a menu-started run already chose its first scene.
            if (!AwaitingArrival)
                PrepareNewRun(string.IsNullOrWhiteSpace(firstLevel) ? scene.path : firstLevel.Trim());
            if (tuning != null) tuning.ResetRunStats();
            GlobalWave = 0;
        }

        shotgun.OnEquip();
        PlayerStatsManager.Instance?.ResetRapidKillWindow();
        UIManager.Instance?.UpdateHealth(player.currentHealth, player.maxHealth);
        UIManager.Instance?.UpdateWave(GlobalWave);
        UIManager.Instance?.UpdateShotgunAmmo(shotgun.LoadedShells);
        incoming = null;
        pendingScene = null;
        return true;
    }
}
