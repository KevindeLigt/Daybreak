using UnityEngine;

public class KillComboSystem : MonoBehaviour
{
    public static KillComboSystem Instance;
    [Header("Optional streak timeout")]
    [Tooltip("Off by default: only taking damage breaks the streak. Objective travel can take as long as needed.")]
    public bool useComboTimeout = false;
    public float comboTimeout = 2.5f;
    [Tooltip("Runtime projection of kills since damage. Score/music milestones live on PlayerStatsManager.")]
    public int combo;

    private PlayerStatsManager boundStats;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }
    public static KillComboSystem GetOrCreate()
    {
        if (Instance == null)
        {
            KillComboSystem existing = FindObjectOfType<KillComboSystem>();
            if (existing != null) existing.Awake();
            else new GameObject("KillComboSystem").AddComponent<KillComboSystem>();
        }
        return Instance;
    }
    private void OnEnable() => Bind();
    private void Start() { Bind(); Refresh(); }
    private void Update()
    {
        if (boundStats != PlayerStatsManager.Instance) Bind();
        if (boundStats != null && useComboTimeout && boundStats.Run.active && boundStats.Run.streak > 0 &&
            Time.time - boundStats.Run.lastKillTime > Mathf.Max(.01f, comboTimeout))
            boundStats.BreakStreak();
    }
    private void Bind()
    {
        if (boundStats != null) boundStats.Changed -= Refresh;
        boundStats = PlayerStatsManager.GetOrCreate();
        boundStats.Changed += Refresh;
        Refresh();
    }
    private void Refresh()
    {
        if (boundStats == null) return;
        combo = boundStats.Run.streak;
        MusicLayersController.Instance?.SetActiveLayers(boundStats.MusicLayerCount);
    }

    public void OnEnemyKilled(EnemyKillInfo kill)
    {
        if (boundStats != PlayerStatsManager.Instance || boundStats == null) Bind();
        // A free test room can score real enemies with GameFlowManager disabled.
        if (!boundStats.Run.initialized && !SceneLoader.IsInputBlocked) boundStats.BeginRun();
        boundStats.RecordKill(kill);
    }

    // Legacy event entry point; normal project kills come through the typed EnemyHealth event.
    public void OnEnemyKilled() => OnEnemyKilled(new EnemyKillInfo { enemyType = "Unknown", basePoints = 100 });
    public void OnPlayerDamaged()
    {
        if (boundStats != PlayerStatsManager.Instance || boundStats == null) Bind();
        boundStats.BreakStreak();
        Refresh();
    }
    private void OnDisable()
    {
        if (boundStats != null) boundStats.Changed -= Refresh;
        boundStats = null;
    }
    private void OnDestroy()
    {
        OnDisable();
        if (Instance == this) Instance = null;
    }
}
