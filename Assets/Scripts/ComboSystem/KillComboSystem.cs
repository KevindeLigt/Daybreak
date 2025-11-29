using UnityEngine;

public class KillComboSystem : MonoBehaviour
{
    public static KillComboSystem Instance;

    [Header("Combo Settings")]
    public float comboTimeout = 2.5f;
    public int combo = 0;

    private float comboTimer;

    // Thresholds match how many layers you want active
    public int[] layerThresholds = { 1, 3, 6, 10 };

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Update()
    {
        if (combo > 0)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f)
            {
                combo = 0;
                MusicLayersController.Instance.ResetToBase();
            }
        }
    }

    public void OnEnemyKilled()
    {
        combo++;
        comboTimer = comboTimeout;

        int activeLayers = CalculateLayerCount(combo);
        MusicLayersController.Instance.SetActiveLayers(activeLayers);
    }

    public void OnPlayerDamaged()
    {
        combo = 0;
        MusicLayersController.Instance.ResetToBase();
    }

    int CalculateLayerCount(int currentCombo)
    {
        int count = 0;

        foreach (int threshold in layerThresholds)
        {
            if (currentCombo >= threshold)
                count++;
        }

        return count;
    }
}
