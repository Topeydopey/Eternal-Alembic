using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerClickInteractor : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference click; // <Mouse>/leftButton

    [Header("Masks")]
    public LayerMask interactableMask; // ProducerNode / Cauldron / BedSleep / PotController
    public LayerMask tableMask;        // Table
    public LayerMask pickupMask;       // Ground pickups

    [Header("Options")]
    public bool blockWhenPointerOverUI = true;

    [Header("Elixir & Death")]
    [Tooltip("Assign your 'Elixir of Life' ItemSO here.")]
    [SerializeField] private ItemSO elixirOfLifeItem;
    [Tooltip("Player death controller that plays the death animation and locks controls.")]
    [SerializeField] private PlayerDeathController deathController;
    [Tooltip("The player's own Collider2D used to detect self-clicks. If null, auto-detected at runtime.")]
    [SerializeField] private Collider2D selfCollider;

    private Camera _cam;

    private void Awake()
    {
        _cam = Camera.main ?? FindFirstObjectByType<Camera>();
        if (!selfCollider)
        {
            selfCollider = GetComponent<Collider2D>() ?? GetComponentInParent<Collider2D>();
        }
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
        if (deathController && deathController.IsDying) return;

        if (click == null || !click.action.WasPressedThisFrame()) return;
        if (blockWhenPointerOverUI && EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;

        if (_cam == null)
        {
            _cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (_cam == null) return;
        }

        Vector2 screen = Mouse.current.position.ReadValue();
        Vector3 w3 = _cam.ScreenToWorldPoint(screen); w3.z = 0f;
        Vector2 w = (Vector2)w3;

        var eq = EquipmentInventory.Instance;
        var activeSlot = eq ? eq.Get(eq.activeHand) : null;
        if (!eq || activeSlot == null) return;

        // --- self click to drink elixir ---
        if (!activeSlot.IsEmpty && activeSlot.item == elixirOfLifeItem && ClickedSelf(w))
        {
            eq.Unequip(eq.activeHand);
            if (deathController) deathController.DrinkElixirAndDie();
            else Debug.LogWarning("[PlayerClickInteractor] No PlayerDeathController assigned.");
            return;
        }

        // --- ground pickups first if empty hand ---
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

        // --- interactables ---
        var iHit = Physics2D.OverlapPoint(w, interactableMask);
        if (iHit)
        {
            // Pot
            var pot = iHit.GetComponentInParent<PotController>() ?? iHit.GetComponent<PotController>();
            if (pot && !activeSlot.IsEmpty)
            {
                var item = activeSlot.item;
                if (pot.TryInsertSeed(item) || pot.TryAddPotion(item))
                {
                    eq.Unequip(eq.activeHand);
                    return;
                }
            }

            // Workstation
            var ws = iHit.GetComponentInParent<WorkstationLauncher>() ?? iHit.GetComponent<WorkstationLauncher>();
            if (ws)
            {
                // proximity gate (no collider changes)
                if (!ws.RequireProximity || ws.IsInRange(transform))
                {
                    ws.Launch();
                }
                else
                {
                    FloatingWorldHint.Show(transform, "Move closer to the workstation.", new Vector3(0f, 1.6f, 0f), 0.1f, 1.0f, 0.2f);
                }
                return;
            }

            // Producer
            var prod = iHit.GetComponentInParent<ProducerNode>() ?? iHit.GetComponent<ProducerNode>();
            if (prod) { prod.ProduceOne(); return; }

            // Cauldron
            var cauld = iHit.GetComponentInParent<Cauldron>() ?? iHit.GetComponent<Cauldron>();
            if (cauld)
            {
                if (!cauld.RequireProximity || cauld.IsInRange(transform))
                {
                    cauld.TryDepositFromActiveHand();
                }
                else
                {
                    FloatingWorldHint.Show(transform, "Move closer to the cauldron.", new Vector3(0f, 1.6f, 0f), 0.1f, 1.0f, 0.2f);
                }
                return;
            }
        }

        // --- table take ---
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

        // --- table place ---
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
    }

    private bool ClickedSelf(Vector2 worldPoint)
    {
        if (!selfCollider)
            selfCollider = GetComponent<Collider2D>() ?? GetComponentInParent<Collider2D>();
        return selfCollider && selfCollider.OverlapPoint(worldPoint);
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
