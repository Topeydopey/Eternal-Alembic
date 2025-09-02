// Pickup.cs
using UnityEngine;

public class Pickup : MonoBehaviour
{
    public ItemSO item;
    [Min(1)] public int amount = 1;

    /// <summary>Removes 1 from this pickup. Destroys GO when empty. Returns true if one was taken.</summary>
    public bool ConsumeOne()
    {
        if (!item || amount <= 0) return false;
        amount -= 1;
        if (amount <= 0) Destroy(gameObject);
        return true;
    }
}
