using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SnakeStationMinigame : MonoBehaviour
{
    public static SnakeStationMinigame Instance;

    [Header("Owner/Close")]
    [SerializeField] private Canvas owningCanvas;
    [SerializeField] private GameObject owningRoot;
    [SerializeField] private bool disableInsteadOfDestroy = true;
    public event Action onClosed;

    [Header("Play Area")]
    public RectTransform playArea;
    public Image headImage;
    public GameObject bodySegmentPrefab;  // plain UI Image prefab
    public Sprite bodySprite;
    public Sprite tailSprite;

    [Header("Snake Shape & Motion")]
    public int bodySegmentCount = 18;
    public float segmentSpacing = 9f;     // px between anchors
    public float bodyThickness = 7f;      // segment height (px)
    public float eatSpeed = 380f;
    public float coilRadiusOverride = 80f; // <=0 auto; else fixed
    public float eatRadius = 16f;
    public float returnSpeedScale = 0.9f;

    [Header("Slither Wiggle")]
    public float wiggleAmplitude = 8f;
    public float wiggleWavelength = 80f;

    [Header("Result (Inventory)")]
    public int seedsToWin = 5; // <-- THIS is the one the station uses
    public ItemSO resultItem;
    public GameObject resultTokenPrefab;       // Image + CanvasGroup + DraggableItem; Tag="Result"
    public Transform resultTokenSpawnParent;   // default: playArea
    public Vector2 resultSpawnAnchoredPos = new Vector2(0f, 0f); // visible center by default
    public DropSlotSnake takeZone;
    public Sprite resultIconOverride;

    [Header("UI Hints (optional)")]
    public CanvasGroup hintDropSeeds;
    public CanvasGroup hintCollect;

    [Header("Debug")]
    public bool verbose = false;

    // runtime
    private readonly List<Vector2> points = new();
    private readonly List<Image> images = new();
    private float radius;
    private Vector2 headPos;

    private float travelAccumulator;
    private float distanceTravelled;
    private Vector2 lastWiggleOffset;

    private bool busy;
    private bool completed;
    private int seedsEaten;
    private readonly Queue<SeedTarget> seeds = new();
    private GameObject spawnedResultToken;

    private struct SeedTarget { public Vector2 localPos; public GameObject marker; }

    public Camera CanvasCamera =>
        owningCanvas && owningCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? owningCanvas.worldCamera : null;

    void Awake()
    {
        Instance = this;
        if (!owningCanvas) owningCanvas = GetComponentInParent<Canvas>(true);
        if (!owningRoot && owningCanvas) owningRoot = owningCanvas.gameObject;
        if (!resultTokenSpawnParent) resultTokenSpawnParent = playArea ? playArea : transform;
    }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnEnable()
    {
        if (playArea && headImage && bodySegmentPrefab) BeginSession();
    }

    // ---------- Reuse mode ----------
    public void SetReuseMode(bool reuse, GameObject root = null)
    {
        disableInsteadOfDestroy = reuse;
        if (root) owningRoot = root;
        if (!owningCanvas) owningCanvas = GetComponentInParent<Canvas>(true);
    }

    public void BeginSession()
    {
        if (verbose) Debug.Log("[SnakeStation] BeginSession");

        StopAllCoroutines();
        seeds.Clear();
        seedsEaten = 0;
        busy = false;
        completed = false;

        travelAccumulator = 0f;
        distanceTravelled = 0f;
        lastWiggleOffset = Vector2.zero;

        ClearSegments();
        SetupCoil();

        headImage.gameObject.SetActive(true);

        SetHint(hintDropSeeds, true);
        SetHint(hintCollect, false);
        if (takeZone) takeZone.Enable(false);

        if (spawnedResultToken) { Destroy(spawnedResultToken); spawnedResultToken = null; }
    }

    private void SetHint(CanvasGroup cg, bool on)
    {
        if (!cg) return;
        cg.alpha = on ? 1f : 0f;
        cg.interactable = on;
        cg.blocksRaycasts = on;
    }

    private void ClearSegments()
    {
        for (int i = 0; i < images.Count; i++) if (images[i]) Destroy(images[i].gameObject);
        images.Clear();
        points.Clear();
    }

    private void SetupCoil()
    {
        if (!playArea || !headImage || !bodySegmentPrefab) return;

        radius = (coilRadiusOverride > 0f)
            ? coilRadiusOverride
            : Mathf.Max(8f, (bodySegmentCount * segmentSpacing) / (2f * Mathf.PI));

        headPos = new Vector2(radius, 0f);
        headImage.rectTransform.anchoredPosition = headPos;
        headImage.rectTransform.localEulerAngles = Vector3.zero;

        float stepAngle = (2f * Mathf.PI) / Mathf.Max(2, bodySegmentCount);
        for (int i = 0; i < bodySegmentCount; i++)
        {
            float ang = -stepAngle * (i + 1); // behind the head
            Vector2 p = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
            points.Add(p);

            var go = Instantiate(bodySegmentPrefab, playArea);
            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.sprite = (i == bodySegmentCount - 1) ? tailSprite : bodySprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;

            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(segmentSpacing, bodyThickness);
            rt.anchoredPosition = p;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;

            go.SetActive(true);
            images.Add(img);
        }
        UpdateBodyVisualsInstant();
    }

    private void UpdateBodyVisualsInstant()
    {
        if (images.Count != points.Count) return;

        var dirH = (points.Count > 0) ? (points[0] - headPos).normalized : Vector2.right;
        headImage.rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(dirH.y, dirH.x) * Mathf.Rad2Deg);

        for (int i = 0; i < images.Count; i++)
        {
            var img = images[i];
            var rt = img.rectTransform;
            rt.anchoredPosition = points[i];

            Vector2 to = (i == 0) ? headPos : points[i - 1];
            Vector2 from = points[i];
            Vector2 d = (to - from).normalized;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            rt.localEulerAngles = new Vector3(0, 0, ang);
        }
    }

    // ---------- Seeds ----------
    public void EnqueueSeed(Vector2 localPos, GameObject marker)
    {
        if (completed)
        {
            if (verbose) Debug.Log("[SnakeStation] Ignored seed: already completed.");
            if (marker) Destroy(marker);
            return;
        }

        if (verbose) Debug.Log($"[SnakeStation] Seed queued at {localPos}");
        seeds.Enqueue(new SeedTarget { localPos = localPos, marker = marker });
        if (!busy) StartCoroutine(EatLoop());
        SetHint(hintDropSeeds, false);
    }

    private IEnumerator EatLoop()
    {
        while (seeds.Count > 0 && !completed)
        {
            busy = true;
            var target = seeds.Dequeue();

            // go to seed with slither
            yield return MoveHeadTo(target.localPos, eatSpeed);

            // eat
            if (target.marker) Destroy(target.marker);
            seedsEaten++;
            if (verbose) Debug.Log($"[SnakeStation] Ate seed #{seedsEaten}");

            if (seedsEaten >= Mathf.Max(1, seedsToWin))
            {
                CompleteAndSpawnResult();
                busy = false;
                yield break;
            }

            // return to coil (nearest point on circle)
            Vector2 nearest = headPos.sqrMagnitude > 0.001f
                ? headPos.normalized * radius
                : new Vector2(radius, 0f);
            yield return MoveHeadTo(nearest, eatSpeed * returnSpeedScale);
        }
        busy = false;
    }

    // ---------- Motion with slither wiggle ----------
    private IEnumerator MoveHeadTo(Vector2 dest, float speed)
    {
        lastWiggleOffset = Vector2.zero;

        while (!completed && (dest - headPos).sqrMagnitude > (eatRadius * eatRadius))
        {
            Vector2 toTarget = dest - headPos;
            float distance = toTarget.magnitude;
            if (distance <= 0.0001f) break;

            float step = Mathf.Min(speed * Time.deltaTime, distance);

            Vector2 dir = toTarget / distance;
            Vector2 baseAdvance = dir * step;

            // wiggle offset
            distanceTravelled += step;
            float phase = (distanceTravelled / Mathf.Max(1f, wiggleWavelength)) * Mathf.PI * 2f;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            Vector2 wiggleOffset = perp * (Mathf.Sin(phase) * wiggleAmplitude);
            Vector2 wiggleDelta = wiggleOffset - lastWiggleOffset;
            lastWiggleOffset = wiggleOffset;

            headPos += baseAdvance + wiggleDelta;
            headImage.rectTransform.anchoredPosition = headPos;

            // shift body at spacing
            travelAccumulator += step;
            while (travelAccumulator >= segmentSpacing)
            {
                travelAccumulator -= segmentSpacing;
                if (points.Count > 0)
                {
                    points.Insert(0, headPos);
                    points.RemoveAt(points.Count - 1);
                }
            }

            UpdateBodyVisualsInstant();
            yield return null;
        }
    }

    // ---------- Completion & Result ----------
    private void CompleteAndSpawnResult()
    {
        completed = true;

        // Hide snake visuals
        headImage.gameObject.SetActive(false);
        for (int i = 0; i < images.Count; i++) if (images[i]) images[i].gameObject.SetActive(false);

        // Spawn the draggable UI result token
        SpawnResultToken();

        // Enable Take Zone & hints
        if (takeZone) takeZone.Enable(true);
        SetHint(hintCollect, true);
        SetHint(hintDropSeeds, false);

        if (verbose) Debug.Log("[SnakeStation] Result spawned; drag to TakeZone.");
    }

    private void SpawnResultToken()
    {
        if (!resultTokenPrefab) { Debug.LogError("[SnakeStation] resultTokenPrefab not assigned."); return; }

        var parent = resultTokenSpawnParent ? resultTokenSpawnParent : (Transform)playArea ?? transform;
        spawnedResultToken = Instantiate(resultTokenPrefab, parent);
        spawnedResultToken.SetActive(true);
        spawnedResultToken.tag = "Result";
        spawnedResultToken.transform.SetAsLastSibling(); // ensure on top

        var rt = spawnedResultToken.GetComponent<RectTransform>() ?? spawnedResultToken.AddComponent<RectTransform>();
        rt.anchoredPosition = resultSpawnAnchoredPos;

        var img = spawnedResultToken.GetComponent<Image>();
        if (img)
        {
            // If your ItemSO has an icon (e.g., resultItem.icon), prefer it; else use override if provided
            Sprite icon = resultIconOverride ? resultIconOverride : img.sprite;
            img.sprite = icon;
        }
    }

    public void HandleResultDrop(DropSlotSnake slot, GameObject item)
    {
        if (!(slot.acceptsTag == "Result" && item.CompareTag("Result"))) return;

        var drag = item.GetComponent<DraggableItem>();
        if (drag) drag.Consume(); else item.SetActive(false);

        var eq = EquipmentInventory.Instance;
        if (eq && resultItem)
        {
            bool equipped = eq.TryEquip(eq.activeHand, resultItem) || eq.TryEquipToFirstAvailable(resultItem);
            if (!equipped) Debug.LogWarning("[SnakeStation] Inventory full; could not equip result.");
        }

        CloseUI();
    }

    // ---------- Close ----------
    public void CloseUI()
    {
        onClosed?.Invoke();

        if (disableInsteadOfDestroy)
        {
            if (owningRoot) owningRoot.SetActive(false);
            else if (owningCanvas) owningCanvas.gameObject.SetActive(false);
            else gameObject.SetActive(false);
        }
        else
        {
            if (owningRoot) Destroy(owningRoot);
            else if (owningCanvas) Destroy(owningCanvas.gameObject);
            else Destroy(gameObject);
        }
    }

    public void CancelAndClose() => CloseUI();
}
