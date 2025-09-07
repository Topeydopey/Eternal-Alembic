using UnityEngine;

public class ProducerNode : MonoBehaviour
{
    public ItemSO outputItem;
    public GameObject pickupPrefab;
    public Transform spawnPoint;             // optional
    public Vector2 spawnOffset = new Vector2(0f, 0.5f); // default: above
    public float clearRadius = 0.15f;        // overlap check radius
    public LayerMask blockMask;              // colliders to avoid (e.g., Interactable, Default, Pickup)

    public void ProduceOne()
    {
        if (!outputItem || !pickupPrefab) return;

        Vector3 basePos = spawnPoint ? spawnPoint.position : transform.position;
        Vector3 pos = basePos + (Vector3)spawnOffset;

        // If there’s something already there, try a tiny radial search
        const int STEPS = 8;
        float stepAngle = 360f / STEPS;
        for (int i = 0; i < STEPS; i++)
        {
            if (!Physics2D.OverlapCircle(pos, clearRadius, blockMask)) break;
            float ang = stepAngle * i * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            pos = basePos + (Vector3)(spawnOffset + dir * 0.25f);
        }

        var go = Instantiate(pickupPrefab, pos, Quaternion.identity);
        var p = go.GetComponent<Pickup>();
        if (p) { p.item = outputItem; p.amount = 1; }
    }
}
