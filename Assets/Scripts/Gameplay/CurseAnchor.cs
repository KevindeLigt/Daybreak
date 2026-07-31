using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CurseAnchor : MonoBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private CurseObjectiveController objectiveController;

    [Tooltip("One object per visible seal. The next object is disabled after every deposited heart.")]
    [SerializeField] private GameObject[] sealObjects;

    [Header("Optional Visual States")]
    [SerializeField] private GameObject activeVisuals;
    [SerializeField] private GameObject completedVisuals;

    public string InteractionPrompt
    {
        get
        {
            if (objectiveController == null)
                return string.Empty;

            if (objectiveController.IsCompleted)
                return "Curse destroyed";

            if (objectiveController.HasRootHeart)
                return "Press E to Place Root Heart";

            return "Find a Root Heart";
        }
    }

    public bool CanInteract =>
        objectiveController != null &&
        objectiveController.HasRootHeart &&
        !objectiveController.IsCompleted;

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;

        if (completedVisuals != null)
            completedVisuals.SetActive(false);
    }

    public void Initialize(CurseObjectiveController controller)
    {
        objectiveController = controller;
    }

    public void Interact()
    {
        if (objectiveController == null)
            return;

        objectiveController.TryDepositHeart();
    }

    public void BreakSeal(int sealIndex)
    {
        if (sealIndex < 0 || sealIndex >= sealObjects.Length)
        {
            Debug.LogWarning($"Curse Anchor has no seal at index {sealIndex}.");
            return;
        }

        if (sealObjects[sealIndex] != null)
            sealObjects[sealIndex].SetActive(false);
    }

    public void CompleteAnchor()
    {
        if (activeVisuals != null)
            activeVisuals.SetActive(false);

        if (completedVisuals != null)
            completedVisuals.SetActive(true);

        InteractionManager.Instance?.RefreshPrompt();
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
