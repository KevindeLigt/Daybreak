using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;

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
}
