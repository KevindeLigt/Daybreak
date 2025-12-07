using UnityEngine;
using System.Collections;

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

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);

        Instance = this;

        // Always reset time scale when entering the scene fresh
        Time.timeScale = 1f;
    }

    private void Start()
    {
        if (uiManager == null)
            uiManager = FindObjectOfType<UIManager>();

        ResetGameState();
        StartCoroutine(StartGameRoutine());
        FindObjectOfType<ShrineSpawnerManager>()?.SpawnShrinesForRun();
        UIManager.Instance.ClearAllStatusEffects();
        PlayerWeaponStats.Instance.ResetRunStats();

    }

    private void ResetGameState()
    {
        currentWave = 0;
        aliveEnemies = 0;
        enemiesThisWave = 0;
        gameActive = true;
    }

    private IEnumerator StartGameRoutine()
    {
        // tiny delay so the UI has initialized
        yield return new WaitForSeconds(0.2f);

        StartNextWave();
    }

    private void StartNextWave()
    {
        currentWave++;
        enemiesThisWave = startingEnemiesPerWave + (currentWave - 1) * 2;
        aliveEnemies = enemiesThisWave;

        uiManager.UpdateWave(currentWave);
        uiManager.UpdateEnemyCount(aliveEnemies, enemiesThisWave);

        StartCoroutine(SpawnWaveRoutine());
    }

    private IEnumerator SpawnWaveRoutine()
    {
        for (int i = 0; i < enemiesThisWave; i++)
        {
            SpawnEnemy();
            yield return new WaitForSeconds(spawnDelay);
        }
    }

    private void SpawnEnemy()
    {
        EnemySpawner[] active = RegionManager.Instance.GetActiveSpawners();
        if (active.Length == 0)
        {
            Debug.LogWarning("No active spawners found!");
            return;
        }

        EnemySpawner chosen = active[Random.Range(0, active.Length)];
        GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];

        Instantiate(prefab, chosen.spawnPoint.position, chosen.spawnPoint.rotation);
    }



    public void EnemyDied()
    {
        aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
        uiManager.UpdateEnemyCount(aliveEnemies, enemiesThisWave);

        if (aliveEnemies <= 0)
        {
            if (currentWave >= maxWaves)
            {
                WinGame();
            }
            else
            {
                StartCoroutine(NextWaveDelay());
            }
        }
    }

    private IEnumerator NextWaveDelay()
    {
        yield return new WaitForSeconds(timeBetweenWaves);
        StartNextWave();
    }

    public void PlayerDied()
    {
        LoseGame();
    }

    public void WinGame()
    {
        if (!gameActive) return;

        gameActive = false;
        Time.timeScale = 0f;
        uiManager.ShowGameOver(true);
    }

    public void LoseGame()
    {
        if (!gameActive) return;

        gameActive = false;
        Time.timeScale = 0f;
        uiManager.ShowGameOver(false);
    }
}
