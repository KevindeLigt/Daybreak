using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;

    public Renderer enemyRenderer;
    public Color hitColor = Color.white;
    public float flashDuration = 0.1f;

    private Color originalColor;
    private EnemyRagdollController ragdoll;

    private bool isDead = false;

    void Start()
    {
        currentHealth = maxHealth;
        ragdoll = GetComponent<EnemyRagdollController>();

        if (enemyRenderer == null)
            enemyRenderer = GetComponentInChildren<Renderer>();

        if (enemyRenderer != null)
            originalColor = enemyRenderer.material.color;
    }

    public void TakeDamage(float damage, Vector3 force)
    {
        if (isDead) return;

        currentHealth -= damage;
        if (TryGetComponent(out ZombieAIHybrid ai))
            ai.HitStun(0.15f);


        StartCoroutine(Flash());

        ragdoll.StartCoroutine(ragdoll.TemporaryRagdollBlast(force));

        if (currentHealth <= 0f)
            Die(force);
    }

    private IEnumerator Flash()
    {
        if (enemyRenderer) enemyRenderer.material.color = hitColor;
        yield return new WaitForSeconds(flashDuration);
        if (enemyRenderer) enemyRenderer.material.color = originalColor;
    }


    private void Die(Vector3 force)
    {
        if (isDead) return;
        isDead = true;

        GameFlowManager.Instance?.EnemyDied();

        ragdoll.EnableRagdoll();

        foreach (var rb in ragdoll.ragdollBodies)
            rb.AddForce(force * 2f, ForceMode.Impulse);

        Destroy(gameObject, 10f);
    }
}