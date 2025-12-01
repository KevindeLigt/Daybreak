using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI References")]
    public TMP_Text waveText;
    public TMP_Text enemyCountText;
    public Image healthBar;
    public TMP_Text healthText;
    public GameObject gameOverPanel;
    public TMP_Text gameOverTitle;
    public Button restartButton;
    public Button quitButton;
    public Image leftShell;
    public Image rightShell;
    public Sprite loadedSprite;
    public Sprite emptySprite;
    public Image ramCooldownImage;

    // ================================
    // STATUS EFFECT SYSTEM
    // ================================
    [Header("Status Effect UI")]
    public Transform statusEffectPanel;      // Vertical Layout Group
    public GameObject statusEffectEntryPrefab;

    // key → instance
    private Dictionary<string, GameObject> activeStatusEffects = new();


    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        Instance = this;
    }

    private void Start()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        if (restartButton != null)
            restartButton.onClick.AddListener(() =>
            {
                SceneLoader.LoadScene("Gameplay");
            });

        if (quitButton != null)
            quitButton.onClick.AddListener(() =>
            {
                SceneLoader.LoadScene("MainMenu");
            });
    }

    // ================================
    // PUBLIC UI API
    // ================================

    public void UpdateWave(int waveNumber)
    {
        if (waveText != null)
            waveText.text = $"Wave {waveNumber}";
    }

    public void UpdateEnemyCount(int remaining, int total)
    {
        if (enemyCountText != null)
            enemyCountText.text = $"Enemies: {remaining}/{total}";
    }

    public void UpdateHealth(float current, float max)
    {
        if (healthBar != null)
        {
            float f = Mathf.Clamp01(max <= 0 ? 0 : current / max);
            healthBar.fillAmount = f;
        }

        if (healthText != null)
            healthText.text = $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(max)}";
    }

    public void UpdateShotgunAmmo(int count)
    {
        leftShell.sprite = count >= 1 ? loadedSprite : emptySprite;
        rightShell.sprite = count == 2 ? loadedSprite : emptySprite;
    }

    public void UpdateRamCooldown(float normalized)
    {
        if (ramCooldownImage)
            ramCooldownImage.fillAmount = normalized;
    }

    public void ShowGameOver(bool playerWon)
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (gameOverTitle != null)
            gameOverTitle.text = playerWon ? "You Survived!" : "You Died";

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ================================
    // STATUS EFFECT UI METHODS
    // ================================

    /// <summary>
    /// Creates or updates a status entry.
    /// </summary>
    public void SetStatusEffect(string key, string text)
    {
        if (statusEffectPanel == null || statusEffectEntryPrefab == null)
        {
            Debug.LogWarning("StatusEffectPanel or Prefab not assigned!");
            return;
        }

        if (!activeStatusEffects.TryGetValue(key, out GameObject entry))
        {
            entry = Instantiate(statusEffectEntryPrefab, statusEffectPanel);
            activeStatusEffects[key] = entry;
        }

        var txt = entry.GetComponentInChildren<TMP_Text>();
        if (txt != null)
            txt.text = text;
    }

    /// <summary>
    /// Removes a status entry if it exists.
    /// </summary>
    public void RemoveStatusEffect(string key)
    {
        if (activeStatusEffects.TryGetValue(key, out GameObject entry))
        {
            Destroy(entry);
            activeStatusEffects.Remove(key);
        }
    }

    /// <summary>
    /// Clears all status effect entries.
    /// </summary>
    public void ClearAllStatusEffects()
    {
        foreach (var entry in activeStatusEffects.Values)
            Destroy(entry);

        activeStatusEffects.Clear();
    }
}
