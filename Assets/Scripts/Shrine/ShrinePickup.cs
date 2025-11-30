using UnityEngine;

public class ShrinePickup : MonoBehaviour
{
    public ShrineType shrineType;

    public float healthBonus = 20f;
    public float damageBonus = 5f;

    private bool collected = false;

    void OnTriggerEnter(Collider other)
    {
        if (collected) return;

        if (other.TryGetComponent(out PlayerHealth playerHealth))
        {
            collected = true;

            if (shrineType == ShrineType.Health)
            {
                playerHealth.IncreaseMaxHealth(healthBonus);
            }
            else if (shrineType == ShrineType.Damage)
            {
                PlayerWeaponStats.Instance.AddDamageBonus(damageBonus);
            }

            // Play sound, particles, flash
            // TODO: hook VFX

            Destroy(gameObject);
        }
    }
}
