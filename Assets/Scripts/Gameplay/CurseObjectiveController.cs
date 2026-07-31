using UnityEngine;
using UnityEngine.Events;

public class CurseObjectiveController : MonoBehaviour
{
    [Header("Objective References")]
    [SerializeField] private CurseAnchor curseAnchor;
    [SerializeField] private RootHeartPickup[] rootHeartPickups;

    [Tooltip("Optional visual attached to the player or camera while a heart is carried.")]
    [SerializeField] private GameObject carriedHeartVisual;

    [Header("Phase 2 Behaviour")]
    [SerializeField] private bool activatePickupsSequentially = true;
    [SerializeField, Min(1)] private int requiredHearts = 3;

    [Header("Temporary Objective UI")]
    [SerializeField] private bool useStatusEffectUI = true;
    [SerializeField] private string objectiveUIKey = "CurseObjective";

    [Header("Events")]
    public UnityEvent onHeartCollected;
    public UnityEvent onHeartDeposited;
    public UnityEvent onObjectiveCompleted;

    private bool hasRootHeart;
    private bool isCompleted;
    private int heartsDeposited;

    public bool HasRootHeart => hasRootHeart;
    public bool IsCompleted => isCompleted;
    public int HeartsDeposited => heartsDeposited;
    public int RequiredHearts => requiredHearts;

    private void Start()
    {
        if (curseAnchor == null)
            curseAnchor = FindObjectOfType<CurseAnchor>();

        if (curseAnchor != null)
            curseAnchor.Initialize(this);
        else
            Debug.LogWarning("CurseObjectiveController has no CurseAnchor assigned.");

        requiredHearts = Mathf.Max(1, requiredHearts);

        if (rootHeartPickups == null)
            rootHeartPickups = new RootHeartPickup[0];

        for (int i = 0; i < rootHeartPickups.Length; i++)
        {
            if (rootHeartPickups[i] == null)
                continue;

            rootHeartPickups[i].Initialize(this);

            if (activatePickupsSequentially)
                rootHeartPickups[i].SetAvailable(i == 0);
        }

        SetCarriedVisual(false);
        UpdateObjectiveUI();
    }

    public bool TryCollectHeart(RootHeartPickup pickup)
    {
        if (isCompleted || hasRootHeart || pickup == null)
            return false;

        hasRootHeart = true;
        SetCarriedVisual(true);

        Debug.Log("Root Heart collected. Return it to the Curse Anchor.");

        onHeartCollected?.Invoke();
        UpdateObjectiveUI();
        InteractionManager.Instance?.RefreshPrompt();
        return true;
    }

    public bool TryDepositHeart()
    {
        if (isCompleted || !hasRootHeart)
            return false;

        hasRootHeart = false;
        SetCarriedVisual(false);

        int sealIndex = heartsDeposited;
        heartsDeposited++;

        curseAnchor?.BreakSeal(sealIndex);
        onHeartDeposited?.Invoke();

        Debug.Log($"Root Heart deposited: {heartsDeposited}/{requiredHearts}");

        if (heartsDeposited >= requiredHearts)
        {
            CompleteObjective();
        }
        else if (activatePickupsSequentially)
        {
            ActivateNextPickup();
        }

        UpdateObjectiveUI();
        InteractionManager.Instance?.RefreshPrompt();
        return true;
    }

    private void ActivateNextPickup()
    {
        int nextIndex = heartsDeposited;

        if (nextIndex < 0 || nextIndex >= rootHeartPickups.Length)
        {
            Debug.LogWarning("No Root Heart Pickup is assigned for the next objective step.");
            return;
        }

        if (rootHeartPickups[nextIndex] != null)
            rootHeartPickups[nextIndex].SetAvailable(true);
    }

    private void CompleteObjective()
    {
        if (isCompleted)
            return;

        isCompleted = true;
        curseAnchor?.CompleteAnchor();
        onObjectiveCompleted?.Invoke();

        Debug.Log("Curse objective completed.");
    }

    private void SetCarriedVisual(bool active)
    {
        if (carriedHeartVisual != null)
            carriedHeartVisual.SetActive(active);
    }

    private void UpdateObjectiveUI()
    {
        if (!useStatusEffectUI || UIManager.Instance == null)
            return;

        if (isCompleted)
        {
            UIManager.Instance.RemoveStatusEffect(objectiveUIKey);
            return;
        }

        string text = hasRootHeart
            ? "Return the Root Heart to the Curse Anchor"
            : $"Root Hearts: {heartsDeposited}/{requiredHearts}";

        UIManager.Instance.SetStatusEffect(objectiveUIKey, text);
    }
}
