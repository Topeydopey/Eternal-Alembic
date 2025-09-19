using UnityEngine;
using UnityEngine.UI;

public class Cauldron : MonoBehaviour
{
    [Header("Visuals (any or none)")]
    public SpriteRenderer liquidSprite;    // world sprite for the liquid color (optional)
    public Image liquidImage;              // UI Image for liquid color (optional)
    public ParticleSystem bubbles;         // optional FX on deposit

    [Header("Final Reward")]
    public ItemSO finalPotionItem;
    public bool allowWorldDropIfFull = true;
    public bool requireEmptyHandToCollect = false;

    [Header("Floating Hint on Collect")]
    [SerializeField] private bool showHintOnCollect = true;
    [SerializeField] private Transform hintTarget;
    [SerializeField] private Vector3 hintLocalOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private float hintFadeIn = 0.12f;
    [SerializeField] private float hintHold = 1.25f;
    [SerializeField] private float hintFadeOut = 0.25f;
    [SerializeField] private string receivedTemplate = "You received {0}!";
    [SerializeField] private string droppedTemplate = "{0} was dropped nearby.";

    [Header("Proximity")]
    [Tooltip("Require the player to be within radius to interact.")]
    [SerializeField] private bool requireProximity = true;
    [SerializeField, Min(0f)] private float interactRadius = 1.8f;
    [SerializeField] private Transform proximityOrigin;

    // Expose for interactor
    public bool RequireProximity => requireProximity;
    public bool IsInRange(Transform player)
    {
        if (!player) return false;
        Vector3 a = (proximityOrigin ? proximityOrigin.position : transform.position);
        return Vector2.Distance(a, player.position) <= interactRadius;
    }

    [Header("Color Mixing")]
    public bool randomizeHuePerSession = true;
    [Range(0f, 1f)] public float startValue = 0.25f;
    [Range(0f, 1f)] public float endValue = 0.85f;
    [Range(0f, 1f)] public float saturation = 0.85f;

    private float hue;

    void OnEnable()
    {
        hue = randomizeHuePerSession ? Random.value : 0.33f;

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

    // Interactor calls this after proximity was checked
    public void TryDepositFromActiveHand()
    {
        var eq = EquipmentInventory.Instance;
        var gs = GameState.Instance;
        if (!eq || !gs) return;

        var hand = eq.Get(eq.activeHand);

        if (hand == null || hand.IsEmpty)
        {
            TryCollectReward();
            return;
        }

        var item = hand.item;
        if (gs.SubmitItem(item))
        {
            eq.Unequip(eq.activeHand);
            if (bubbles) bubbles.Play();
        }
        else
        {
            // wrong item feedback here if desired
        }
    }

    private void TryCollectReward()
    {
        var gs = GameState.Instance;
        var eq = EquipmentInventory.Instance;
        if (!gs || !eq || !finalPotionItem) return;

        if (!gs.IsRecipeComplete || !gs.RewardAvailable) return;

        var active = eq.Get(eq.activeHand);
        if (requireEmptyHandToCollect && active != null && !active.IsEmpty) return;

        bool equipped = eq.TryEquip(eq.activeHand, finalPotionItem) || eq.TryEquipToFirstAvailable(finalPotionItem);

        bool dropped = false;
        if (!equipped && allowWorldDropIfFull && eq.pickupPrefab)
        {
            var go = Instantiate(eq.pickupPrefab, transform.position, Quaternion.identity);
            var pickup = go.GetComponent<Pickup>();
            if (pickup) { pickup.item = finalPotionItem; pickup.amount = 1; }
            dropped = true;
        }

        gs.MarkRewardCollected();

        if (showHintOnCollect)
        {
            string nameText = finalPotionItem ? finalPotionItem.displayName : "the Elixir of Life";
            string msg = dropped ? string.Format(droppedTemplate, nameText)
                                 : string.Format(receivedTemplate, nameText);
            var target = hintTarget ? hintTarget
                                    : (EquipmentInventory.Instance ? EquipmentInventory.Instance.transform : null);
            if (target)
                FloatingWorldHint.Show(target, msg, hintLocalOffset, hintFadeIn, hintHold, hintFadeOut);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!requireProximity) return;
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.35f);
        Vector3 o = (proximityOrigin ? proximityOrigin.position : transform.position);
        Gizmos.DrawWireSphere(o, interactRadius);
    }
#endif
}
