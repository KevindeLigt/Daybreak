using UnityEngine;

public class UnlockableGate : MonoBehaviour, IInteractable
{
    public MapRegion connectedRegion;
    public int cost = 0;

    public string InteractionPrompt => "Press E to Unlock";
    public bool CanInteract => connectedRegion != null;

    public void Interact()
    {
        TryUnlock();
    }

    public void TryUnlock()
    {
        if (!connectedRegion)
            return;

        connectedRegion.Unlock();
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            InteractionManager.Instance?.RegisterInteractable(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            InteractionManager.Instance?.UnregisterInteractable(this);
    }
}
