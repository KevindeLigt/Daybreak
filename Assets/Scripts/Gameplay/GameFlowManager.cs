using System.Collections;
using UnityEngine;

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("Wave Settings")]
    public GameObject[] enemyPrefabs;
    public int startingEnemiesPerWave = 5;

    [Tooltip("Used only when the active region has no Curse Objective assigned.")]
    public int maxWaves = 5;

    public float timeBetweenWaves = 3f;
    public float spawnDelay = 0.3f;

    [Header("Curse Objective Flow")]
    [SerializeField] private bool useCurseObjectiveFlow = true;

    [Tooltip("Pause after killing the final enemy before revealing the next Root Heart.")]
    [SerializeField, Min(0f)] private float rootHeartReleaseDelay = 0.75f;

    [Tooltip("Pause after depositing a Root Heart before the next wave begins.")]
    [SerializeField, Min(0f)] private float nextWaveAfterDepositDelay = 1.5f;

    [Tooltip("Pause after opening a gate and activating a new region before its first wave begins.")]
    [SerializeField, Min(0f)] private float newRegionStartDelay = 1f;

    [Header("References")]
    public UIManager uiManager;

    private int currentWave;
    private int aliveEnemies;
    private int enemiesThisWave;
    private bool gameActive;
    private bool isSpawningWave;
    private bool isWaitingForNextWave;
    private bool isWaitingForHeartDeposit;
    private bool hasStarted;

    private MapRegion currentRegion;
    private CurseObjectiveController currentObjective;
    private RegionManager subscribedRegionManager;
    private Coroutine progressionCoroutine;

    public int CurrentWave => currentWave;
    public int AliveEnemies => aliveEnemies;
    public bool GameActive => gameActive;
    public bool IsWaitingForHeartDeposit => isWaitingForHeartDeposit;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;
    }

    private void Start()
    {
        if (uiManager == null)
            uiManager = FindObjectOfType<UIManager>();

        ResetGameState();
        SubscribeToRegionManager();
        StartCoroutine(StartGameRoutine());
        FindObjectOfType<ShrineSpawnerManager>()?.SpawnShrinesForRun();

        UIManager.Instance?.ClearAllStatusEffects();
        PlayerWeaponStats.Instance?.ResetRunStats();
    }

    private void OnDestroy()
    {
        UnsubscribeFromRegionManager();
        UnbindCurrentObjective();
    }

    private void ResetGameState()
    {
        currentWave = 0;
        aliveEnemies = 0;
        enemiesThisWave = 0;
        gameActive = true;
        isSpawningWave = false;
        isWaitingForNextWave = false;
        isWaitingForHeartDeposit = false;
        hasStarted = false;
    }

    private IEnumerator StartGameRoutine()
    {
        // Allows RegionManager, regional runtime objects, UI, and objectives to initialize.
        yield return new WaitForSeconds(0.2f);

        SubscribeToRegionManager();
        hasStarted = true;

        MapRegion startingRegion = RegionManager.Instance != null
            ? RegionManager.Instance.CurrentRegion
            : null;

        BeginRegionEncounter(startingRegion, 0f);
    }

    private void SubscribeToRegionManager()
    {
        RegionManager manager = RegionManager.Instance;
        if (manager == subscribedRegionManager)
            return;

        UnsubscribeFromRegionManager();
        subscribedRegionManager = manager;

        if (subscribedRegionManager != null)
            subscribedRegionManager.CurrentRegionChanged += HandleCurrentRegionChanged;
    }

    private void UnsubscribeFromRegionManager()
    {
        if (subscribedRegionManager != null)
            subscribedRegionManager.CurrentRegionChanged -= HandleCurrentRegionChanged;

        subscribedRegionManager = null;
    }

    private void HandleCurrentRegionChanged(MapRegion region)
    {
        if (!hasStarted || !gameActive)
            return;

        BeginRegionEncounter(region, newRegionStartDelay);
    }

    private void BeginRegionEncounter(MapRegion region, float delay)
    {
        StopProgressionCoroutine();
        UnbindCurrentObjective();

        currentRegion = region;
        currentWave = 0;
        aliveEnemies = 0;
        enemiesThisWave = 0;
        isSpawningWave = false;
        isWaitingForNextWave = false;
        isWaitingForHeartDeposit = false;

        uiManager?.UpdateEnemyCount(0, 0);

        if (currentRegion == null)
        {
            Debug.LogError("GameFlowManager cannot begin an encounter because RegionManager has no Current Region.");
            return;
        }

        currentObjective = currentRegion.CurseObjective;
        BindCurrentObjective();

        if (currentRegion.IsCompleted)
        {
            Debug.LogWarning($"GameFlowManager was asked to begin completed region: {currentRegion.RegionName}");
            return;
        }

        progressionCoroutine = StartCoroutine(BeginRegionAfterDelay(delay));
    }

    private IEnumerator BeginRegionAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        progressionCoroutine = null;

        if (!gameActive)
            yield break;

        StartNextWave();
    }

    private void BindCurrentObjective()
    {
        if (currentObjective != null)
            currentObjective.HeartDepositedRuntime += HandleHeartDeposited;
    }

    private void UnbindCurrentObjective()
    {
        if (currentObjective != null)
            currentObjective.HeartDepositedRuntime -= HandleHeartDeposited;

        currentObjective = null;
    }

    private void StartNextWave()
    {
        if (!gameActive || isWaitingForHeartDeposit || isSpawningWave)
            return;

        if (!HasValidSpawnSetup())
        {
            Debug.LogError("Wave could not start. Check RegionManager.CurrentRegion, its active spawners, enemy prefabs, and encounter state.");
            return;
        }

        currentWave++;
        enemiesThisWave = startingEnemiesPerWave + (currentWave - 1) * 2;
        aliveEnemies = enemiesThisWave;
        isSpawningWave = true;
        isWaitingForNextWave = false;

        uiManager?.UpdateWave(currentWave);
        uiManager?.UpdateEnemyCount(aliveEnemies, enemiesThisWave);

        StartCoroutine(SpawnWaveRoutine());
    }

    private IEnumerator SpawnWaveRoutine()
    {
        int failedSpawns = 0;

        for (int i = 0; i < enemiesThisWave; i++)
        {
            if (!gameActive)
                yield break;

            if (!SpawnEnemy())
                failedSpawns++;

            yield return new WaitForSeconds(spawnDelay);
        }

        if (failedSpawns > 0)
        {
            aliveEnemies = Mathf.Max(0, aliveEnemies - failedSpawns);
            uiManager?.UpdateEnemyCount(aliveEnemies, enemiesThisWave);
            Debug.LogWarning($"{failedSpawns} enemies could not be spawned during Wave {currentWave}.");
        }

        isSpawningWave = false;

        if (aliveEnemies <= 0)
        {
            if (failedSpawns >= enemiesThisWave)
            {
                Debug.LogError("The entire wave failed to spawn. Progression has been paused instead of falsely completing the wave.");
                yield break;
            }

            HandleWaveCleared();
        }
    }

    private bool SpawnEnemy()
    {
        if (RegionManager.Instance == null)
        {
            Debug.LogError("No RegionManager exists in the scene.");
            return false;
        }

        EnemySpawner[] activeSpawners = RegionManager.Instance.GetCurrentRegionSpawners();
        if (activeSpawners.Length == 0)
        {
            Debug.LogWarning("No active spawners were found in the current combat region.");
            return false;
        }

        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogError("GameFlowManager has no enemy prefabs assigned.");
            return false;
        }

        EnemySpawner chosenSpawner = activeSpawners[Random.Range(0, activeSpawners.Length)];
        GameObject chosenPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];

        if (chosenSpawner == null || chosenPrefab == null)
        {
            Debug.LogWarning("A selected enemy spawner or enemy prefab was null.");
            return false;
        }

        Instantiate(
            chosenPrefab,
            chosenSpawner.spawnPoint.position,
            chosenSpawner.spawnPoint.rotation);

        return true;
    }

    private bool HasValidSpawnSetup()
    {
        if (RegionManager.Instance == null)
            return false;

        if (RegionManager.Instance.CurrentRegion == null)
            return false;

        if (RegionManager.Instance.GetCurrentRegionSpawners().Length == 0)
            return false;

        return enemyPrefabs != null && enemyPrefabs.Length > 0;
    }

    public void EnemyDied()
    {
        if (!gameActive)
            return;

        aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
        uiManager?.UpdateEnemyCount(aliveEnemies, enemiesThisWave);

        // Do not complete the wave while more enemies are still waiting to spawn.
        if (aliveEnemies <= 0 && !isSpawningWave)
            HandleWaveCleared();
    }

    private void HandleWaveCleared()
    {
        if (!gameActive || isWaitingForNextWave || isWaitingForHeartDeposit)
            return;

        if (useCurseObjectiveFlow && currentObjective != null && !currentObjective.IsCompleted)
        {
            isWaitingForNextWave = true;
            isWaitingForHeartDeposit = true;
            progressionCoroutine = StartCoroutine(ReleaseRootHeartAfterDelay());
            return;
        }

        // Backwards-compatible fallback for a region that has no Curse Objective.
        if (currentWave >= maxWaves)
        {
            WinGame();
            return;
        }

        isWaitingForNextWave = true;
        progressionCoroutine = StartCoroutine(NextWaveDelay(timeBetweenWaves));
    }

    private IEnumerator ReleaseRootHeartAfterDelay()
    {
        if (rootHeartReleaseDelay > 0f)
            yield return new WaitForSeconds(rootHeartReleaseDelay);

        progressionCoroutine = null;

        if (!gameActive || currentObjective == null)
            yield break;

        if (!currentObjective.ReleaseNextHeart())
        {
            Debug.LogError(
                "The wave was cleared, but the next Root Heart could not be released. " +
                "Check Wave Controlled Pickups, Required Hearts, and the pickup array.");
        }
    }

    private void HandleHeartDeposited(CurseObjectiveController objective)
    {
        if (!gameActive || objective == null || objective != currentObjective)
            return;

        if (!isWaitingForHeartDeposit)
        {
            Debug.LogWarning("A Root Heart was deposited while GameFlowManager was not waiting for one.");
            return;
        }

        isWaitingForHeartDeposit = false;
        isWaitingForNextWave = false;

        if (objective.IsCompleted)
        {
            CompleteCurrentRegionalEncounter();
            return;
        }

        progressionCoroutine = StartCoroutine(NextWaveDelay(nextWaveAfterDepositDelay));
    }

    private IEnumerator NextWaveDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        progressionCoroutine = null;

        if (!gameActive)
            yield break;

        StartNextWave();
    }

    private void CompleteCurrentRegionalEncounter()
    {
        StopProgressionCoroutine();
        aliveEnemies = 0;
        enemiesThisWave = 0;
        isSpawningWave = false;
        isWaitingForHeartDeposit = false;
        isWaitingForNextWave = false;

        uiManager?.UpdateEnemyCount(0, 0);

        MapRegion completedRegion = currentRegion;

        if (RegionManager.Instance == null || !RegionManager.Instance.CompleteCurrentRegion())
        {
            Debug.LogError("The Curse Objective completed, but the current MapRegion could not be completed.");
            return;
        }

        if (completedRegion != null && completedRegion.CompleteRunWhenFinished)
        {
            WinGame();
            return;
        }

        Debug.Log(
            $"Regional encounter completed: {completedRegion?.RegionName}. " +
            "Its spawners are disabled and its exit gate is now unlocked.");
    }

    private void StopProgressionCoroutine()
    {
        if (progressionCoroutine == null)
            return;

        StopCoroutine(progressionCoroutine);
        progressionCoroutine = null;
    }

    public void PlayerDied()
    {
        LoseGame();
    }

    public void WinGame()
    {
        if (!gameActive)
            return;

        gameActive = false;
        Time.timeScale = 0f;
        uiManager?.ShowGameOver(true);
    }

    public void LoseGame()
    {
        if (!gameActive)
            return;

        gameActive = false;
        Time.timeScale = 0f;
        uiManager?.ShowGameOver(false);
    }
}
