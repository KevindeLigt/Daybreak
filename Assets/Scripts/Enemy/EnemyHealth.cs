using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("Feedback")]
    public Renderer enemyRenderer;
    public Color hitColor = Color.white;
    public float flashDuration = 0.08f;
    public float stunDuration = 0.25f;

    private Color originalColor;
    private NavMeshAgent agent;
    private bool isFlashing;

    private void Start()
    {
        currentHealth = maxHealth;
        agent = GetComponent<NavMeshAgent>();

        if (enemyRenderer == null) enemyRenderer = GetComponentInChildren<Renderer>();
        if (enemyRenderer != null) originalColor = enemyRenderer.material.color;
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;

        if (!isFlashing)
            StartCoroutine(FlashAndStun());

        if (currentHealth <= 0f)
            Die();
    }

    private System.Collections.IEnumerator FlashAndStun()
    {
        isFlashing = true;

        if (agent != null) agent.isStopped = true;
        if (enemyRenderer != null) enemyRenderer.material.color = hitColor;

        yield return new WaitForSeconds(flashDuration);

        if (enemyRenderer != null) enemyRenderer.material.color = originalColor;

        yield return new WaitForSeconds(stunDuration);

        if (agent != null) agent.isStopped = false;
        isFlashing = false;
    }

    private void Die()
    {
        // inform flow manager
        GameFlowManager.Instance?.EnemyDied();

        // TODO: play death VFX/animation before destroy
        Destroy(gameObject);
    }
}
