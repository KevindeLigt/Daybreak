using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("Drop Settings")]
    public GameObject healthOrbPrefab;
    [Range(0f, 1f)] public float healthOrbDropChance = 0.10f; // 10% default


    public Renderer enemyRenderer;
    public Color hitColor = Color.white;
    public float flashDuration = 0.1f;

    private Color originalColor;
    private EnemyRagdollController ragdoll;

    private bool isDead = false;
    public bool IsDead => isDead;

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
        KillComboSystem.Instance.OnEnemyKilled();

        ragdoll.EnableRagdoll();

        foreach (var rb in ragdoll.ragdollBodies)
            rb.AddForce(force * 2f, ForceMode.Impulse);

        TryDropHealthOrb();
        Destroy(gameObject, 10f);

    }

    private void TryDropHealthOrb()
    {
        if (healthOrbPrefab == null)
            return;

        if (Random.value <= healthOrbDropChance)
        {
            Instantiate(
                healthOrbPrefab,
                transform.position + Vector3.up * 0.5f,
                Quaternion.identity
            );
        }
    }

}