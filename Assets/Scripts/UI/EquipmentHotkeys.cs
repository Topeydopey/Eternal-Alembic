// EquipmentHotkeys.cs
using UnityEngine;
using UnityEngine.InputSystem;

public class EquipmentHotkeys : MonoBehaviour
{
    public InputActionReference swapHands;  // bind to Q
    public InputActionReference unequipLeft;  // bind to 1
    public InputActionReference unequipRight; // bind to 2

    private void OnEnable()
    {
        if (swapHands) swapHands.action.Enable();
        if (unequipLeft) unequipLeft.action.Enable();
        if (unequipRight) unequipRight.action.Enable();
    }
    private void OnDisable()
    {
        if (swapHands) swapHands.action.Disable();
        if (unequipLeft) unequipLeft.action.Disable();
        if (unequipRight) unequipRight.action.Disable();
    }

    private void Update()
    {
        var eq = EquipmentInventory.Instance;
        if (!eq) return;

        if (swapHands && swapHands.action.WasPressedThisFrame())
            eq.SwapHands();

        if (unequipLeft && unequipLeft.action.WasPressedThisFrame())
        {
            var item = eq.Unequip(EquipmentSlotType.LeftHand);
            if (item && PlayerInventory.Instance)
                PlayerInventory.Instance.inv.Add(item, 1);
        }

        if (unequipRight && unequipRight.action.WasPressedThisFrame())
        {
            var item = eq.Unequip(EquipmentSlotType.RightHand);
            if (item && PlayerInventory.Instance)
                PlayerInventory.Instance.inv.Add(item, 1);
        }
    }
}
