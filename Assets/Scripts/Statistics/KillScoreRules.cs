using System;

[Serializable]
public sealed class KillScoreTier
{
    public int killsRequired;
    public int multiplier;
    public int musicLayers;
    public KillScoreTier() { }
    public KillScoreTier(int kills, int value, int layers)
    { killsRequired = kills; multiplier = value; musicLayers = layers; }
}

[Serializable]
public sealed class KillScoreSettings
{
    public KillScoreTier[] tiers =
    {
        new KillScoreTier(3, 2, 1), new KillScoreTier(6, 3, 2),
        new KillScoreTier(10, 4, 3), new KillScoreTier(15, 5, 4)
    };
    public int eviscerationBonus = 75;
    public float closeRangeMeters = 5f;
    public int closeRangeBonus = 25;
    public float longRangeMeters = 20f;
    public int longRangeBonus = 50;
    public float rapidKillWindow = 1.5f;
    public int doubleKillBonus = 50;
    public int tripleKillBonus = 100;
    public int furtherMultiKillBonus = 100;

    public void GetTier(int streak, out int multiplier, out int musicLayers)
    {
        multiplier = 1; musicLayers = 0;
        if (tiers == null) return;
        foreach (KillScoreTier tier in tiers)
        {
            if (tier == null || tier.killsRequired <= 0 || streak < tier.killsRequired) continue;
            multiplier = Math.Max(multiplier, tier.multiplier);
            musicLayers = Math.Max(musicLayers, Math.Max(0, tier.musicLayers));
        }
    }

    public int NextMilestone(int streak)
    {
        int next = int.MaxValue;
        if (tiers != null) foreach (KillScoreTier tier in tiers)
            if (tier != null && tier.killsRequired > streak) next = Math.Min(next, tier.killsRequired);
        return next == int.MaxValue ? -1 : next;
    }

    public float TierProgress(int streak)
    {
        int next = NextMilestone(streak);
        if (next < 0) return 1f;
        int previous = 0;
        if (tiers != null) foreach (KillScoreTier tier in tiers)
            if (tier != null && tier.killsRequired > 0 && tier.killsRequired <= streak)
                previous = Math.Max(previous, tier.killsRequired);
        return (float)(streak - previous) / Math.Max(1, next - previous);
    }
}

public enum KillScoreRange { Normal, Close, Long }
public enum KillScoreWeapon { Other, Shotgun, Crossbow, ShoulderRam }
public enum ScoredRunResult { Completed, Died, Abandoned }

public struct EnemyKillInfo
{
    public string enemyType;
    public int basePoints;
    public bool eviscerated, hasDistance;
    public float distance;
    public KillScoreWeapon weapon;
}

public struct KillScoreAward
{
    public bool valid;
    public long points;
    public int multiplier, streak, rapidKills;
    public int basePoints, eviscerationBonus, rangeBonus, rapidBonus;
    public bool eviscerated;
    public KillScoreRange range;

    public string Label
    {
        get
        {
            string label = rapidKills == 2 ? "DOUBLE KILL" : rapidKills == 3 ? "TRIPLE KILL" :
                rapidKills > 3 ? rapidKills + " KILLS" : string.Empty;
            if (eviscerated) label = Append(label, "EVISCERATE");
            if (range == KillScoreRange.Close) label = Append(label, "CLOSE RANGE");
            if (range == KillScoreRange.Long) label = Append(label, "LONG RANGE");
            return label.Length == 0 ? "KILL" : label;
        }
    }
    private static string Append(string a, string b) => a.Length == 0 ? b : a + " + " + b;
}

/// <summary>Live run state. Kept by the persistent stats manager, not serialized into the lifetime profile.</summary>
public sealed class KillScoreRun
{
    public bool initialized, active;
    public long score;
    public int kills, streak, bestStreak, highestMultiplier = 1, rapidKills;
    public float lastKillTime;
    public bool hasRecentKill;
}

/// <summary>Pure scoring rules: no Unity scene, audio, HUD or disk dependencies.</summary>
public static class KillScoreCalculator
{
    public static KillScoreAward Award(KillScoreRun run, KillScoreSettings settings, EnemyKillInfo kill, float time)
    {
        if (run == null || !run.active || settings == null) return default;
        run.kills = Increment(run.kills);
        run.streak = Increment(run.streak);
        run.bestStreak = Math.Max(run.bestStreak, run.streak);
        float elapsed = time - run.lastKillTime;
        run.rapidKills = run.hasRecentKill && elapsed >= 0f && elapsed <= Math.Max(0f, settings.rapidKillWindow)
            ? Increment(run.rapidKills) : 1;
        run.hasRecentKill = true;
        run.lastKillTime = time;
        settings.GetTier(run.streak, out int multiplier, out _);
        run.highestMultiplier = Math.Max(run.highestMultiplier, multiplier);
        var award = new KillScoreAward
        {
            valid = true, multiplier = multiplier, streak = run.streak, rapidKills = run.rapidKills,
            basePoints = Math.Max(0, kill.basePoints), eviscerated = kill.eviscerated,
            eviscerationBonus = kill.eviscerated ? Math.Max(0, settings.eviscerationBonus) : 0,
            range = KillScoreRange.Normal
        };
        if (kill.hasDistance && !float.IsNaN(kill.distance) && !float.IsInfinity(kill.distance) && kill.distance >= 0f)
        {
            if (kill.distance <= Math.Max(0f, settings.closeRangeMeters))
            { award.range = KillScoreRange.Close; award.rangeBonus = Math.Max(0, settings.closeRangeBonus); }
            else if (kill.distance >= Math.Max(Math.Max(0f, settings.closeRangeMeters) + .01f, settings.longRangeMeters))
            { award.range = KillScoreRange.Long; award.rangeBonus = Math.Max(0, settings.longRangeBonus); }
        }
        award.rapidBonus = run.rapidKills == 2 ? Math.Max(0, settings.doubleKillBonus) :
            run.rapidKills == 3 ? Math.Max(0, settings.tripleKillBonus) :
            run.rapidKills > 3 ? Math.Max(0, settings.furtherMultiKillBonus) : 0;
        long subtotal = (long)award.basePoints + award.eviscerationBonus + award.rangeBonus + award.rapidBonus;
        award.points = subtotal > long.MaxValue / multiplier ? long.MaxValue : subtotal * multiplier;
        run.score = Add(run.score, award.points);
        return award;
    }

    public static bool BreakStreak(KillScoreRun run)
    {
        if (run == null) return false;
        bool hadStreak = run.streak > 0;
        run.streak = run.rapidKills = 0;
        run.hasRecentKill = false;
        return hadStreak;
    }
    public static int Increment(int value) => value >= int.MaxValue ? int.MaxValue : Math.Max(0, value) + 1;
    public static long Add(long value, long amount) => amount > long.MaxValue - Math.Max(0L, value)
        ? long.MaxValue : Math.Max(0L, value) + Math.Max(0L, amount);
}
