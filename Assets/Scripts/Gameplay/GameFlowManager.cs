using System.Collections;
using UnityEngine;

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("Wave Settings")]
    public GameObject[] enemyPrefabs;
    public int startingEnemiesPerWave = 5;
    public int maxWaves = 5;
    public float timeBetweenWaves = 3f;
    public float spawnDelay = 0.3f;

    [Header("References")]
    public UIManager uiManager;

    private int currentWave;
    private int aliveEnemies;
    private int enemiesThisWave;
    private bool gameActive;
    private bool isSpawningWave;
    private bool isWaitingForNextWave;

    public int CurrentWave => currentWave;
    public int AliveEnemies => aliveEnemies;
    public bool GameActive => gameActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Always reset time scale when entering the scene fresh.
        Time.timeScale = 1f;
    }

    private void Start()
    {
        if (uiManager == null)
            uiManager = FindObjectOfType<UIManager>();

        ResetGameState();
        StartCoroutine(StartGameRoutine());
        FindObjectOfType<ShrineSpawnerManager>()?.SpawnShrinesForRun();

        UIManager.Instance?.ClearAllStatusEffects();
        PlayerWeaponStats.Instance?.ResetRunStats();
    }

    private void ResetGameState()
    {
        currentWave = 0;
        aliveEnemies = 0;
        enemiesThisWave = 0;
        gameActive = true;
        isSpawningWave = false;
        isWaitingForNextWave = false;
    }

    private IEnumerator StartGameRoutine()
    {
        // Small delay so UI and region initialization are complete.
        yield return new WaitForSeconds(0.2f);
        StartNextWave();
    }

    private void StartNextWave()
    {
        if (!gameActive)
            return;

        if (!HasValidSpawnSetup())
        {
            Debug.LogError("Wave could not start. Check RegionManager.CurrentRegion, the region spawner list, active spawner states, and enemy prefabs.");
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
        if (!gameActive || isWaitingForNextWave)
            return;

        if (currentWave >= maxWaves)
        {
            WinGame();
            return;
        }

        isWaitingForNextWave = true;
        StartCoroutine(NextWaveDelay());
    }

    private IEnumerator NextWaveDelay()
    {
        yield return new WaitForSeconds(timeBetweenWaves);

        if (!gameActive)
            yield break;

        StartNextWave();
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
