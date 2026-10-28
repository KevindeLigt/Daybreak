using System;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStatsManager : MonoBehaviour
{
    public static PlayerStatsManager Instance;
    public PlayerStats stats;
    [Header("Kill Score — shared across the run")]
    public KillScoreSettings scoring = new KillScoreSettings();
    [Header("Profile Saving")]
    [Min(.25f)] public float saveInterval = 2f;

    public KillScoreRun Run { get; private set; } = new KillScoreRun();
    public long RunScore => Run.score;
    public long HighScore => stats != null ? stats.killHighScore : 0L;
    public int Multiplier { get { Rules.GetTier(Run.streak, out int value, out _); return value; } }
    public int MusicLayerCount { get { Rules.GetTier(Run.streak, out _, out int layers); return Run.active ? layers : 0; } }
    public KillScoreSettings Rules => scoring ?? (scoring = new KillScoreSettings());
    public event Action Changed;
    public event Action<KillScoreAward> KillScored;
    public event Action StreakBroken;

    private const int SchemaVersion = 2;
    private string savePath;
    private bool dirty, saveWarningLogged, readOnlyProfile, initialized;
    private float lastSaveTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (initialized) return;
        initialized = true;
        // Use a dedicated GameObject for this manager; it must be a root to persist.
        if (transform.parent != null) transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
        savePath = Path.Combine(Application.persistentDataPath, "playerstats.json");
        LoadStats();
    }

    public static PlayerStatsManager GetOrCreate()
    {
        if (Instance == null)
        {
            PlayerStatsManager existing = FindObjectOfType<PlayerStatsManager>();
            if (existing != null) existing.Awake();
            else new GameObject("PlayerStatsManager").AddComponent<PlayerStatsManager>();
        }
        return Instance;
    }

    private void Update()
    {
        if (dirty && Time.unscaledTime - lastSaveTime >= Mathf.Max(.25f, saveInterval)) SaveStats();
    }

    [ContextMenu("Begin Score Run (Test Scene)")]
    public void BeginRun()
    {
        Run = new KillScoreRun { initialized = true, active = true };
        Changed?.Invoke();
    }

    public void FinishRun(ScoredRunResult result)
    {
        if (!Run.active) return;
        Run.active = false;
        KillScoreCalculator.BreakStreak(Run);
        if (result == ScoredRunResult.Completed) stats.runsCompleted = KillScoreCalculator.Increment(stats.runsCompleted);
        if (result == ScoredRunResult.Died) stats.deaths = KillScoreCalculator.Increment(stats.deaths);
        dirty = true;
        Changed?.Invoke();
        SaveStats();
    }

    public KillScoreAward RecordKill(EnemyKillInfo kill)
    {
        KillScoreAward award = KillScoreCalculator.Award(Run, Rules, kill, Time.time);
        if (!award.valid) return award;
        AddKillCounters(kill.enemyType);
        if (award.eviscerated) stats.eviscerationKills = KillScoreCalculator.Increment(stats.eviscerationKills);
        if (award.range == KillScoreRange.Close) stats.closeRangeKills = KillScoreCalculator.Increment(stats.closeRangeKills);
        if (award.range == KillScoreRange.Long) stats.longRangeKills = KillScoreCalculator.Increment(stats.longRangeKills);
        if (award.rapidKills == 2) stats.doubleKillBonuses = KillScoreCalculator.Increment(stats.doubleKillBonuses);
        if (award.rapidKills == 3) stats.tripleKillBonuses = KillScoreCalculator.Increment(stats.tripleKillBonuses);
        if (award.rapidKills > 3) stats.multiKillBonuses = KillScoreCalculator.Increment(stats.multiKillBonuses);
        stats.totalKillScore = KillScoreCalculator.Add(stats.totalKillScore, award.points);
        stats.killHighScore = Math.Max(stats.killHighScore, Run.score);
        stats.bestKillStreak = Math.Max(stats.bestKillStreak, Run.bestStreak);
        stats.highestMultiplier = Math.Max(stats.highestMultiplier, award.multiplier);
        dirty = true;
        Changed?.Invoke();
        KillScored?.Invoke(award);
        return award;
    }

    public void BreakStreak()
    {
        if (!KillScoreCalculator.BreakStreak(Run)) return;
        Changed?.Invoke();
        StreakBroken?.Invoke();
    }

    public void ResetRapidKillWindow()
    {
        // A loading screen should not join kills in separate levels into a double kill.
        Run.rapidKills = 0;
        Run.hasRecentKill = false;
    }

    // Compatibility counters for other project scripts. EnemyHealth uses RecordKill once.
    public void AddKill(string type) { AddKillCounters(type); dirty = true; }
    private void AddKillCounters(string type)
    {
        stats.totalKills = KillScoreCalculator.Increment(stats.totalKills);
        string normalized = (type ?? string.Empty).Trim();
        if (normalized.StartsWith("Zombie", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Shambler", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Pursuer", StringComparison.OrdinalIgnoreCase))
            stats.zombieKills = KillScoreCalculator.Increment(stats.zombieKills);
        if (normalized.StartsWith("Skeleton", StringComparison.OrdinalIgnoreCase))
            stats.skeletonKills = KillScoreCalculator.Increment(stats.skeletonKills);
    }

    public void AddDamageDealt(float damage)
    {
        if (!ValidPositive(damage)) return;
        stats.totalDamageDealt = AddDamage(stats.totalDamageDealt, damage); dirty = true;
    }
    public void AddDamageTaken(float damage)
    {
        if (!ValidPositive(damage)) return;
        stats.totalDamageTaken = AddDamage(stats.totalDamageTaken, damage); dirty = true;
    }
    public void AddShot(string weapon)
    {
        if (string.Equals(weapon, "Shotgun", StringComparison.OrdinalIgnoreCase))
            stats.shotgunShotsFired = KillScoreCalculator.Increment(stats.shotgunShotsFired);
        else if (string.Equals(weapon, "Crossbow", StringComparison.OrdinalIgnoreCase))
            stats.crossbowShotsFired = KillScoreCalculator.Increment(stats.crossbowShotsFired);
        else return;
        dirty = true;
    }
    public void AddCrossbowHit()
    { stats.crossbowHits = KillScoreCalculator.Increment(stats.crossbowHits); dirty = true; }

    public void SaveStats()
    {
        if (!dirty || readOnlyProfile || stats == null || string.IsNullOrEmpty(savePath)) return;
        lastSaveTime = Time.unscaledTime;
        string temporary = savePath + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllText(temporary, JsonUtility.ToJson(stats, true));
            if (File.Exists(savePath))
            {
                try { File.Replace(temporary, savePath, savePath + ".bak"); }
                catch (PlatformNotSupportedException) { CopyFallback(temporary); }
            }
            else File.Move(temporary, savePath);
            dirty = saveWarningLogged = false;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
        {
            if (!saveWarningLogged) Debug.LogWarning("Player statistics could not be saved: " + ex.Message, this);
            saveWarningLogged = true;
        }
    }

    private void CopyFallback(string temporary)
    {
        File.Copy(savePath, savePath + ".bak", true);
        File.Copy(temporary, savePath, true);
        File.Delete(temporary);
    }

    public void LoadStats()
    {
        if (string.IsNullOrEmpty(savePath)) savePath = Path.Combine(Application.persistentDataPath, "playerstats.json");
        readOnlyProfile = false;
        stats = ReadProfile(savePath);
        if (stats == null)
        {
            stats = ReadProfile(savePath + ".bak");
            // Preserve the unreadable original for recovery rather than silently deleting it.
            try { if (File.Exists(savePath)) File.Move(savePath, savePath + ".corrupt-" + DateTime.UtcNow.Ticks); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            { Debug.LogWarning("Could not preserve the unreadable stats file: " + ex.Message, this); readOnlyProfile = true; }
        }
        if (stats == null) stats = new PlayerStats();
        if (stats.schemaVersion > SchemaVersion)
        {
            readOnlyProfile = true;
            Debug.LogWarning("Statistics were written by a newer version. This version will not overwrite that profile.", this);
        }
        else stats.schemaVersion = SchemaVersion;
        stats.killHighScore = Math.Max(0L, stats.killHighScore);
        stats.totalKillScore = Math.Max(0L, stats.totalKillScore);
        stats.highestMultiplier = Math.Max(1, stats.highestMultiplier);
        dirty = true;
        SaveStats();
        Changed?.Invoke();
    }

    private PlayerStats ReadProfile(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            string json = File.ReadAllText(path).Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}")) throw new FormatException("Expected a JSON object.");
            return JsonUtility.FromJson<PlayerStats>(json);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
            ex is ArgumentException || ex is FormatException)
        {
            Debug.LogWarning("Could not read statistics from " + path + ": " + ex.Message, this);
            return null;
        }
    }

    private static bool ValidPositive(float value) => value > 0f && !float.IsInfinity(value) && !float.IsNaN(value);
    private static float AddDamage(float total, float amount) => (float)Math.Min(float.MaxValue,
        (float.IsNaN(total) || float.IsInfinity(total) ? 0d : Math.Max(0d, total)) + amount);
    private void OnApplicationPause(bool paused) { if (paused) SaveStats(); }
    private void OnApplicationQuit() => SaveStats();
    private void OnDestroy() { if (Instance == this) { SaveStats(); Instance = null; } }
}
