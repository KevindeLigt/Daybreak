using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>Displays the run score and an editable title for its multiplier.</summary>
[DisallowMultipleComponent]
public sealed class KillScoreHUD : MonoBehaviour
{
    [Header("TextMeshPro Labels")]
    public TMP_Text scoreText;
    [FormerlySerializedAs("multiplierText")]
    public TMP_Text rankText;

    [Header("Rank Names")]
    [Tooltip("In multiplier order: Element 0 = 1x, Element 1 = 2x, and so on. Extra multipliers use the last name.")]
    public string[] rankNames = { "Unholy", "Wicked", "Wild", "Hunter", "Blasphemer" };

    [Header("Optional Feedback")]
    [Tooltip("Once per frame when the multiplier increases. Can trigger your animation or a dedicated guitar-riff AudioSource.")]
    public UnityEvent onRankUpgraded = new UnityEvent();
    [Tooltip("When the multiplier drops. Can restore your normal appearance.")]
    public UnityEvent onRankReset = new UnityEvent();

    private PlayerStatsManager bound;
    private KillScoreRun observedRun;
    private int observedMultiplier;
    private bool pendingUpgrade, pendingReset;

    private void OnEnable() => Bind();

    private void OnDisable()
    {
        Unbind();
        pendingUpgrade = pendingReset = false;
    }

    private void Bind()
    {
        Unbind();
        observedRun = null;
        pendingUpgrade = pendingReset = false;
        bound = PlayerStatsManager.GetOrCreate();
        if (bound == null) return;
        bound.Changed += Refresh;
        Refresh();
    }

    private void Unbind()
    {
        if (bound != null) bound.Changed -= Refresh;
        bound = null;
    }

    private void Refresh()
    {
        if (bound == null) return;
        int multiplier = bound.Multiplier;
        WriteLabels(bound.RunScore, multiplier);

        if (observedRun != bound.Run)
        {
            // Entering a scene, re-enabling the HUD, or starting a new run
            // displays its current rank without replaying an upgrade effect.
            pendingUpgrade = pendingReset = false;
        }
        else if (multiplier > observedMultiplier)
        {
            pendingUpgrade = true;
            pendingReset = false;
        }
        else if (multiplier < observedMultiplier)
        {
            // A hit also cancels an upgrade queued earlier in this frame.
            pendingUpgrade = false;
            pendingReset = true;
        }

        observedRun = bound.Run;
        observedMultiplier = multiplier;
    }

    private void LateUpdate()
    {
        if (bound != PlayerStatsManager.Instance) Bind();

        // A shotgun burst can cross several tiers. Celebrate the final rank once.
        bool upgraded = pendingUpgrade;
        bool reset = pendingReset;
        pendingUpgrade = pendingReset = false;
        if (upgraded) onRankUpgraded?.Invoke();
        else if (reset) onRankReset?.Invoke();
    }

    private void WriteLabels(long score, int multiplier)
    {
        if (scoreText != null)
            scoreText.text = score.ToString("N0", CultureInfo.InvariantCulture);
        if (rankText != null)
            rankText.text = GetRankName(multiplier);
    }

    public string GetRankName(int multiplier)
    {
        if (rankNames == null || rankNames.Length == 0) return string.Empty;
        int index = Mathf.Clamp(multiplier, 1, rankNames.Length) - 1;
        return rankNames[index] ?? string.Empty;
    }

    [ContextMenu("Preview Labels")]
    private void PreviewLabels()
    {
        if (Application.isPlaying && bound != null) Refresh();
        else WriteLabels(0, 1);
    }
}
