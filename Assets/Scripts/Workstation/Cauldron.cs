// Cauldron.cs
using UnityEngine;

public class Cauldron : MonoBehaviour
{
    public void TryDepositFromActiveHand()
    {
        var eq = EquipmentInventory.Instance;
        var gs = GameState.Instance;
        if (!eq || !gs) return;

        var hand = eq.Get(eq.activeHand);
        if (hand == null || hand.IsEmpty) return;

        if (gs.SubmitItem(hand.item))
        {
            eq.Unequip(eq.activeHand); // remove from hand on success
            // TODO: FX/SFX
        }
        else
        {
            // TODO: feedback "Wrong ingredient"
        }
    }
}
