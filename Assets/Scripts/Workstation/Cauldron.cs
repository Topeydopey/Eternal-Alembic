// Assets/Scripts/Minigame V2/Cauldron.cs
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
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

    [Header("Floating Hint Target")]
    [SerializeField] private Transform hintTarget; // if null, will try EquipmentInventory.Instance.transform
    [SerializeField] private Vector3 hintLocalOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Hint Timing")]
    [SerializeField] private float hintFadeIn = 0.12f;
    [SerializeField] private float hintHold = 1.25f;
    [SerializeField] private float hintFadeOut = 0.25f;
    [SerializeField, Min(0f)] private float hintCooldownSeconds = 0.75f; // anti-spam

    [Header("Hint Text")]
    [SerializeField] private string needIngredientsText = "You need to add the ingredients first.";
    [SerializeField] private string wrongIngredientText = "That doesn’t seem like the right ingredient.";
    [SerializeField] private string readyText = "The Elixir of Life is ready in the cauldron!";
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

    [Header("Collect Hint Toggle")]
    [SerializeField] private bool showHintOnCollect = true;

    private float hue;
    private bool rewardAvailablePrev;
    private float lastHintTime = -999f;

    void OnEnable()
    {
        hue = randomizeHuePerSession ? Random.value : 0.33f;

        var gs = GameState.Instance;
        if (gs)
        {
            gs.OnChanged += HandleStateChanged;
            rewardAvailablePrev = gs.RewardAvailable;  // capture initial state so we only chime/show on transition
            UpdateLiquidVisual(gs.Progress01);

            // If you want to also show the “ready” hint immediately when loading into a scene where it’s already ready:
            // if (gs.RewardAvailable) ShowHint(readyText);
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

        // Reward-ready: play SFX and show hint exactly on transition false -> true
        bool nowAvail = gs && gs.RewardAvailable;
        if (nowAvail && !rewardAvailablePrev)
        {
            PlayOneShotSafe(rewardReadySfx, rewardReadyVolume);
            ShowHint(readyText);
        }
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

    /// <summary>Interactor calls this after proximity check.</summary>
    public void TryDepositFromActiveHand()
    {
        var eq = EquipmentInventory.Instance;
        var gs = GameState.Instance;
        if (!eq || !gs) return;

        var hand = eq.Get(eq.activeHand);

        // Empty hand → try to collect if ready, otherwise tell the player what’s missing.
        if (hand == null || hand.IsEmpty)
        {
            if (!gs.RewardAvailable)
            {
                ShowHint(needIngredientsText); // <-- NEW: guidance when not ready
                return;
            }

            TryCollectReward();
            return;
        }

        // Has an item → try to submit to the recipe
        var item = hand.item;
        if (gs.SubmitItem(item))
        {
            // Success: consume, visuals, and SFX
            eq.Unequip(eq.activeHand);

            if (bubbles) bubbles.Play();
            PlayOneShotSafe(depositSfx, depositVolume);
        }
        else
        {
            // NEW: wrong item feedback
            ShowHint(wrongIngredientText);
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
            var target = ResolveHintTarget();
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

    // ---------------- Hints & Audio helpers ----------------
    private void ShowHint(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // Simple anti-spam: throttle hints
        if (Time.unscaledTime - lastHintTime < hintCooldownSeconds) return;
        lastHintTime = Time.unscaledTime;

        var target = ResolveHintTarget();
        if (target)
            FloatingWorldHint.Show(target, text, hintLocalOffset, hintFadeIn, hintHold, hintFadeOut);
    }

    private Transform ResolveHintTarget()
    {
        if (hintTarget) return hintTarget;
        var eq = EquipmentInventory.Instance;
        if (eq) return eq.transform;       // near the player
        return transform;                  // fallback: near the cauldron
    }

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
