using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerClickInteractor : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference click; // <Mouse>/leftButton

    [Header("Raycast Masks")]
    public LayerMask tableMask;   // set to your "Table" layer
    public LayerMask pickupMask;  // set to your "Pickup" layer

    [Header("Options")]
    public bool blockWhenPointerOverUI = true;

    private Camera _cam;

    private void Awake()
    {
        _cam = Camera.main ?? FindFirstObjectByType<Camera>();
    }

    private void OnEnable()
    {
        if (click) click.action.Enable();
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
        if (click) click.action.Disable();
    }

    private void OnSceneChanged(Scene prev, Scene next)
    {
        _cam = null; // force re-acquire next Update
    }

    private void Update()
    {
        if (click == null || !click.action.WasPressedThisFrame()) return;

        if (blockWhenPointerOverUI && EventSystem.current && EventSystem.current.IsPointerOverGameObject())
            return;

        if (_cam == null)
        {
            _cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (_cam == null) return;
        }

        Vector2 screen = Mouse.current.position.ReadValue();
        Vector3 world3 = _cam.ScreenToWorldPoint(screen);
        world3.z = 0f;
        Vector2 world = world3;

        var eq = EquipmentInventory.Instance;
        if (!eq) return;
        var activeSlot = eq.Get(eq.activeHand);
        if (activeSlot == null) return;

        // -------- 1) Take FROM table (hand empty) --------
        if (activeSlot.IsEmpty)
        {
            var tableHit = Physics2D.OverlapPoint(world, tableMask);
            if (tableHit)
            {
                var surface = tableHit.GetComponent<TableSurface>() ?? tableHit.GetComponentInParent<TableSurface>();
                if (surface != null)
                {
                    var placed = surface.ItemAt(world);
                    if (placed && activeSlot.Accepts(placed.item))
                    {
                        surface.Remove(placed);
                        eq.TryEquip(eq.activeHand, placed.item);
                        return;
                    }
                }
            }
        }

        // -------- 2) Place ONTO table (hand has item) --------
        if (!activeSlot.IsEmpty)
        {
            var tableHit = Physics2D.OverlapPoint(world, tableMask);
            if (tableHit)
            {
                var surface = tableHit.GetComponent<TableSurface>() ?? tableHit.GetComponentInParent<TableSurface>();
                if (surface != null && surface.TryPlace(activeSlot.item, world, out _))
                {
                    eq.Unequip(eq.activeHand);
                    return;
                }
            }
        }

        // -------- 3) Fallback: ground pickups (hand empty) --------
        if (activeSlot.IsEmpty)
        {
            var hits = Physics2D.OverlapPointAll(world, pickupMask);
            if (hits != null && hits.Length > 0)
            {
                Pickup target = null;
                foreach (var h in hits)
                {
                    if (!h) continue;
                    target = h.GetComponent<Pickup>() ?? h.GetComponentInParent<Pickup>() ?? h.GetComponentInChildren<Pickup>();
                    if (target) break;
                }

                if (target && activeSlot.Accepts(target.item) && target.ConsumeOne())
                {
                    eq.TryEquip(eq.activeHand, target.item);
                    return;
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_cam == null || Mouse.current == null) return;
        var screen = Mouse.current.position.ReadValue();
        var world = _cam.ScreenToWorldPoint(screen);
        world.z = 0f;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(world, 0.08f);
    }
}
