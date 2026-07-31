using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance { get; private set; }

    private MonoBehaviour currentBehaviour;
    private IInteractable currentInteractable;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void RegisterInteractable(MonoBehaviour behaviour)
    {
        if (behaviour == null)
            return;

        IInteractable interactable = behaviour as IInteractable;
        if (interactable == null)
        {
            Debug.LogWarning($"{behaviour.name} was registered for interaction, but does not implement IInteractable.");
            return;
        }

        currentBehaviour = behaviour;
        currentInteractable = interactable;
        RefreshPrompt();
    }

    public void UnregisterInteractable(MonoBehaviour behaviour)
    {
        if (currentBehaviour != behaviour)
            return;

        ClearCurrentInteractable();
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
            TryInteract();
    }

    public void TryInteract()
    {
        if (!HasValidInteractable())
        {
            ClearCurrentInteractable();
            return;
        }

        if (!currentInteractable.CanInteract)
        {
            RefreshPrompt();
            return;
        }

        currentInteractable.Interact();
        RefreshPrompt();
    }

    public void RefreshPrompt()
    {
        if (!HasValidInteractable())
        {
            UIManager.Instance?.HideInteractPrompt();
            return;
        }

        string prompt = currentInteractable.InteractionPrompt;

        if (string.IsNullOrWhiteSpace(prompt))
            UIManager.Instance?.HideInteractPrompt();
        else
            UIManager.Instance?.ShowInteractPrompt(prompt);
    }

    private bool HasValidInteractable()
    {
        return currentBehaviour != null &&
               currentBehaviour.isActiveAndEnabled &&
               currentInteractable != null;
    }

    private void ClearCurrentInteractable()
    {
        currentBehaviour = null;
        currentInteractable = null;
        UIManager.Instance?.HideInteractPrompt();
    }
}
