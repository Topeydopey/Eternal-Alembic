using UnityEngine;
using System.Collections;

public class PestleController : MonoBehaviour
{
    [Header("Sprites (optional)")]
    public Sprite emptySprite;
    public Sprite loadedSprite;   // when Dead Plant is inside

    [Header("Grind settings")]
    public float grindTime = 1.0f;

    [Header("Outputs")]
    public GameObject plantRemnantsPickupPrefab; // Pickup + Collider2D, item=PlantRemnants
    public GameObject seedPickupPrefab;          // optional: Pickup + Collider2D, item=Seed
    public Transform spawnPoint;                 // where to spawn outputs (else uses transform.position)

    [Header("IDs (match your ItemSO ids)")]
    public string deadPlantId = "dead_plant";
    public string mortarId = "mortar";

    private SpriteRenderer sr;
    private bool hasDeadPlant = false;
    private bool isGrinding = false;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr && emptySprite) sr.sprite = emptySprite;
    }

    // Allow starting grind without a tool after dead plant is inserted
    public bool TryGrindNoTool()
    {
        if (isGrinding || !hasDeadPlant) return false;
        StartCoroutine(GrindRoutine());
        return true;
    }

    public bool TryInsertDeadPlant(ItemSO item)
    {
        if (isGrinding || hasDeadPlant) return false;
        if (item == null || item.id != deadPlantId) return false;

        hasDeadPlant = true;
        if (sr && loadedSprite) sr.sprite = loadedSprite;
        return true;
    }

    public bool TryAddMortar(ItemSO item)
    {
        if (isGrinding || !hasDeadPlant) return false;
        if (item == null || item.id != mortarId) return false;

        StartCoroutine(GrindRoutine());
        return true;
    }

    private IEnumerator GrindRoutine()
    {
        isGrinding = true;
        // TODO: play shake/particles/sfx
        yield return new WaitForSeconds(grindTime);

        Vector3 pos = spawnPoint ? spawnPoint.position : transform.position + Vector3.up * 0.15f;

        if (plantRemnantsPickupPrefab)
        {
            Instantiate(plantRemnantsPickupPrefab, pos, Quaternion.identity);
        }
        if (seedPickupPrefab) // optional: give seed back
        {
            Instantiate(seedPickupPrefab, pos + new Vector3(0.2f, 0f, 0f), Quaternion.identity);
        }

        // reset
        hasDeadPlant = false;
        isGrinding = false;
        if (sr && emptySprite) sr.sprite = emptySprite;
    }
}
