using UnityEngine;

public class UnlockableGate : MonoBehaviour
{
    public MapRegion connectedRegion;
    public int cost = 0;

    public void TryUnlock()
    {
        if (!connectedRegion) return;

        connectedRegion.Unlock();
        Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            InteractionManager.Instance.RegisterGate(this);
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            InteractionManager.Instance.UnregisterGate(this);
    }
}
