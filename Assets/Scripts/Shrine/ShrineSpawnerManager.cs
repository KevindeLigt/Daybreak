using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ShrineSpawnerManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject healthShrinePrefab;
    public GameObject damageShrinePrefab;

    private List<ShrineSpawnPoint> healthPoints = new();
    private List<ShrineSpawnPoint> damagePoints = new();

    void Awake()
    {
        // Collect all spawn points in scene
        foreach (var point in FindObjectsOfType<ShrineSpawnPoint>())
        {
            if (point.shrineType == ShrineType.Health)
                healthPoints.Add(point);

            if (point.shrineType == ShrineType.Damage)
                damagePoints.Add(point);
        }
    }

    public void SpawnShrinesForRun()
    {
        SpawnShrine(healthPoints, healthShrinePrefab);
        SpawnShrine(damagePoints, damageShrinePrefab);
    }

    private void SpawnShrine(List<ShrineSpawnPoint> list, GameObject prefab)
    {
        if (list.Count == 0 || prefab == null) return;

        ShrineSpawnPoint selected = list[Random.Range(0, list.Count)];

        Instantiate(
            prefab,
            selected.transform.position,
            selected.transform.rotation
        );
    }
}
