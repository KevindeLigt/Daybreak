using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RootHeartPickup : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CurseObjectiveController objectiveController;
    [SerializeField] private GameObject visualRoot;

    private Collider pickupCollider;
    private bool collected;

    public bool IsCollected => collected;

    private void Awake()
    {
        pickupCollider = GetComponent<Collider>();
        pickupCollider.isTrigger = true;

        if (visualRoot == null)
            visualRoot = gameObject;
    }

    public void Initialize(CurseObjectiveController controller)
    {
        objectiveController = controller;
    }

    public void SetAvailable(bool available)
    {
        collected = false;

        if (visualRoot != null && visualRoot != gameObject)
            visualRoot.SetActive(available);

        if (pickupCollider != null)
            pickupCollider.enabled = available;

        gameObject.SetActive(available);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected || !other.CompareTag("Player"))
            return;

        if (objectiveController == null)
            objectiveController = FindObjectOfType<CurseObjectiveController>();

        if (objectiveController == null)
        {
            Debug.LogWarning($"{name} could not find a CurseObjectiveController.");
            return;
        }

        if (!objectiveController.TryCollectHeart(this))
            return;

        collected = true;

        gameObject.SetActive(false);
    }
}
