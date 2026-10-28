using UnityEngine;

/// <summary>Explicit player attribution and the attack's original position, including projectile flight.</summary>
public readonly struct PlayerDamageContext
{
    public readonly bool IsPlayerDamage;
    public readonly Vector3 Origin;
    public readonly KillScoreWeapon Weapon;
    private PlayerDamageContext(Vector3 origin, KillScoreWeapon weapon)
    { IsPlayerDamage = true; Origin = origin; Weapon = weapon; }
    public static PlayerDamageContext FromPlayer(Vector3 origin, KillScoreWeapon weapon = KillScoreWeapon.Other)
        => new PlayerDamageContext(origin, weapon);
}
