using UnityEngine;

public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance;

    private UnlockableGate currentGate;

    void Awake()
    {
        Instance = this;
    }

    public void RegisterGate(UnlockableGate gate)
    {
        currentGate = gate;
        UIManager.Instance.ShowInteractPrompt("Press E to Unlock");
    }

    public void UnregisterGate(UnlockableGate gate)
    {
        if (currentGate == gate)
        {
            currentGate = null;
            UIManager.Instance.HideInteractPrompt();
        }
    }

    public void TryInteract()
    {
        if (currentGate != null)
            currentGate.TryUnlock();
    }
}
