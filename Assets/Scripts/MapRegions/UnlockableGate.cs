using UnityEngine;

public class UnlockableGate : MonoBehaviour
{
    public MapRegion connectedRegion;
    public int cost = 0;  // currently always free

    [Header("UI Prompt")]
    public string interactText = "Press E to Unlock";

    private bool playerInRange = false;

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            TryUnlock();
        }
    }

    void TryUnlock()
    {
        if (!connectedRegion) return;

        // When currency exists:
        // if (PlayerRunCurrency.Instance.points < cost) return;

        connectedRegion.Unlock();
        // PlayerRunCurrency.Instance.points -= cost;

        Destroy(gameObject); // gate disappears
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = false;
    }
}
