using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponManager : MonoBehaviour
{
    [Header("Weapons")]
    public WeaponBase[] weaponSlots;      // assign in inspector
    private int currentIndex = 0;

    private WeaponBase Current => weaponSlots[currentIndex];

    void Start()
    {
        // Disable all, then equip first
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] != null)
                weaponSlots[i].OnUnequip();
        }

        if (weaponSlots.Length > 0 && weaponSlots[0] != null)
        {
            currentIndex = 0;
            weaponSlots[currentIndex].OnEquip();
        }
    }

    // Called by Input System
    public void OnFire(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
            Current?.OnFire();
    }

    public void OnReload(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
            Current?.OnReload();
    }

    public void OnScrollWheel(InputAction.CallbackContext ctx)
    {
        float scroll = ctx.ReadValue<Vector2>().y;
        if (scroll > 0) NextWeapon();
        else if (scroll < 0) PreviousWeapon();
    }

    public void OnWeapon1(InputAction.CallbackContext ctx)
    {
        if (ctx.performed) EquipSlot(0);
    }
    public void OnWeapon2(InputAction.CallbackContext ctx)
    {
        if (ctx.performed) EquipSlot(1);
    }
    public void OnWeapon3(InputAction.CallbackContext ctx)
    {
        if (ctx.performed) EquipSlot(2);
    }

    private void EquipSlot(int index)
    {
        if (index < 0 || index >= weaponSlots.Length)
            return;
        if (weaponSlots[index] == null)
            return;

        Current?.OnUnequip();
        currentIndex = index;
        Current?.OnEquip();
    }

    private void NextWeapon()
    {
        int newIndex = (currentIndex + 1) % weaponSlots.Length;
        EquipSlot(newIndex);
    }

    private void PreviousWeapon()
    {
        int newIndex = (currentIndex - 1 + weaponSlots.Length) % weaponSlots.Length;
        EquipSlot(newIndex);
    }
}
