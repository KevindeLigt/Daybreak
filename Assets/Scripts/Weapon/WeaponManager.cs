using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>Shotgun input bridge. The name is retained for existing PlayerInput/scene references.</summary>
[DisallowMultipleComponent]
public class WeaponManager : MonoBehaviour
{
    [Tooltip("The player's shotgun. Empty uses the previous slot assignment or searches this hierarchy.")]
    public DoubleBarrelShotgunController shotgun;

    // Unity stores component references in the old slot array. Keep a hidden
    // migration path without retaining WeaponBase or any switching behavior.
    [SerializeField, HideInInspector, FormerlySerializedAs("weaponSlots")]
    private MonoBehaviour[] legacyWeaponSlots;

    public DoubleBarrelShotgunController Shotgun
    {
        get
        {
            if (shotgun != null) return shotgun;
            if (legacyWeaponSlots != null)
            {
                foreach (MonoBehaviour previous in legacyWeaponSlots)
                    if (previous is DoubleBarrelShotgunController found)
                    { shotgun = found; break; }
            }
            if (shotgun == null) shotgun = GetComponentInChildren<DoubleBarrelShotgunController>(true);
            return shotgun;
        }
    }

    private void Start()
    {
        if (Shotgun != null) Shotgun.OnEquip();
        else Debug.LogError("WeaponManager: Assign the player's Shotgun. This scene uses the single-shotgun setup.", this);
    }

    public void OnFire(InputAction.CallbackContext context)
    {
        if (context.performed && !SceneLoader.IsInputBlocked) Shotgun?.OnFire();
    }

    public void OnReload(InputAction.CallbackContext context)
    {
        if (context.performed && !SceneLoader.IsInputBlocked) Shotgun?.OnReload();
    }

    // Harmless until obsolete PlayerInput listeners are removed in the Editor.
    public void OnScrollWheel(InputAction.CallbackContext context) { }
    public void OnWeapon1(InputAction.CallbackContext context) { }
    public void OnWeapon2(InputAction.CallbackContext context) { }
    public void OnWeapon3(InputAction.CallbackContext context) { }
}
