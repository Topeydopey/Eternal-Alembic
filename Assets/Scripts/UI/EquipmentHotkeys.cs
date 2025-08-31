using UnityEngine;
using UnityEngine.InputSystem;

public class EquipmentHotkeys : MonoBehaviour
{
    public InputActionReference toggleHand; // bind to Q
    public InputActionReference dropActive; // bind to G

    private void OnEnable()
    {
        if (toggleHand) toggleHand.action.Enable();
        if (dropActive) dropActive.action.Enable();
    }
    private void OnDisable()
    {
        if (toggleHand) toggleHand.action.Disable();
        if (dropActive) dropActive.action.Disable();
    }

    private void Update()
    {
        var eq = EquipmentInventory.Instance;
        if (!eq) return;

        if (toggleHand && toggleHand.action.WasPressedThisFrame())
            eq.ToggleActiveHand();

        if (dropActive && dropActive.action.WasPressedThisFrame())
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player)
            {
                // Drop slightly in front of the player (optional offset)
                Vector3 pos = player.transform.position + Vector3.up * 0.1f;
                eq.DropActive(pos);
            }
        }
    }
}
