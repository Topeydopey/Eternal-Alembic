// Pickup.cs (equipment-only version)
using UnityEngine;

public class Pickup : MonoBehaviour, IInteractable
{
    public ItemSO item;
    [Min(1)] public int amount = 1;

    public void Interact(GameObject interactor)
    {
        if (!item || amount <= 0) return;

        var eq = EquipmentInventory.Instance;
        if (!eq) return;

        bool equipped = false;
        // Equip as many single items as we can (usually your items are 1 anyway)
        while (amount > 0 && eq.TryEquipToFirstAvailable(item))
        {
            amount--;
            equipped = true;
        }

        if (equipped && amount <= 0)
        {
            Destroy(gameObject);
        }
        else
        {
            // Could not equip (no suitable slot). Keep the pickup in the world.
            // TODO: show "Hands/Pockets full" feedback
        }
    }
}
