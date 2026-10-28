using UnityEngine;

public class UnlockableGate : MonoBehaviour, IInteractable
{
    public enum DestinationMode { InSceneRegion, LevelExit }
    [Tooltip("Level Exit opens the barrier only. Put a separate LevelExit trigger behind it.")]
    [SerializeField] private DestinationMode destinationMode = DestinationMode.InSceneRegion;
    public bool OpensForLevelExit => destinationMode == DestinationMode.LevelExit;

    [Header("Region Progression")]
    [SerializeField] private MapRegion connectedRegion;

    [Tooltip("For an objective-locked exit, turn this off. MapRegion unlocks the gate after cleansing.")]
    [SerializeField] private bool startsUnlocked = true;

    [Header("Opening Behaviour")]
    [SerializeField] private bool destroyOnOpen = true;

    [Tooltip("Optional gate mesh/root object to hide when Destroy On Open is disabled.")]
    [SerializeField] private GameObject gateVisual;

    [Tooltip("Optional solid collider to disable when Destroy On Open is disabled. Do not assign the interaction trigger unless that is intentional.")]
    [SerializeField] private Collider blockingCollider;

    [Header("Interaction Text")]
    [SerializeField] private string unlockedPrompt = "Press E to Open";
    [SerializeField] private string lockedPrompt = "The curse seals this gate";

    private bool isUnlocked;
    private bool isOpen;

    public MapRegion ConnectedRegion => connectedRegion;
    public bool IsUnlocked => isUnlocked;
    public bool IsOpen => isOpen;

    public string InteractionPrompt
    {
        get
        {
            if (isOpen)
                return string.Empty;

            if (!isUnlocked)
                return lockedPrompt;

            if (!OpensForLevelExit && connectedRegion == null)
                return "Gate has no connected region";

            return unlockedPrompt;
        }
    }

    public bool CanInteract =>
        !isOpen &&
        isUnlocked &&
        !SceneLoader.IsInputBlocked &&
        (OpensForLevelExit || connectedRegion != null);

    private void Awake()
    {
        isUnlocked = startsUnlocked;
    }

    public void Interact()
    {
        TryOpen();
    }

    public bool TryOpen()
    {
        if (!CanInteract)
        {
            InteractionManager.Instance?.RefreshPrompt();
            return false;
        }

        if (!OpensForLevelExit && RegionManager.Instance == null)
        {
            Debug.LogError($"{name} cannot open because no RegionManager exists in the scene.");
            return false;
        }

        if (!OpensForLevelExit && !RegionManager.Instance.TransitionToRegion(connectedRegion))
            return false;

        OpenGate();
        return true;
    }

    public void UnlockGate()
    {
        if (isOpen)
            return;

        isUnlocked = true;
        InteractionManager.Instance?.RefreshPrompt();
        Debug.Log($"Gate unlocked: {name}");
    }

    public void LockGate()
    {
        if (isOpen)
            return;

        isUnlocked = false;
        InteractionManager.Instance?.RefreshPrompt();
    }

    public void OpenGate()
    {
        if (isOpen)
            return;

        isOpen = true;
        InteractionManager.Instance?.UnregisterInteractable(this);

        if (destroyOnOpen)
        {
            Destroy(gameObject);
            return;
        }

        if (blockingCollider != null)
            blockingCollider.enabled = false;

        if (gateVisual != null)
            gateVisual.SetActive(false);

        InteractionManager.Instance?.RefreshPrompt();
        Debug.Log($"Gate opened: {name}");
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
