using UnityEngine;

public class PlayerWeaponStats : MonoBehaviour
{
    public static PlayerWeaponStats Instance { get; private set; }

    [Header("Damage Settings")]
    public float baseDamageMultiplier = 1f;     // always 1
    public float bonusDamageFlat = 0f;          // shrines add to this

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // ===== Increase Damage from Shrines =====
    public void AddDamageBonus(float amount)
    {
        bonusDamageFlat += amount;

        UIManager.Instance?.SetStatusEffect(
            "DamageBonus",
            $"Damage +{bonusDamageFlat}"
        );
    }


    // ===== Get final damage per pellet =====
    public float CalculatePelletDamage(float rawDamage)
    {
        return rawDamage * baseDamageMultiplier + bonusDamageFlat;
    }

    // ===== Reset stats for a new run =====
    public void ResetRunStats()
    {
        bonusDamageFlat = 0f;
    }
}
