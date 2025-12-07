using UnityEngine;
using System.Collections.Generic;

public class RegionManager : MonoBehaviour
{
    public static RegionManager Instance;

    public MapRegion[] allRegions;

    void Awake()
    {
        Instance = this;
    }

    public void UnlockRegion(MapRegion region)
    {
        region.Unlock();
    }

    public bool IsRegionUnlocked(string regionName)
    {
        foreach (var region in allRegions)
            if (region.regionName == regionName)
                return region.isUnlocked;

        return false;
    }

    public EnemySpawner[] GetActiveSpawners()
    {
        List<EnemySpawner> list = new List<EnemySpawner>();

        foreach (var region in allRegions)
            if (region.isUnlocked)
                foreach (var sp in region.regionSpawners)
                    if (sp && sp.isActive)
                        list.Add(sp);

        return list.ToArray();
    }

}
