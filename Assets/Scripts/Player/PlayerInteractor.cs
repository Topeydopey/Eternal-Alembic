using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))] // optional if you prefer overlap circle only
public class PlayerInteractor : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference interact; // bind to <Keyboard>/e

    [Header("Detection")]
    [SerializeField] private float radius = 1.25f;
    [SerializeField] private LayerMask interactableMask; // set to "Interactable" layer

    private void OnEnable() { if (interact) interact.action.Enable(); }
    private void OnDisable() { if (interact) interact.action.Disable(); }

    private void Update()
    {
        if (interact == null) return;
        if (!interact.action.WasPressedThisFrame()) return;

        // Find the closest interactable within radius
        var hits = Physics2D.OverlapCircleAll((Vector2)transform.position, radius, interactableMask);
        IInteractable closest = null;
        float bestDist = float.MaxValue;

        foreach (var h in hits)
        {
            if (!h) continue;
            if (h.TryGetComponent<IInteractable>(out var cand))
            {
                float d = Vector2.SqrMagnitude((Vector2)h.transform.position - (Vector2)transform.position);
                if (d < bestDist) { bestDist = d; closest = cand; }
            }
        }

        if (closest != null)
            closest.Interact(gameObject);
        else
            Debug.Log("Nothing to interact with.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
