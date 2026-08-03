using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class MapRegion : MonoBehaviour
{
    [Header("Region Identity")]
    public string regionName = "Region";

    [Tooltip("Enable this only on the final region if cleansing it should end the complete run.")]
    [SerializeField] private bool completeRunWhenFinished = false;

    [Header("Runtime State")]
    [Tooltip("The player may enter and explore this region.")]
    public bool isUnlocked = false;

    [Tooltip("This region is currently allowed to run its combat encounter.")]
    public bool isEncounterActive = false;

    [Tooltip("The region's curse/encounter has been completed.")]
    public bool isCompleted = false;

    [Header("Regional Progression References")]
    [Tooltip("The Curse Objective belonging only to this region.")]
    [SerializeField] private CurseObjectiveController curseObjective;

    [Tooltip("The gate that leads out of this region. It is unlocked when this encounter is completed.")]
    [SerializeField] private UnlockableGate exitGate;

    [SerializeField] private bool unlockExitGateOnCompletion = true;

    [Header("Access Content")]
    [Tooltip("Objects enabled when this region becomes accessible.")]
    public GameObject[] enableWhenUnlocked;

    [Tooltip("Objects disabled when this region becomes accessible, such as blockers or fog walls.")]
    public GameObject[] disableWhenUnlocked;

    [Header("Encounter Runtime")]
    [Tooltip("Optional runtime-only objects enabled while this region's encounter is active. Do not place the MapRegion object itself in this list.")]
    public GameObject[] encounterRuntimeObjects;

    [Header("Completion Content")]
    [Tooltip("Optional objects enabled after this region is completed.")]
    public GameObject[] enableWhenCompleted;

    [Tooltip("Optional objects disabled after this region is completed.")]
    public GameObject[] disableWhenCompleted;

    [Header("Enemy Spawners In This Region")]
    public EnemySpawner[] regionSpawners;

    [Header("Optional Events")]
    public UnityEvent onRegionUnlocked;
    public UnityEvent onEncounterActivated;
    public UnityEvent onEncounterDeactivated;
    public UnityEvent onRegionCompleted;

    public string RegionName => regionName;
    public bool IsUnlocked => isUnlocked;
    public bool IsEncounterActive => isEncounterActive;
    public bool IsCompleted => isCompleted;
    public bool CompleteRunWhenFinished => completeRunWhenFinished;
    public CurseObjectiveController CurseObjective => curseObjective;
    public UnlockableGate ExitGate => exitGate;

    /// <summary>
    /// Applies the Inspector state when the scene starts.
    /// RegionManager calls this before choosing the starting combat region.
    /// </summary>
    public void InitializeState()
    {
        if (isCompleted)
        {
            isUnlocked = true;
            isEncounterActive = false;
        }

        ApplyUnlockState();
        ApplyCompletionState();
        ApplyEncounterState();
    }

    /// <summary>
    /// Makes the region accessible, but does not automatically start combat.
    /// </summary>
    public bool UnlockRegion()
    {
        if (isUnlocked)
        {
            ApplyUnlockState();
            return false;
        }

        isUnlocked = true;
        ApplyUnlockState();
        onRegionUnlocked?.Invoke();

        Debug.Log($"Region unlocked: {regionName}");
        return true;
    }

    /// <summary>
    /// Backwards-compatible alias for older scene events or scripts.
    /// New progression code should call RegionManager.UnlockRegion instead.
    /// </summary>
    public void Unlock()
    {
        UnlockRegion();
    }

    /// <summary>
    /// Enables this region's encounter runtime and enemy spawners.
    /// </summary>
    public bool ActivateEncounter()
    {
        if (isCompleted)
        {
            Debug.LogWarning($"Cannot activate completed region: {regionName}");
            return false;
        }

        if (!isUnlocked)
            UnlockRegion();

        bool changed = !isEncounterActive;
        isEncounterActive = true;
        ApplyEncounterState();

        if (changed)
        {
            onEncounterActivated?.Invoke();
            Debug.Log($"Encounter activated: {regionName}");
        }

        return true;
    }

    /// <summary>
    /// Stops this region from producing enemies without removing access to it.
    /// </summary>
    public void DeactivateEncounter()
    {
        bool changed = isEncounterActive;
        isEncounterActive = false;
        ApplyEncounterState();

        if (changed)
        {
            onEncounterDeactivated?.Invoke();
            Debug.Log($"Encounter deactivated: {regionName}");
        }
    }

    /// <summary>
    /// Permanently completes this regional encounter, disables its spawners,
    /// and optionally unlocks the regional exit gate.
    /// </summary>
    public bool CompleteEncounter()
    {
        if (isCompleted)
            return false;

        isUnlocked = true;
        isCompleted = true;
        isEncounterActive = false;

        ApplyUnlockState();
        ApplyEncounterState();
        ApplyCompletionState();

        if (unlockExitGateOnCompletion)
        {
            if (exitGate != null)
                exitGate.UnlockGate();
            else if (!completeRunWhenFinished)
                Debug.LogWarning($"Region {regionName} completed, but it has no Exit Gate assigned.");
        }

        onRegionCompleted?.Invoke();
        Debug.Log($"Region completed: {regionName}");
        return true;
    }

    public void SetSpawnersActive(bool active)
    {
        if (regionSpawners == null)
            return;

        foreach (EnemySpawner spawner in regionSpawners)
        {
            if (spawner != null)
                spawner.isActive = active;
        }
    }

    public EnemySpawner[] GetActiveSpawners()
    {
        if (!isUnlocked || !isEncounterActive || isCompleted || regionSpawners == null)
            return new EnemySpawner[0];

        List<EnemySpawner> activeSpawners = new List<EnemySpawner>();

        foreach (EnemySpawner spawner in regionSpawners)
        {
            if (spawner != null && spawner.isActive)
                activeSpawners.Add(spawner);
        }

        return activeSpawners.ToArray();
    }

    private void ApplyUnlockState()
    {
        SetObjectsActive(enableWhenUnlocked, isUnlocked);
        SetObjectsActive(disableWhenUnlocked, !isUnlocked);
    }

    private void ApplyEncounterState()
    {
        bool shouldBeActive = isUnlocked && isEncounterActive && !isCompleted;
        SetObjectsActive(encounterRuntimeObjects, shouldBeActive);
        SetSpawnersActive(shouldBeActive);
    }

    private void ApplyCompletionState()
    {
        SetObjectsActive(enableWhenCompleted, isCompleted);
        SetObjectsActive(disableWhenCompleted, !isCompleted);
    }

    private static void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null)
            return;

        foreach (GameObject obj in objects)
        {
            if (obj != null)
                obj.SetActive(active);
        }
    }
}
