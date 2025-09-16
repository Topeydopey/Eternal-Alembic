using UnityEngine;
using System.Collections;

public class Cauldron : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("SpriteRenderer for the cauldron liquid (world-space). Leave null if you use a UI Image instead.")]
    [SerializeField] private SpriteRenderer liquidSprite;
    [Tooltip("Optional UI Image for liquid if your cauldron is UI. If both assigned, SpriteRenderer wins.")]
    [SerializeField] private UnityEngine.UI.Image liquidImage;
    [Tooltip("Seconds to tween the liquid color after each deposit.")]
    [SerializeField] private float colorLerpDuration = 0.25f;
    [Range(0f, 1f)] public float liquidAlpha = 1f;

    [Header("Result")]
    [Tooltip("What the player receives after all ingredients are deposited.")]
    public ItemSO finalPotionItem;
    [Tooltip("Where to drop the pickup if inventory is full (defaults to this.transform).")]
    public Transform dropPoint;
    [Tooltip("If inventory is full, drop a pickup in the world.")]
    public bool dropIfInventoryFull = true;

    private Coroutine colorRoutine;
    private Color currentColor = new Color(0.05f, 0.05f, 0.05f, 1f); // start dark

    private void Awake()
    {
        if (!dropPoint) dropPoint = transform;
        ApplyLiquidColorImmediate(currentColor);
    }

    /// <summary>
    /// Single entry point from PlayerClickInteractor.
    /// If holding the next ingredient -> deposit & mix color.
    /// If empty hand and recipe complete -> dispense final potion.
    /// </summary>
    public void TryDepositFromActiveHand()
    {
        var eq = EquipmentInventory.Instance;
        var gs = GameState.Instance;
        if (!gs) return;

        var hand = (eq && eq.Get(eq.activeHand) != null) ? eq.Get(eq.activeHand) : null;

        // If empty hand: try collect final potion
        if (hand == null || hand.IsEmpty)
        {
            TryDispenseIfReady();
            return;
        }

        // Holding something: try to submit
        var item = hand.item;
        bool ok = gs.SubmitItem(item);
        if (ok)
        {
            // Remove from hand
            eq.Unequip(eq.activeHand);

            // Mix color toward a brighter random hue
            int stage = gs.progress;                         // 1..N after this deposit
            int total = gs.recipe != null ? gs.recipe.Length : 1;
            Color target = RandomStageColor(stage, total);
            // "Mix" with current color so it doesn't jump harshly
            Color mixed = Color.Lerp(currentColor, target, 0.6f);
            TweenLiquidColorTo(mixed);

            // TODO SFX/VFX success
        }
        else
        {
            // TODO feedback wrong ingredient
            // Debug.Log("[Cauldron] Wrong ingredient.");
        }
    }

    private void TryDispenseIfReady()
    {
        var gs = GameState.Instance;
        var eq = EquipmentInventory.Instance;
        if (!gs || !gs.IsRecipeComplete) return;

        if (finalPotionItem && eq)
        {
            // Try equip to active hand, then fallback
            bool equipped = eq.TryEquip(eq.activeHand, finalPotionItem) || eq.TryEquipToFirstAvailable(finalPotionItem);
            if (!equipped && dropIfInventoryFull && eq.pickupPrefab)
            {
                // Drop a pickup in the world
                var go = Instantiate(eq.pickupPrefab, dropPoint.position, Quaternion.identity);
                var p = go.GetComponent<Pickup>();
                if (p) { p.item = finalPotionItem; p.amount = 1; }
            }
        }

        // Reset state/visuals for next round
        gs.ResetRecipe();
        ResetLiquid();
        // TODO SFX/VFX dispense
    }

    // ------------------------
    // Color mixing helpers
    // ------------------------

    // Pick a random color with increasing brightness per stage
    private Color RandomStageColor(int stageIndex1Based, int totalStages)
    {
        // Normalize t in [0..1] where 0=first deposit (dark), 1=final deposit (light)
        float t = (totalStages <= 1) ? 1f : Mathf.InverseLerp(1, totalStages, stageIndex1Based);

        float h = Random.value;                   // any hue
        float s = Mathf.Lerp(0.55f, 0.95f, Random.value * 0.7f + 0.3f); // fairly saturated
        // Brightness gradually increases with t; add a small random range
        float vMin = Mathf.Lerp(0.15f, 0.75f, t);
        float vMax = Mathf.Lerp(0.35f, 1.00f, t);
        float v = Random.Range(vMin, vMax);

        Color c = Color.HSVToRGB(h, s, v);
        c.a = 1f;
        return c;
    }

    private void TweenLiquidColorTo(Color target)
    {
        target.a = liquidAlpha;
        if (colorRoutine != null) StopCoroutine(colorRoutine);
        colorRoutine = StartCoroutine(CoLerpColor(currentColor, target, colorLerpDuration));
        currentColor = target;
    }

    private IEnumerator CoLerpColor(Color from, Color to, float dur)
    {
        if (dur <= 0f) { ApplyLiquidColorImmediate(to); yield break; }
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            var c = Color.Lerp(from, to, Mathf.SmoothStep(0, 1, t));
            ApplyLiquidColorImmediate(c);
            yield return null;
        }
        ApplyLiquidColorImmediate(to);
    }

    private void ApplyLiquidColorImmediate(Color c)
    {
        c.a = liquidAlpha;
        if (liquidSprite) liquidSprite.color = c;
        else if (liquidImage) liquidImage.color = c;
    }

    private void ResetLiquid()
    {
        // Reset to a dark base with the configured alpha
        currentColor = new Color(0.05f, 0.05f, 0.05f, liquidAlpha);
        ApplyLiquidColorImmediate(currentColor);
    }
}
