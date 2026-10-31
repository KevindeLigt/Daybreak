using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Tooltip("If true, the player heals to full when max health increases.")]
    public bool healToFullOnMaxIncrease = true;


    public ScreenFlashEffect screenFlash;

    // Hit sound
    [Header("Audio")]
    public AudioClip playerHitSFX;
    public float playerHitVolume = 1f;

    public AudioClip healPickupSFX;
    public float healPickupVolume = 1f;


    void Start()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        UIManager.Instance?.UpdateHealth(currentHealth, maxHealth);
    }

    public void TakeDamage(float dmg)
    {
        if (currentHealth <= 0f || dmg <= 0f || float.IsNaN(dmg) || float.IsInfinity(dmg)) return;
        float actualDamage = Mathf.Min(currentHealth, dmg);
        currentHealth -= dmg;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        // UI update
        UIManager.Instance?.UpdateHealth(currentHealth, maxHealth);

        // Visual flash
        screenFlash?.Flash(Color.red, 0.4f, 0.25f);

        PlayerStatsManager.GetOrCreate().AddDamageTaken(actualDamage);
        KillComboSystem.GetOrCreate().OnPlayerDamaged();

        // Hit SFX with pitch variation
        if (playerHitSFX != null)
        {
            float pitch = Random.Range(0.9f, 1.1f);
            PlayClipAtPointWithPitch(playerHitSFX, transform.position, playerHitVolume, pitch);
        }

        if (currentHealth <= 0f)
        {
            PlayerStatsManager.GetOrCreate().FinishRun(ScoredRunResult.Died);
            GameFlowManager.Instance?.PlayerDied();
        }
    }

    /// <summary>Accepts a health pickup for a living player, retaining full-health pickup behaviour.</summary>
    public bool TryHeal(float amount)
    {
        if (currentHealth <= 0f || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            return false;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        UIManager.Instance?.UpdateHealth(currentHealth, maxHealth);
        PlayHealFeedback();
        return true;
    }

    public void IncreaseMaxHealth(float amount)
    {
        maxHealth += amount;
        currentHealth = healToFullOnMaxIncrease ? maxHealth : currentHealth;

        UIManager.Instance?.UpdateHealth(currentHealth, maxHealth);

        UIManager.Instance?.SetStatusEffect(
            "MaxHealth",
            $"Max Health +{maxHealth - 100f}"
        );
    }

    public void PlayHealFeedback()
    {
        // Heal SFX
        if (healPickupSFX != null)
            AudioSource.PlayClipAtPoint(healPickupSFX, transform.position, healPickupVolume);

        // Heal flash (green-ish)
        if (screenFlash != null)
        {
            var healColor = new Color(0f, 1f, 0.2f);
            screenFlash.Flash(healColor, 0.3f, 0.25f);
        }
    }


    // ===== Helper for pitched 1-shot audio =====
    private void PlayClipAtPointWithPitch(AudioClip clip, Vector3 pos, float volume, float pitch)
    {
        GameObject go = new GameObject("PlayerHitAudio");
        go.transform.position = pos;

        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.pitch = pitch;
        src.spatialBlend = 0f; // 2D UI-style sound

        src.Play();
        Destroy(go, clip.length / Mathf.Max(pitch, 0.01f));
    }
}
