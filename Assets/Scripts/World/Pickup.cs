using UnityEngine;

public class Pickup : MonoBehaviour, IInteractable
{
    public ItemSO item;
    [Min(1)] public int amount = 1;

    public void Interact(GameObject interactor)
    {
        if (!PlayerInventory.Instance) return;
        int added = PlayerInventory.Instance.inv.Add(item, amount);
        if (added > 0)
        {
            amount -= added;
            if (amount <= 0) Destroy(gameObject);
            // optional: SFX / popup here
        }
        else
        {
            // optional: "Inventory full" feedback
        }
    }
}
