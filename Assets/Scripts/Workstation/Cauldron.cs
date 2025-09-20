// Assets/Scripts/Minigame V2/Cauldron.cs
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

    // ---------------- AUDIO ----------------
    [Header("Audio (optional)")]
    [Tooltip("2D AudioSource for cauldron SFX. If left empty, the script will spawn temporary one-shot sources.")]
    [SerializeField] private AudioSource sfxAudio;

    [Tooltip("Plays when an item is successfully deposited.")]
    [SerializeField] private AudioClip depositSfx;
    [Range(0f, 1f)][SerializeField] private float depositVolume = 1f;

    [Tooltip("Plays once when the recipe becomes ready to collect (first time RewardAvailable turns true).")]
    [SerializeField] private AudioClip rewardReadySfx;
    [Range(0f, 1f)][SerializeField] private float rewardReadyVolume = 1f;

    private float hue;
    private bool rewardAvailablePrev;

    void OnEnable()
    {
        hue = randomizeHuePerSession ? Random.value : 0.33f;

        var gs = GameState.Instance;
        if (gs)
        {
            gs.OnChanged += HandleStateChanged;
            rewardAvailablePrev = gs.RewardAvailable;  // capture initial state so we only chime on transition
            UpdateLiquidVisual(gs.Progress01);
        }
        else
        {
            rewardAvailablePrev = false;
            UpdateLiquidVisual(0f);
        }
    }

    void OnDisable()
    {
        var gs = GameState.Instance;
        if (gs) gs.OnChanged -= HandleStateChanged;
    }

    private void HandleStateChanged()
    {
        var gs = GameState.Instance;
        float progress = gs ? gs.Progress01 : 0f;
        UpdateLiquidVisual(progress);

        // Reward-ready chime on first transition to available
        bool nowAvail = gs && gs.RewardAvailable;
        if (nowAvail && !rewardAvailablePrev)
            PlayOneShotSafe(rewardReadySfx, rewardReadyVolume);
        rewardAvailablePrev = nowAvail;
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
            // Success: consume, visuals, and DEPOSIT SFX
            eq.Unequip(eq.activeHand);

            if (bubbles) bubbles.Play();
            PlayOneShotSafe(depositSfx, depositVolume);
        }
        else
        {
            // wrong item feedback hook (optional)
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

    // ---------------- Audio helper ----------------
    private void PlayOneShotSafe(AudioClip clip, float volume)
    {
        if (!clip) return;

        if (sfxAudio)
        {
            sfxAudio.PlayOneShot(clip, Mathf.Clamp01(volume));
            return;
        }

        // Detached temp 2D one-shot so it isn’t silenced by hierarchy disables
        var go = new GameObject("CauldronOneShot2D");
        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = false;
        a.spatialBlend = 0f;   // 2D
        a.volume = Mathf.Clamp01(volume);
        a.clip = clip;
        a.Play();
        Destroy(go, Mathf.Max(0.02f, clip.length));
    }
}
