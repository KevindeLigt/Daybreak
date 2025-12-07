using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Tooltip("If false, the spawner will not be used until a region unlocks it.")]
    public bool isActive = false;

    [Tooltip("Optionally: custom weight so some spawners are picked more often.")]
    public float spawnWeight = 1f;

    public Transform spawnPoint => transform;
}
