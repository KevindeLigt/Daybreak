using UnityEngine;

public class MapRegion : MonoBehaviour
{
    [Header("Region Settings")]
    public string regionName = "Region";
    public bool isUnlocked = false;

    [Header("Content To Activate On Unlock")]
    public GameObject[] enableWhenUnlocked;

    [Header("Content To Disable On Unlock")]
    public GameObject[] disableWhenUnlocked;

    [Header("Enemy Spawners In This Region")]
    public EnemySpawner[] regionSpawners;

    public void Unlock()
    {
        if (isUnlocked) return;
        isUnlocked = true;

        // Enable objects
        foreach (var obj in enableWhenUnlocked)
            if (obj) obj.SetActive(true);

        // Disable objects
        foreach (var obj in disableWhenUnlocked)
            if (obj) obj.SetActive(false);

        // Activate enemy spawners
        foreach (var sp in regionSpawners)
            if (sp) sp.isActive = true;


        Debug.Log($"Region unlocked: {regionName}");
    }
}
