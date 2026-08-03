using System;
using UnityEngine;
using UnityEngine.Events;

public class CurseObjectiveController : MonoBehaviour
{
    [Header("Objective References")]
    [SerializeField] private CurseAnchor curseAnchor;
    [SerializeField] private RootHeartPickup[] rootHeartPickups;

    [Tooltip("Optional visual attached to the player or camera while a heart is carried.")]
    [SerializeField] private GameObject carriedHeartVisual;

    [Header("Pickup Behaviour")]
    [SerializeField] private bool activatePickupsSequentially = true;

    [Tooltip("When enabled, GameFlowManager releases one Root Heart after each cleared wave. No heart is available when the encounter begins.")]
    [SerializeField] private bool waveControlledPickups = true;

    [SerializeField, Min(1)] private int requiredHearts = 3;

    [Header("Temporary Objective UI")]
    [SerializeField] private bool useStatusEffectUI = true;
    [SerializeField] private string objectiveUIKey = "CurseObjective";

    [Header("Events")]
    public UnityEvent onHeartReleased;
    public UnityEvent onHeartCollected;
    public UnityEvent onHeartDeposited;
    public UnityEvent onObjectiveCompleted;

    /// <summary>
    /// Runtime events used by GameFlowManager. These avoid having to manually
    /// wire the basic wave flow through UnityEvents in every regional prefab.
    /// </summary>
    public event Action<CurseObjectiveController> HeartDepositedRuntime;
    public event Action<CurseObjectiveController> ObjectiveCompletedRuntime;

    private bool hasRootHeart;
    private bool isCompleted;
    private int heartsDeposited;
    private int releasedHeartIndex = -1;

    public bool HasRootHeart => hasRootHeart;
    public bool IsCompleted => isCompleted;
    public bool HasReleasedHeart => releasedHeartIndex >= 0;
    public bool WaveControlledPickups => waveControlledPickups;
    public int HeartsDeposited => heartsDeposited;
    public int RequiredHearts => requiredHearts;

    private void Start()
    {
        if (curseAnchor == null)
            curseAnchor = FindObjectOfType<CurseAnchor>();

        if (curseAnchor != null)
            curseAnchor.Initialize(this);
        else
            Debug.LogWarning($"{name} has no CurseAnchor assigned.");

        requiredHearts = Mathf.Max(1, requiredHearts);

        if (rootHeartPickups == null)
            rootHeartPickups = Array.Empty<RootHeartPickup>();

        for (int i = 0; i < rootHeartPickups.Length; i++)
        {
            RootHeartPickup pickup = rootHeartPickups[i];
            if (pickup == null)
                continue;

            pickup.Initialize(this);

            if (waveControlledPickups)
            {
                // Phase 3: all hearts begin hidden. GameFlowManager releases them.
                pickup.SetAvailable(false);
            }
            else if (activatePickupsSequentially)
            {
                // Backwards-compatible standalone Phase 2 behaviour.
                pickup.SetAvailable(i == 0);
            }
        }

        SetCarriedVisual(false);
        UpdateObjectiveUI();
    }

    private void OnDisable()
    {
        if (useStatusEffectUI && UIManager.Instance != null)
            UIManager.Instance.RemoveStatusEffect(objectiveUIKey);
    }

    /// <summary>
    /// Called by GameFlowManager after a wave is cleared.
    /// Releases the next Root Heart matching the next unbroken seal.
    /// </summary>
    public bool ReleaseNextHeart()
    {
        if (isCompleted || hasRootHeart || releasedHeartIndex >= 0)
            return false;

        int nextIndex = heartsDeposited;
        if (nextIndex < 0 || nextIndex >= rootHeartPickups.Length)
        {
            Debug.LogError(
                $"{name} cannot release Root Heart {nextIndex + 1}. " +
                "Check the Root Heart Pickups array and Required Hearts value.");
            return false;
        }

        RootHeartPickup pickup = rootHeartPickups[nextIndex];
        if (pickup == null)
        {
            Debug.LogError($"{name} has no Root Heart Pickup assigned at index {nextIndex}.");
            return false;
        }

        releasedHeartIndex = nextIndex;
        pickup.SetAvailable(true);

        Debug.Log($"Root Heart {nextIndex + 1} released for {name}.");
        onHeartReleased?.Invoke();
        UpdateObjectiveUI();
        return true;
    }

    public bool TryCollectHeart(RootHeartPickup pickup)
    {
        if (isCompleted || hasRootHeart || pickup == null)
            return false;

        if (waveControlledPickups)
        {
            if (releasedHeartIndex < 0 || releasedHeartIndex >= rootHeartPickups.Length)
                return false;

            if (rootHeartPickups[releasedHeartIndex] != pickup)
                return false;
        }

        hasRootHeart = true;
        releasedHeartIndex = -1;
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

        bool completedNow = heartsDeposited >= requiredHearts;
        if (completedNow)
        {
            CompleteObjective();
        }
        else if (activatePickupsSequentially && !waveControlledPickups)
        {
            // Standalone Phase 2 mode still releases the next pickup immediately.
            ActivateNextPickup();
        }

        Debug.Log($"Root Heart deposited: {heartsDeposited}/{requiredHearts}");

        onHeartDeposited?.Invoke();
        HeartDepositedRuntime?.Invoke(this);

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
        releasedHeartIndex = -1;
        curseAnchor?.CompleteAnchor();

        onObjectiveCompleted?.Invoke();
        ObjectiveCompletedRuntime?.Invoke(this);

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

        string text;

        if (hasRootHeart)
            text = "Return the Root Heart to the Curse Anchor";
        else if (releasedHeartIndex >= 0)
            text = "Retrieve the Root Heart";
        else if (waveControlledPickups)
            text = $"Survive the wave — Root Hearts: {heartsDeposited}/{requiredHearts}";
        else
            text = $"Root Hearts: {heartsDeposited}/{requiredHearts}";

        UIManager.Instance.SetStatusEffect(objectiveUIKey, text);
    }
}
