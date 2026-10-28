using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Run values only. Scene objects, enemies, UI and navigation stay in their own scenes.</summary>
public sealed class RunState : MonoBehaviour
{
    public static RunState Instance { get; private set; }
    public string FirstLevelScene { get; private set; } = "Level_01_Church";
    public int GlobalWave { get; private set; }
    public bool AwaitingArrival => !string.IsNullOrEmpty(pendingScene);
    public bool HasContinuation => incoming != null;
    public IReadOnlyList<string> CompletedLevels => completedLevels;

    private string pendingScene;
    private Snapshot incoming;
    private float initialMaxHealth;
    private readonly List<string> completedLevels = new List<string>();

    private sealed class Snapshot
    {
        public float health, maxHealth, baseDamage, bonusDamage, ramCooldown;
        public int wave, selectedSlot;
        public readonly List<WeaponSnapshot> weapons = new List<WeaponSnapshot>();
    }

    private sealed class WeaponSnapshot
    {
        public int slot, ammo;
        public string type;
        public bool leftBarrelNext;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public static RunState GetOrCreate()
    {
        if (Instance == null) new GameObject("RunState").AddComponent<RunState>();
        return Instance;
    }

    public void PrepareNewRun(string firstLevel)
    {
        PlayerStatsManager.GetOrCreate().BeginRun();
        FirstLevelScene = firstLevel;
        GlobalWave = 0;
        initialMaxHealth = 0f;
        completedLevels.Clear();
        incoming = null;
        pendingScene = firstLevel;
    }

    public void EndRun()
    {
        PlayerStatsManager.Instance?.FinishRun(ScoredRunResult.Abandoned);
        pendingScene = null;
        incoming = null;
        GlobalWave = 0;
        initialMaxHealth = 0f;
        completedLevels.Clear();
    }

    public bool PrepareContinuation(string destination, PlayerHealth player,
        WeaponManager manager, int wave, string completedLevel, out string error)
    {
        error = null;
        if (player == null || player.currentHealth <= 0f || manager == null ||
            PlayerWeaponStats.Instance == null)
        {
            error = "Level exit needs a living PlayerHealth, WeaponManager and PlayerWeaponStats.";
            return false;
        }
        if (!manager.IsValidSlot(manager.CurrentSlot))
        {
            error = "The player has no valid equipped weapon to carry into the next level.";
            return false;
        }

        var state = new Snapshot
        {
            health = player.currentHealth,
            maxHealth = player.maxHealth,
            baseDamage = PlayerWeaponStats.Instance.baseDamageMultiplier,
            bonusDamage = PlayerWeaponStats.Instance.bonusDamageFlat,
            wave = Mathf.Max(0, wave),
            selectedSlot = manager.CurrentSlot
        };
        ShoulderRam ram = player.GetComponent<ShoulderRam>();
        state.ramCooldown = ram != null ? ram.RemainingCooldown : 0f;
        for (int i = 0; i < manager.weaponSlots.Length; i++)
        {
            WeaponBase weapon = manager.weaponSlots[i];
            if (weapon == null) continue;
            var saved = new WeaponSnapshot { slot = i, type = weapon.GetType().FullName, ammo = -1 };
            if (weapon is DoubleBarrelShotgunController shotgun)
            {
                saved.ammo = shotgun.LoadedShells;
                saved.leftBarrelNext = shotgun.LeftBarrelNext;
            }
            else if (weapon is CrossbowController crossbow) saved.ammo = crossbow.boltLoaded ? 1 : 0;
            state.weapons.Add(saved);
        }
        incoming = state;
        pendingScene = destination;
        if (!string.IsNullOrEmpty(completedLevel) && !completedLevels.Contains(completedLevel))
            completedLevels.Add(completedLevel);
        return true;
    }

    /// <summary>Called after destination Start methods; validates the full loadout before applying any values.</summary>
    public bool RestoreLevel(Scene scene, string firstLevel, PlayerHealth player,
        WeaponManager manager, out string error)
    {
        error = null;
        if (player == null || manager == null || PlayerWeaponStats.Instance == null)
        {
            error = "Assign PlayerHealth and WeaponManager; include PlayerWeaponStats in this level.";
            return false;
        }
        if (AwaitingArrival && !SceneLoader.MatchesScene(pendingScene, scene))
        {
            error = "Loaded level does not match the queued run destination: " + pendingScene;
            return false;
        }
        if (incoming != null)
        {
            if (!manager.IsValidSlot(incoming.selectedSlot))
            {
                error = "Destination is missing the equipped weapon slot. Use the same player/loadout prefab in both levels.";
                return false;
            }
            foreach (WeaponSnapshot saved in incoming.weapons)
            {
                if (!manager.IsValidSlot(saved.slot) ||
                    manager.weaponSlots[saved.slot].GetType().FullName != saved.type)
                {
                    error = "Weapon slots differ between levels. Missing/mismatched slot: " + saved.slot;
                    return false;
                }
            }

            player.maxHealth = Mathf.Max(1f, incoming.maxHealth);
            player.currentHealth = Mathf.Clamp(incoming.health, 0f, player.maxHealth);
            PlayerWeaponStats.Instance.baseDamageMultiplier = incoming.baseDamage;
            PlayerWeaponStats.Instance.bonusDamageFlat = incoming.bonusDamage;
            GlobalWave = incoming.wave;
            foreach (WeaponSnapshot saved in incoming.weapons)
            {
                WeaponBase weapon = manager.weaponSlots[saved.slot];
                if (weapon is DoubleBarrelShotgunController shotgun)
                    shotgun.RestoreLoadedState(saved.ammo, saved.leftBarrelNext);
                else if (weapon is CrossbowController crossbow) crossbow.boltLoaded = saved.ammo > 0;
            }
            manager.RestoreSlot(incoming.selectedSlot);
            ShoulderRam ram = player.GetComponent<ShoulderRam>();
            if (ram != null) ram.RestoreCooldown(incoming.ramCooldown);
        }
        else
        {
            // Direct Editor Play is a new run; a queued fresh run already chose its first scene.
            if (!AwaitingArrival)
                PrepareNewRun(string.IsNullOrWhiteSpace(firstLevel) ? scene.path : firstLevel.Trim());
            PlayerWeaponStats.Instance.ResetRunStats();
            GlobalWave = 0;
            initialMaxHealth = player.maxHealth;
        }

        PlayerStatsManager.Instance?.ResetRapidKillWindow();
        UIManager.Instance?.UpdateHealth(player.currentHealth, player.maxHealth);
        UIManager.Instance?.UpdateWave(GlobalWave);
        if (PlayerWeaponStats.Instance.bonusDamageFlat != 0f)
            UIManager.Instance?.SetStatusEffect("DamageBonus", $"Damage +{PlayerWeaponStats.Instance.bonusDamageFlat}");
        if (initialMaxHealth > 0f && player.maxHealth > initialMaxHealth)
            UIManager.Instance?.SetStatusEffect("MaxHealth", $"Max Health +{player.maxHealth - initialMaxHealth}");
        foreach (WeaponBase weapon in manager.weaponSlots)
            if (weapon is DoubleBarrelShotgunController shotgun) UIManager.Instance?.UpdateShotgunAmmo(shotgun.LoadedShells);
        incoming = null;
        pendingScene = null;
        return true;
    }
}
