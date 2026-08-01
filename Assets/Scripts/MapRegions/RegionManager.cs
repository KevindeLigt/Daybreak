using UnityEngine;

public class RegionManager : MonoBehaviour
{
    public static RegionManager Instance { get; private set; }

    [Header("Regions")]
    public MapRegion[] allRegions;

    [Tooltip("The region whose encounter should be active when the scene begins.")]
    [SerializeField] private MapRegion startingRegion;

    [SerializeField] private bool automaticallyActivateStartingRegion = true;

    public MapRegion CurrentRegion { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        InitializeRegions();
        ResolveStartingRegion();
    }

    private void InitializeRegions()
    {
        if (allRegions == null)
            allRegions = new MapRegion[0];

        foreach (MapRegion region in allRegions)
        {
            if (region != null)
                region.InitializeState();
        }
    }

    private void ResolveStartingRegion()
    {
        MapRegion configuredActiveRegion = FindConfiguredActiveRegion();
        CurrentRegion = configuredActiveRegion;

        if (!automaticallyActivateStartingRegion)
            return;

        MapRegion regionToActivate = startingRegion;

        if (regionToActivate == null)
            regionToActivate = configuredActiveRegion;

        if (regionToActivate == null)
            regionToActivate = FindFirstUnlockedIncompleteRegion();

        if (regionToActivate == null)
        {
            Debug.LogWarning("RegionManager could not find a starting region. Assign Starting Region or mark one region as unlocked and encounter-active.");
            return;
        }

        ActivateRegion(regionToActivate);
    }

    /// <summary>
    /// Makes a region accessible without starting its combat encounter.
    /// </summary>
    public bool UnlockRegion(MapRegion region)
    {
        if (region == null)
        {
            Debug.LogWarning("RegionManager was asked to unlock a null region.");
            return false;
        }

        return region.UnlockRegion();
    }

    /// <summary>
    /// Makes this the only active combat region.
    /// Other regional encounters and spawners are deactivated first.
    /// </summary>
    public bool ActivateRegion(MapRegion region)
    {
        if (region == null)
        {
            Debug.LogWarning("RegionManager was asked to activate a null region.");
            return false;
        }

        if (region.IsCompleted)
        {
            Debug.LogWarning($"RegionManager cannot activate completed region: {region.RegionName}");
            return false;
        }

        DeactivateAllOtherRegions(region);
        region.UnlockRegion();

        if (!region.ActivateEncounter())
            return false;

        CurrentRegion = region;
        Debug.Log($"Current combat region: {region.RegionName}");
        return true;
    }

    /// <summary>
    /// Used by gates or progression logic to move combat ownership to another region.
    /// </summary>
    public bool TransitionToRegion(MapRegion nextRegion)
    {
        return ActivateRegion(nextRegion);
    }

    public bool CompleteCurrentRegion()
    {
        if (CurrentRegion == null)
        {
            Debug.LogWarning("RegionManager has no current region to complete.");
            return false;
        }

        return CompleteRegion(CurrentRegion);
    }

    public bool CompleteRegion(MapRegion region)
    {
        if (region == null)
        {
            Debug.LogWarning("RegionManager was asked to complete a null region.");
            return false;
        }

        return region.CompleteEncounter();
    }

    public bool IsRegionUnlocked(string regionName)
    {
        if (allRegions == null)
            return false;

        foreach (MapRegion region in allRegions)
        {
            if (region != null && region.RegionName == regionName)
                return region.IsUnlocked;
        }

        return false;
    }

    /// <summary>
    /// Returns spawn points only from the current active combat region.
    /// </summary>
    public EnemySpawner[] GetCurrentRegionSpawners()
    {
        if (CurrentRegion == null)
            return new EnemySpawner[0];

        return CurrentRegion.GetActiveSpawners();
    }

    /// <summary>
    /// Backwards-compatible alias. It now returns spawners only from CurrentRegion.
    /// </summary>
    public EnemySpawner[] GetActiveSpawners()
    {
        return GetCurrentRegionSpawners();
    }

    private void DeactivateAllOtherRegions(MapRegion regionToKeep)
    {
        if (allRegions == null)
            return;

        foreach (MapRegion region in allRegions)
        {
            if (region != null && region != regionToKeep)
                region.DeactivateEncounter();
        }
    }

    private MapRegion FindConfiguredActiveRegion()
    {
        if (allRegions == null)
            return null;

        foreach (MapRegion region in allRegions)
        {
            if (region != null && region.IsEncounterActive && !region.IsCompleted)
                return region;
        }

        return null;
    }

    private MapRegion FindFirstUnlockedIncompleteRegion()
    {
        if (allRegions == null)
            return null;

        foreach (MapRegion region in allRegions)
        {
            if (region != null && region.IsUnlocked && !region.IsCompleted)
                return region;
        }

        return null;
    }
}
