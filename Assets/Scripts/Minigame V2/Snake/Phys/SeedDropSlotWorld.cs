using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SeedDropSlotWorld : MonoBehaviour, IDropHandler
{
    [Header("Accepts")]
    public string acceptsTag = "Seed";

    [Header("World")]
    public Camera worldCamera;
    public GameObject seedWorldPrefab;
    public Transform worldParent;

    [Header("Snake")]
    public SnakePhysicsController snake;

    [Header("Debug")]
    public bool verbose = false;

    void Awake()
    {
        var img = GetComponent<Image>();
        if (!img)
        {
            img = gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0);
        }
        img.raycastTarget = true;

        if (!worldCamera) worldCamera = Camera.main;
    }

    public void OnDrop(PointerEventData e)
    {
        GameObject dragged = e.pointerDrag;

        // Fallback if the event is still pointing at the bag
        if (!dragged || !dragged.CompareTag(acceptsTag))
            dragged = SeedBag.CurrentToken;

        if (!dragged || !dragged.CompareTag(acceptsTag))
        {
            if (verbose) Debug.Log("[SeedDropSlotWorld] No valid seed token on drop.");
            return;
        }

        if (!worldCamera || !seedWorldPrefab || !snake)
        {
            Debug.LogError("[SeedDropSlotWorld] Missing refs (camera/prefab/snake).");
            return;
        }

        // Screen → world
        Vector3 w3 = worldCamera.ScreenToWorldPoint(
            new Vector3(e.position.x, e.position.y, -worldCamera.transform.position.z));
        w3.z = 0f;

        // Consume UI token so it doesn’t snap back
        var di = dragged.GetComponent<DraggableItem>();
        if (di) di.Consume(); else dragged.SetActive(false);

        // Spawn world seed
        var seed = Instantiate(seedWorldPrefab, w3, Quaternion.identity, worldParent);
        if (verbose) Debug.Log($"[SeedDropSlotWorld] Spawned world seed at {w3}");

        // Send the snake
        snake.EnqueueTarget(w3);
    }
}
