using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SeedWorld : MonoBehaviour
{
    void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true; // so the head can “eat” it
    }
}
