using UnityEngine;
using System.IO;

public class PlayerStatsManager : MonoBehaviour
{
    public static PlayerStatsManager Instance;
    public PlayerStats stats;

    private string savePath;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        savePath = Path.Combine(Application.persistentDataPath, "playerstats.json");
        LoadStats();
    }

    public void SaveStats()
    {
        string json = JsonUtility.ToJson(stats, true);
        File.WriteAllText(savePath, json);
    }

    public void LoadStats()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            stats = JsonUtility.FromJson<PlayerStats>(json);
        }
        else
        {
            stats = new PlayerStats();
            SaveStats(); // create file
        }
    }

    // Helper: whenever something changes
    public void AddKill(string type)
    {
        stats.totalKills++;

        if (type == "Zombie") stats.zombieKills++;
        if (type == "Skeleton") stats.skeletonKills++;

        SaveStats();
    }

    public void AddDamageDealt(float dmg)
    {
        stats.totalDamageDealt += dmg;
        SaveStats();
    }

    public void AddDamageTaken(float dmg)
    {
        stats.totalDamageTaken += dmg;
        SaveStats();
    }

    public void AddShot(string weapon)
    {
        if (weapon == "Shotgun") stats.shotgunShotsFired++;
        if (weapon == "Crossbow") stats.crossbowShotsFired++;

        SaveStats();
    }

    public void AddCrossbowHit()
    {
        stats.crossbowHits++;
        SaveStats();
    }
}
