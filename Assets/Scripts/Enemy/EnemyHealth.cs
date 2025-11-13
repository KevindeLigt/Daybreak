using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    [Header("Feedback Settings")]
    [SerializeField] private Renderer enemyRenderer;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private float stunDuration = 0.3f;

    private Color originalColor;
    private bool isFlashing;
    private NavMeshAgent agent;

    private void Start()
    {
        currentHealth = maxHealth;
        agent = GetComponent<NavMeshAgent>();

        if (enemyRenderer == null)
            enemyRenderer = GetComponentInChildren<Renderer>();

        if (enemyRenderer != null)
            originalColor = enemyRenderer.material.color;
    }

    public void TakeDamage(float damage)
    {
        if (isFlashing) return;

        currentHealth -= damage;
        StartCoroutine(FlashAndStun());

        if (currentHealth <= 0)
            Die();
    }

    private System.Collections.IEnumerator FlashAndStun()
    {
        isFlashing = true;

        // Stop movement briefly
        if (agent != null) agent.isStopped = true;

        // Flash color
        if (enemyRenderer != null)
            enemyRenderer.material.color = hitColor;

        yield return new WaitForSeconds(flashDuration);

        // Reset color
        if (enemyRenderer != null)
            enemyRenderer.material.color = originalColor;

        // Small stun delay
        yield return new WaitForSeconds(stunDuration);
        if (agent != null) agent.isStopped = false;

        isFlashing = false;
    }

    private void Die()
    {
        // Here you could call into an enemy manager, play a death animation, etc.
        Destroy(gameObject);
    }
}
