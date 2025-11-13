using UnityEngine;
using System.Collections;

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("Wave Settings")]
    public GameObject[] enemyPrefabs;
    public Transform[] spawnPoints;
    public int startingEnemiesPerWave = 5;
    public int maxWaves = 5;
    public float timeBetweenWaves = 3f;
    public float spawnDelay = 0.3f;

    [Header("References")]
    public UIManager uiManager; // assign in inspector

    private int currentWave = 0;
    private int aliveEnemies = 0;
    private int enemiesThisWave = 0;
    private bool gameActive = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        Instance = this;
    }

    private void Start()
    {
        if (uiManager == null)
            uiManager = FindObjectOfType<UIManager>();

        StartCoroutine(StartGameRoutine());
    }

    private IEnumerator StartGameRoutine()
    {
        yield return new WaitForSeconds(0.5f);
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
        if (spawnPoints == null || spawnPoints.Length == 0 || enemyPrefabs == null || enemyPrefabs.Length == 0) return;

        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        Instantiate(prefab, sp.position, sp.rotation);
    }

    // Called by EnemyHealth when an enemy dies
    public void EnemyDied()
    {
        aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
        uiManager.UpdateEnemyCount(aliveEnemies, enemiesThisWave);

        if (aliveEnemies <= 0)
        {
            // All enemies of the wave are dead
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

    // Called by PlayerHealth when player dies
    public void PlayerDied()
    {
        LoseGame();
    }

    public void WinGame()
    {
        gameActive = false;
        uiManager.ShowGameOver(true);
        Time.timeScale = 0f;
    }

    public void LoseGame()
    {
        gameActive = false;
        uiManager.ShowGameOver(false);
        Time.timeScale = 0f;
    }
}
