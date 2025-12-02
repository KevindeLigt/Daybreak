using UnityEngine;

public abstract class WeaponBase : MonoBehaviour
{
    public abstract void OnFire();
    public abstract void OnReload();
    public virtual void OnEquip() { }
    public virtual void OnUnequip() { }
}
