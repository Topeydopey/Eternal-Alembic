using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerClickInteractor : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference click; // bind to <Mouse>/leftButton

    [Header("Raycast")]
    public LayerMask pickupMask;       // include your "Pickup" layer here
    public bool blockWhenPointerOverUI = true;

    private Camera _cam;

    private void Awake()
    {
        _cam = Camera.main;
        if (!_cam) _cam = FindFirstObjectByType<Camera>();
    }

    private void OnEnable()
    {
        if (click) click.action.Enable();
    }

    private void OnDisable()
    {
        if (click) click.action.Disable();
    }

    private void Update()
    {
        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam == null) _cam = FindFirstObjectByType<Camera>();
            if (_cam == null) return; // still no camera, skip this frame
        }
        if (click == null || !click.action.WasPressedThisFrame())
            return;

        if (blockWhenPointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            // Comment this out if you want clicks to pass through UI
            // Debug.Log("[ClickInteractor] Pointer over UI, ignoring world click.");
            return;
        }

        if (_cam == null)
        {
            Debug.LogWarning("[ClickInteractor] No camera found.");
            return;
        }

        Vector2 screen = Mouse.current.position.ReadValue();
        Vector3 world = _cam.ScreenToWorldPoint(screen);
        world.z = 0f; // 2D world at z=0
        Vector2 p = world;

        // Hit all colliders under the cursor on the pickupMask
        var hits = Physics2D.OverlapPointAll(p, pickupMask);
        if (hits == null || hits.Length == 0)
        {
            // Debug.Log($"[ClickInteractor] Nothing at {p} on mask {pickupMask.value}.");
            return;
        }

        // Find a Pickup on the collider or its parent/children
        Pickup target = null;
        foreach (var h in hits)
        {
            if (!h) continue;
            target = h.GetComponent<Pickup>()
                  ?? h.GetComponentInParent<Pickup>()
                  ?? h.GetComponentInChildren<Pickup>();
            if (target) break;
        }
        if (!target)
        {
            // Debug.Log("[ClickInteractor] Colliders found, but no Pickup component there.");
            return;
        }

        // Try to put it into the active hand if empty & accepts the item
        var eq = EquipmentInventory.Instance;
        if (!eq) return;

        var handSlot = eq.Get(eq.activeHand);
        if (handSlot == null)
        {
            Debug.LogWarning("[ClickInteractor] Active hand slot missing.");
            return;
        }

        if (!handSlot.IsEmpty)
        {
            // Debug.Log("[ClickInteractor] Active hand occupied.");
            return;
        }
        if (!handSlot.Accepts(target.item))
        {
            // Debug.Log("[ClickInteractor] Item doesn't fit in active hand.");
            return;
        }

        // Consume one from the world stack, then equip in hand
        if (target.ConsumeOne())
        {
            eq.TryEquip(eq.activeHand, target.item);
            // Debug.Log($"[ClickInteractor] Picked {target.item.displayName} into {eq.activeHand}.");
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Optional: draw a small gizmo at mouse for sanity
        if (_cam == null) return;
        var screen = Mouse.current?.position.ReadValue() ?? Vector2.zero;
        var world = _cam.ScreenToWorldPoint(screen);
        world.z = 0f;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(world, 0.1f);
    }
}
