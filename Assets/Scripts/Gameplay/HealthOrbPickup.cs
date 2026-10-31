using UnityEngine;

public class HealthOrbPickup : MonoBehaviour
{
    public float healAmount = 5f;
    public float pickupRadius = 2f;
    public float floatHeight = 0.5f;
    public float floatSpeed = 2f;

    private Vector3 startPos;
    private int playerMask;
    private bool collected;

    void Start()
    {
        startPos = transform.position;
        playerMask = LayerMask.GetMask("Player");
    }

    void Update()
    {
        if (collected || SceneLoader.IsInputBlocked) return;
        // simple float animation
        transform.position = startPos + Vector3.up * (Mathf.Sin(Time.time * floatSpeed) * floatHeight);

        // auto-collect if player walks near
        if (Physics.CheckSphere(transform.position, pickupRadius, playerMask))
        {
            TryHeal();
        }
    }

    void TryHeal()
    {
        if (collected) return;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || !player.TryGetComponent(out PlayerHealth hp) || !hp.TryHeal(healAmount))
            return;

        collected = true;
        Destroy(gameObject);
    }
}
