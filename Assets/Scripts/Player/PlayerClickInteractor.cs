using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerClickInteractor : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference click; // <Mouse>/leftButton

    [Header("Masks")]
    public LayerMask interactableMask; // ProducerNode / Cauldron / BedSleep
    public LayerMask tableMask;        // Table
    public LayerMask pickupMask;       // Ground pickups

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
        _cam = null; // re-acquire next frame
    }

    private void Update()
    {
        if (click == null || !click.action.WasPressedThisFrame()) return;
        if (blockWhenPointerOverUI && EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;

        if (_cam == null)
        {
            _cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (_cam == null) return;
        }

        Vector2 screen = Mouse.current.position.ReadValue();
        Vector3 w3 = _cam.ScreenToWorldPoint(screen);
        w3.z = 0f;
        Vector2 w = w3;

        var eq = EquipmentInventory.Instance;
        var activeSlot = eq ? eq.Get(eq.activeHand) : null;
        if (!eq || activeSlot == null) return;

        // -------- 0) If hand empty, try ground pickups FIRST --------
        if (activeSlot.IsEmpty)
        {
            var hits = Physics2D.OverlapPointAll(w, pickupMask);
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

        // -------- 1) Table: take (empty hand) --------
        if (activeSlot.IsEmpty)
        {
            var tHit = Physics2D.OverlapPoint(w, tableMask);
            if (tHit)
            {
                var ts = tHit.GetComponent<TableSurface>() ?? tHit.GetComponentInParent<TableSurface>();
                if (ts != null)
                {
                    var pi = ts.ItemAt(w);
                    if (pi && activeSlot.Accepts(pi.item))
                    {
                        ts.Remove(pi);
                        eq.TryEquip(eq.activeHand, pi.item);
                        return;
                    }
                }
            }
        }

        // -------- 2) Table: place (holding) --------
        if (!activeSlot.IsEmpty)
        {
            var tHit = Physics2D.OverlapPoint(w, tableMask);
            if (tHit)
            {
                var ts = tHit.GetComponent<TableSurface>() ?? tHit.GetComponentInParent<TableSurface>();
                if (ts && ts.TryPlace(activeSlot.item, w, out _))
                {
                    eq.Unequip(eq.activeHand);
                    return;
                }
            }
        }

        // -------- 3) World interactables LAST --------
        var iHit = Physics2D.OverlapPoint(w, interactableMask);
        if (iHit)
        {
            var prod = iHit.GetComponentInParent<ProducerNode>() ?? iHit.GetComponent<ProducerNode>();
            if (prod) { prod.ProduceOne(); return; }

            var cauld = iHit.GetComponentInParent<Cauldron>() ?? iHit.GetComponent<Cauldron>();
            if (cauld) { cauld.TryDepositFromActiveHand(); return; }

            var bed = iHit.GetComponentInParent<BedSleep>() ?? iHit.GetComponent<BedSleep>();
            if (bed) { bed.TrySleep(); return; }
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
