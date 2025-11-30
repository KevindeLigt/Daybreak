using UnityEngine;

public enum ShrineType
{
    Health,
    Damage
}

public class ShrineSpawnPoint : MonoBehaviour
{
    public ShrineType shrineType;   // What shrine this location is allowed to spawn
}
