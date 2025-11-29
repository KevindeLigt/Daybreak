using UnityEngine;




public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;
    public DamageFlashEffect damageFlash;

    [Header("Audio")]
    public AudioClip playerHitSFX;
    public float playerHitVolume = 1f;

    private void Start()
    {
        currentHealth = maxHealth;
        UIManager.Instance?.UpdateHealth(currentHealth, maxHealth);
    }

    public void TakeDamage(float dmg)
    {
        currentHealth -= dmg;
        currentHealth = Mathf.Max(0, currentHealth);
        UIManager.Instance?.UpdateHealth(currentHealth, maxHealth);
        KillComboSystem.Instance.OnPlayerDamaged();
        damageFlash?.PlayFlash();


        if (playerHitSFX != null)
        {
            float pitch = Random.Range(0.9f, 1.1f);
            PlayClipAtPointWithPitch(playerHitSFX, transform.position, playerHitVolume, pitch);
        }


        if (currentHealth <= 0f)
        {
            // inform the flow manager
            GameFlowManager.Instance?.PlayerDied();
        }
    }

    // convenience method to heal
    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        UIManager.Instance?.UpdateHealth(currentHealth, maxHealth);
    }

    private void PlayClipAtPointWithPitch(AudioClip clip, Vector3 position, float volume, float pitch)
    {
        GameObject go = new GameObject("PlayerHitAudio");
        go.transform.position = position;

        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.pitch = pitch;
        src.spatialBlend = 0f; // keep it 2D UI feedback
        src.Play();

        Destroy(go, clip.length / Mathf.Max(pitch, 0.01f));
    }

}
