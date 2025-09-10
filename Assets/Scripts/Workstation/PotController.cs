using UnityEngine;

public class PotController : MonoBehaviour
{
    public Sprite emptySprite;
    public Sprite seededSprite;

    [Header("Growth settings")]
    public float growTime = 2f;                // seconds to animate
    public GameObject deadPlantPickupPrefab;   // prefab with Pickup + Collider2D
    public Transform spawnPoint;               // where to spawn Dead Plant (optional)

    private SpriteRenderer sr;
    private bool hasSeed = false;
    private bool isGrowing = false;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr && emptySprite) sr.sprite = emptySprite;
    }

    public bool TryInsertSeed(ItemSO item)
    {
        if (hasSeed || isGrowing) return false;
        if (item.id != "seed") return false; // match your Seed ItemSO ID

        hasSeed = true;
        if (sr && seededSprite) sr.sprite = seededSprite;
        return true;
    }

    public bool TryAddPotion(ItemSO item)
    {
        if (!hasSeed || isGrowing) return false;
        if (item.id != "growth_potion") return false; // match your Potion ItemSO ID

        StartCoroutine(GrowRoutine());
        return true;
    }

    private System.Collections.IEnumerator GrowRoutine()
    {
        isGrowing = true;
        // TODO: play grow animation instead of just wait
        yield return new WaitForSeconds(growTime);

        // Spawn Dead Plant pickup
        Vector3 pos = spawnPoint ? spawnPoint.position : transform.position + Vector3.up * 0.2f;
        var go = Instantiate(deadPlantPickupPrefab, pos, Quaternion.identity);
        var pickup = go.GetComponent<Pickup>();
        if (pickup && pickup.item) pickup.amount = 1;

        // Reset pot
        hasSeed = false;
        isGrowing = false;
        if (sr && emptySprite) sr.sprite = emptySprite;
    }
}
