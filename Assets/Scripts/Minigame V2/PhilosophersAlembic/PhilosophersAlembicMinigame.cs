// Assets/Scripts/Minigame V2/PhilosophersAlembic/PhilosophersAlembicMinigame.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PhilosophersAlembicMinigame : MonoBehaviour
{
    public enum Phase { Black = 0, White = 1, Gold = 2, Red = 3 }

    [Header("Scene Refs")]
    public Canvas owningCanvas;
    public GameObject owningRoot;
    [SerializeField] private DropSlotAlembic mouthDropSlot;    // acceptsTag = "Runoff"
    [SerializeField] private DropSlotAlembic takeZoneDropSlot; // acceptsTag = "Result"
    [SerializeField] private RunoffBeakerSprites beaker;
    [SerializeField] private Image alembicLiquidImage;

    [Header("Phase Colors")]
    [SerializeField] private Color blackCol = new Color(0.1f, 0.1f, 0.1f);
    [SerializeField] private Color whiteCol = new Color(0.93f, 0.93f, 0.93f);
    [SerializeField] private Color goldCol = new Color(0.95f, 0.76f, 0.31f);
    [SerializeField] private Color redCol = new Color(0.89f, 0.27f, 0.23f);

    [Header("Fill Timings (sec) per phase: Black,White,Gold,Red")]
    [SerializeField] private float[] phaseFillTimes = new float[] { 2.5f, 2.0f, 1.6f, 1.6f };

    [Header("Result")]
    [SerializeField] private ItemSO resultItem;

    [Header("Events")]
    public UnityEvent onClosed;

    public Phase CurrentPhase { get; private set; } = Phase.Black;
    public bool BurnerOn { get; private set; }

    private Coroutine fillingCo;

    void Awake()
    {
        UpdateAlembicColor();
        if (beaker) beaker.Init(this);

        if (!mouthDropSlot)
            mouthDropSlot = GetComponentInChildren<DropSlotAlembic>(true);
        if (!takeZoneDropSlot)
        {
            foreach (var s in GetComponentsInChildren<DropSlotAlembic>(true))
                if (s && s.acceptsTag == "Result") { takeZoneDropSlot = s; break; }
        }
    }

    public void BeginSession()
    {
        SetPhase(Phase.Black);
        BurnerOn = false;
        RestartFillingLoop();
    }

    public void ToggleBurner()
    {
        BurnerOn = !BurnerOn;
        RestartFillingLoop();
    }

    private void RestartFillingLoop()
    {
        if (fillingCo != null) StopCoroutine(fillingCo);
        if (BurnerOn) fillingCo = StartCoroutine(FillingRoutine());
    }

    private IEnumerator FillingRoutine()
    {
        beaker.SetVisualPhase((int)CurrentPhase, GetPhaseColor(CurrentPhase));

        float fillTime = GetPhaseFillTime(CurrentPhase);
        float t = 0f;
        while (t < fillTime)
        {
            if (!BurnerOn) yield break;
            t += Time.deltaTime;
            yield return null;
        }

        if (CurrentPhase == Phase.Red)
            beaker.MarkFullCollectible((int)CurrentPhase);
        else
            beaker.MarkFullPourable((int)CurrentPhase);
    }

    // ---------- ALEMBIC-SPECIFIC DROP HANDLER (unique name) ----------
    public void HandleDropAlembic(DropSlotAlembic slot, GameObject droppedGO)
    {
        if (!slot || !droppedGO) return;

        // Collect on TakeZone (expects 'Result' during Red)
        if (slot.acceptsTag == "Result" && droppedGO.CompareTag("Result") && CurrentPhase == Phase.Red)
        {
            var drag = droppedGO.GetComponent<DraggableItem>();
            if (drag) drag.Consume();

            if (EquipmentInventory.Instance.TryEquipToFirstAvailable(resultItem))
            {
                owningRoot.SetActive(false);
                onClosed?.Invoke();
            }
            else
            {
                Debug.Log("[Alembic] Inventory full; could not equip result.");
            }
            return;
        }

        // Pour on Mouth (expects 'Runoff' in non-Red)
        if (slot.acceptsTag == "Runoff" && droppedGO.CompareTag("Runoff") && CurrentPhase != Phase.Red)
        {
            var rb = droppedGO.GetComponent<RunoffBeakerSprites>();
            if (rb) StartCoroutine(PourAndAdvance(rb));
            return;
        }

        Debug.Log($"[Alembic] Drop ignored. Slot:{slot.acceptsTag}, Item:{droppedGO.tag}, Phase:{CurrentPhase}");
    }
    // ---------------------------------------------------------------

    // (Optional) Keep your generic signature for any old DropSlot usage elsewhere
    public void HandleDrop(DropSlot slot, GameObject droppedGO)
    {
        if (!slot || !droppedGO) return;

        if (slot.acceptsTag == "Result" && droppedGO.CompareTag("Result") && CurrentPhase == Phase.Red)
        {
            var drag = droppedGO.GetComponent<DraggableItem>();
            if (drag) drag.Consume();

            if (EquipmentInventory.Instance.TryEquipToFirstAvailable(resultItem))
            {
                owningRoot.SetActive(false);
                onClosed?.Invoke();
            }
            else
            {
                Debug.Log("[Alembic] Inventory full; could not equip result.");
            }
            return;
        }

        if (slot.acceptsTag == "Runoff" && droppedGO.CompareTag("Runoff") && CurrentPhase != Phase.Red)
        {
            var rb = droppedGO.GetComponent<RunoffBeakerSprites>();
            if (rb) StartCoroutine(PourAndAdvance(rb));
            return;
        }

        Debug.Log($"[Alembic] Drop ignored. Slot:{slot.acceptsTag}, Item:{droppedGO.tag}, Phase:{CurrentPhase}");
    }

    private IEnumerator PourAndAdvance(RunoffBeakerSprites rb)
    {
        BurnerOn = false;
        if (fillingCo != null) StopCoroutine(fillingCo);

        yield return rb.PourTiltRoutine();

        Phase next = NextPhase(CurrentPhase);
        SetPhase(next);

        BurnerOn = true;
        RestartFillingLoop();
    }

    private void SetPhase(Phase p)
    {
        CurrentPhase = p;
        UpdateAlembicColor();
        if (beaker) beaker.SetVisualPhase((int)p, GetPhaseColor(p));
    }

    private void UpdateAlembicColor()
    {
        if (!alembicLiquidImage) return;
        alembicLiquidImage.color = GetPhaseColor(CurrentPhase);
    }

    public Color GetPhaseColor(Phase p) =>
        p switch
        {
            Phase.Black => blackCol,
            Phase.White => whiteCol,
            Phase.Gold => goldCol,
            Phase.Red => redCol,
            _ => blackCol
        };

    private float GetPhaseFillTime(Phase p)
    {
        int i = (int)p;
        if (phaseFillTimes != null && i < phaseFillTimes.Length) return phaseFillTimes[i];
        return 2f;
    }

    private Phase NextPhase(Phase p) =>
        p switch
        {
            Phase.Black => Phase.White,
            Phase.White => Phase.Gold,
            Phase.Gold => Phase.Red,
            _ => Phase.Red
        };
}
