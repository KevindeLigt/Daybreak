using UnityEngine;

public class HealthOrbPickup : MonoBehaviour
{
    public float healAmount = 5f;
    public float pickupRadius = 2f;
    public float floatHeight = 0.5f;
    public float floatSpeed = 2f;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // simple float animation
        transform.position = startPos + Vector3.up * (Mathf.Sin(Time.time * floatSpeed) * floatHeight);

        // auto-collect if player walks near
        if (Physics.CheckSphere(transform.position, pickupRadius, LayerMask.GetMask("Player")))
        {
            TryHeal();
        }
    }

    void TryHeal()
    {
        // detect player health
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player && player.TryGetComponent(out PlayerHealth hp))
        {
            hp.currentHealth = Mathf.Min(hp.maxHealth, hp.currentHealth + healAmount);
            UIManager.Instance.UpdateHealth(hp.currentHealth, hp.maxHealth);
            hp.screenFlash?.Flash(new Color(0f, 1f, 0.2f), 0.3f, 0.25f);

        }

        Destroy(gameObject);
    }
}
