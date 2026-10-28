[System.Serializable]
public class PlayerStats
{
    public int schemaVersion;
    public long killHighScore;
    public long totalKillScore;
    public int bestKillStreak;
    public int highestMultiplier = 1;
    public int eviscerationKills;
    public int closeRangeKills;
    public int longRangeKills;
    public int doubleKillBonuses;
    public int tripleKillBonuses;
    public int multiKillBonuses;
    public int totalKills;
    public int zombieKills;
    public int skeletonKills;

    public float totalDamageDealt;
    public float totalDamageTaken;

    public int runsCompleted;
    public int deaths;

    public int shotgunShotsFired;
    public int crossbowShotsFired;
    public int crossbowHits;

    public int shrinesUsed;
}
