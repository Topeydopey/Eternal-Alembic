// WorkstationLauncher.cs
using UnityEngine;

public class WorkstationLauncher : MonoBehaviour
{
    public GameObject mortarPestleUIPrefab; // assign the UI prefab from Project
    public ItemSO fallbackItemIfInventoryFull; // optional

    public void Launch()
    {
        MinigameManager.Instance.Open(mortarPestleUIPrefab, result =>
        {
            if (!result) return;

            var eq = EquipmentInventory.Instance;
            if (eq && eq.TryEquipToFirstAvailable(result)) return;

            // fallback: drop near player
            var player = GameObject.FindGameObjectWithTag("Player");
            var pos = player ? player.transform.position + Vector3.up * 0.2f : Vector3.zero;
            var pickupPrefab = eq ? eq.pickupPrefab : null;
            if (pickupPrefab)
            {
                var go = Object.Instantiate(pickupPrefab, pos, Quaternion.identity);
                var p = go.GetComponent<Pickup>();
                if (p) { p.item = result; p.amount = 1; }
            }
        });
    }
}
