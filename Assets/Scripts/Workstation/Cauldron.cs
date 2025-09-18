// Assets/Scripts/World/Cauldron.cs
using UnityEngine;
using UnityEngine.UI;

public class Cauldron : MonoBehaviour
{
    [Header("Visuals (any or none)")]
    public SpriteRenderer liquidSprite;    // world sprite for the liquid color (optional)
    public Image liquidImage;              // UI Image for liquid color (optional)
    public ParticleSystem bubbles;         // optional FX on deposit

    [Header("Final Reward")]
    public ItemSO finalPotionItem;         // assign the final potion ItemSO
    [Tooltip("If inventory full, optionally drop this as a world pickup using EquipmentInventory.pickupPrefab.")]
    public bool allowWorldDropIfFull = true;
    [Tooltip("Require empty active hand to collect the final reward.")]
    public bool requireEmptyHandToCollect = false;

    [Header("Floating Hint on Collect")]
    [Tooltip("Show a world-space hint when the potion is awarded/dropped.")]
    [SerializeField] private bool showHintOnCollect = true;
    [Tooltip("Where to attach the floating hint (usually the Player root). If null, falls back to EquipmentInventory.Instance.transform.")]
    [SerializeField] private Transform hintTarget;
    [SerializeField] private Vector3 hintLocalOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private float hintFadeIn = 0.12f;
    [SerializeField] private float hintHold = 1.25f;
    [SerializeField] private float hintFadeOut = 0.25f;
    [SerializeField] private string receivedTemplate = "You received {0}!";
    [SerializeField] private string droppedTemplate = "{0} was dropped nearby.";

    [Header("Color Mixing")]
    public bool randomizeHuePerSession = true;
    [Range(0f, 1f)] public float startValue = 0.25f;   // dark start
    [Range(0f, 1f)] public float endValue = 0.85f;     // bright end
    [Range(0f, 1f)] public float saturation = 0.85f;

    private float hue;

    void OnEnable()
    {
        if (randomizeHuePerSession) hue = Random.value;
        else hue = 0.33f; // default green-ish

        var gs = GameState.Instance;
        if (gs) gs.OnChanged += HandleStateChanged;

        UpdateLiquidVisual(gs ? gs.Progress01 : 0f);
    }

    void OnDisable()
    {
        var gs = GameState.Instance;
        if (gs) gs.OnChanged -= HandleStateChanged;
    }

    private void HandleStateChanged()
    {
        var gs = GameState.Instance;
        UpdateLiquidVisual(gs ? gs.Progress01 : 0f);
    }

    private void UpdateLiquidVisual(float progress01)
    {
        float v = Mathf.Lerp(startValue, endValue, Mathf.Clamp01(progress01));
        Color c = Color.HSVToRGB(hue, saturation, v);

        if (liquidSprite) liquidSprite.color = c;
        if (liquidImage) liquidImage.color = c;

        if (bubbles)
        {
            var em = bubbles.emission;
            em.rateOverTime = Mathf.Lerp(2f, 12f, progress01);
        }
    }

    /// <summary>
    /// Called by PlayerClickInteractor. If hand holds an item, try deposit.
    /// If hand is empty, try to collect the final potion (if available).
    /// </summary>
    public void TryDepositFromActiveHand()
    {
        var eq = EquipmentInventory.Instance;
        var gs = GameState.Instance;
        if (!eq || !gs) return;

        var hand = eq.Get(eq.activeHand);

        // Empty hand → attempt to collect final reward
        if (hand == null || hand.IsEmpty)
        {
            TryCollectReward();
            return;
        }

        // Holding an item → attempt deposit
        var item = hand.item;
        if (gs.SubmitItem(item))
        {
            eq.Unequip(eq.activeHand); // remove from hand
            if (bubbles) bubbles.Play();
            // color updates via OnChanged → UpdateLiquidVisual
        }
        else
        {
            // Optional: wrong-ingredient feedback
        }
    }

    private void TryCollectReward()
    {
        var gs = GameState.Instance;
        var eq = EquipmentInventory.Instance;
        if (!gs || !eq || !finalPotionItem) return;

        if (!gs.IsRecipeComplete || !gs.RewardAvailable) return;

        var active = eq.Get(eq.activeHand);
        if (requireEmptyHandToCollect && active != null && !active.IsEmpty)
            return; // player must empty hand first

        // Try to equip to active hand (single-hand setup), then fallback (if you still use pockets)
        bool equipped = eq.TryEquip(eq.activeHand, finalPotionItem) || eq.TryEquipToFirstAvailable(finalPotionItem);

        // If we couldn't equip, optionally drop into the world
        bool dropped = false;
        if (!equipped && allowWorldDropIfFull && eq.pickupPrefab)
        {
            var go = Instantiate(eq.pickupPrefab, transform.position, Quaternion.identity);
            var pickup = go.GetComponent<Pickup>();
            if (pickup) { pickup.item = finalPotionItem; pickup.amount = 1; }
            dropped = true;
        }

        // Mark the reward consumed in the game state
        gs.MarkRewardCollected();

        // Show floating hint
        if (showHintOnCollect)
        {
            string nameText = finalPotionItem ? finalPotionItem.displayName : "the Elixir of Life";
            string msg = dropped
                ? string.Format(droppedTemplate, nameText)
                : string.Format(receivedTemplate, nameText);

            var target = hintTarget ? hintTarget
                                    : (EquipmentInventory.Instance ? EquipmentInventory.Instance.transform : null);

            if (target)
            {
                FloatingWorldHint.Show(target, msg, hintLocalOffset, hintFadeIn, hintHold, hintFadeOut);
            }
        }

        // Optional: reset the recipe if you want replay
        // gs.ResetRecipe(); UpdateLiquidVisual(0f);
    }
}
