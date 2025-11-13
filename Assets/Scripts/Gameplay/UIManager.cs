using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI References")]
    public TMP_Text waveText;
    public TMP_Text enemyCountText;
    public Image healthBar;
    public GameObject gameOverPanel;
    public TMP_Text gameOverTitle;
    public Button restartButton;
    public Button quitButton;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        Instance = this;
    }

    private void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (restartButton != null)
            restartButton.onClick.AddListener(() => UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex));

        if (quitButton != null)
            quitButton.onClick.AddListener(() => Application.Quit());
    }

    // Public API used by other scripts:
    public void UpdateWave(int waveNumber)
    {
        if (waveText != null) waveText.text = $"Wave {waveNumber}";
    }

    public void UpdateEnemyCount(int remaining, int total)
    {
        if (enemyCountText != null) enemyCountText.text = $"Enemies: {remaining}/{total}";
    }

    public void UpdateHealth(float current, float max)
    {
        if (healthBar != null)
        {
            float f = Mathf.Clamp01(max <= 0 ? 0 : current / max);
            healthBar.fillAmount = f;
        }
    }

    // Make public so GameFlowManager / PlayerHealth can call it
    public void ShowGameOver(bool playerWon)
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (gameOverTitle != null) gameOverTitle.text = playerWon ? "You Survived!" : "You Died";
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
