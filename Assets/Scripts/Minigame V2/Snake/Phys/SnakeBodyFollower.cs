using System.Collections.Generic;
using UnityEngine;

/// IK-style chain for a snake body following a physics head.
/// Link scale control + auto segment length from sprite width.
/// Now exposes CoilNow() / StraightenNow() to reseed on demand.
public class SnakeBodyIKChain : MonoBehaviour
{
    [Header("Head")]
    public Rigidbody2D headRb;
    public Transform headTransformFallback;

    [Header("Body Visuals")]
    public GameObject linkPrefab;          // SpriteRenderer-only
    public Sprite bodySprite;
    public Sprite tailSprite;
    public string sortingLayerName = "";
    public int baseSortingOrder = 0;

    [Header("Visual Scale")]
    [Tooltip("Scale to apply to each link sprite.")]
    public Vector2 linkScale = Vector2.one;
    [Tooltip("If true, set link localScale = linkScale. If false, multiply the prefab's localScale by linkScale.")]
    public bool overridePrefabScale = true;

    [Header("Chain Shape")]
    public int linkCount = 18;
    public float segmentLength = 0.35f;    // spacing between links (world units)
    public float rotationOffsetDeg = -90f;
    public float segmentZ = -0.01f;

    [Header("Auto Spacing From Sprite")]
    [Tooltip("Set segmentLength from the first link sprite's world width after scaling.")]
    public bool autoSegmentLengthFromSprite = true;
    [Tooltip("Multiply the measured width (1 = butt-to-butt, 0.9 = slight overlap, 1.2 = gap).")]
    public float spacingScale = 1.0f;

    [Header("Solver")]
    [Range(0.05f, 1f)] public float followT = 0.6f;
    [Range(1, 4)] public int solverIterations = 1;

    [Header("Start Coil")]
    public bool startCoiled = true;
    public bool autoRadiusFromLength = true;
    public float coilRadius = 1.2f;
    public float coilTurns = 1f;
    public bool clockwise = true;
    public bool spiralInwards = false;

    // runtime
    private Transform[] links;
    private Vector2[] pos;

    private Vector2 HeadPos =>
        headRb ? headRb.position :
        (headTransformFallback ? (Vector2)headTransformFallback.position : (Vector2)transform.position);

    void Awake()
    {
        BuildLinks();
        SeedPositions();
    }

    void LateUpdate()
    {
        if (links == null || links.Length == 0) return;

        pos[0] = HeadPos;

        for (int iter = 0; iter < solverIterations; iter++)
        {
            for (int i = 1; i < pos.Length; i++)
            {
                Vector2 toPrev = pos[i] - pos[i - 1];
                float d = toPrev.magnitude;
                if (d < 1e-5f) { pos[i] = pos[i - 1] - Vector2.right * segmentLength; continue; }

                Vector2 dir = toPrev / d;
                Vector2 desired = pos[i - 1] + dir * segmentLength;
                pos[i] = Vector2.Lerp(pos[i], desired, followT);
            }
        }

        for (int i = 1; i < links.Length; i++)
        {
            var t = links[i];
            var p = pos[i];
            var pp = pos[i - 1];

            t.position = new Vector3(p.x, p.y, segmentZ);

            Vector2 dir = pp - p;
            if (dir.sqrMagnitude > 0.00001f)
            {
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                t.rotation = Quaternion.Euler(0, 0, ang + rotationOffsetDeg);
            }
        }
    }

    // -------- build & seed --------

    private void BuildLinks()
    {
        // destroy old visuals (keep array slot 0 for head anchor)
        if (links != null)
            for (int i = 1; i < links.Length; i++)
                if (links[i]) Destroy(links[i].gameObject);

        links = new Transform[Mathf.Max(1, linkCount + 1)];
        pos = new Vector2[links.Length];

        // slot 0: virtual head anchor
        links[0] = new GameObject("HeadAnchor").transform;
        links[0].SetParent(transform, false);
        links[0].gameObject.hideFlags = HideFlags.HideInHierarchy;

        // ensure prefab
        if (!linkPrefab)
        {
            var go = new GameObject("Link");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = bodySprite;
            linkPrefab = go;
        }

        // instantiate links
        for (int i = 1; i < links.Length; i++)
        {
            var go = Instantiate(linkPrefab, transform);
            go.name = (i == links.Length - 1) ? "Tail" : $"Body_{i}";

            var sr = go.GetComponent<SpriteRenderer>() ?? go.AddComponent<SpriteRenderer>();
            sr.sprite = (i == links.Length - 1 && tailSprite) ? tailSprite : (bodySprite ? bodySprite : sr.sprite);

            if (!string.IsNullOrEmpty(sortingLayerName)) sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = baseSortingOrder + i;

            // apply visual scale
            if (overridePrefabScale) go.transform.localScale = new Vector3(linkScale.x, linkScale.y, 1f);
            else go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(linkScale.x, linkScale.y, 1f));

            // initial depth
            var tp = go.transform.position; tp.z = segmentZ; go.transform.position = tp;

            links[i] = go.transform;

            // set spacing from the first link's **visible** width
            if (autoSegmentLengthFromSprite && i == 1 && sr && sr.sprite)
            {
                // world width = (pixels / PPU) * lossyScale.x
                float widthWorld = (sr.sprite.rect.width / sr.sprite.pixelsPerUnit) * go.transform.lossyScale.x;
                segmentLength = Mathf.Max(0.01f, widthWorld * spacingScale);
            }
        }
    }

    private void SeedPositions()
    {
        Vector2 head = HeadPos;
        pos[0] = head;
        for (int i = 1; i < pos.Length; i++) pos[i] = head - Vector2.right * (segmentLength * i);

        if (!startCoiled) { WriteInitialTransforms(); return; }

        float totalLen = (pos.Length - 1) * segmentLength;
        float r = autoRadiusFromLength
            ? Mathf.Max(0.05f, totalLen / (2f * Mathf.PI * Mathf.Max(0.25f, coilTurns)))
            : Mathf.Max(0.05f, coilRadius);

        Vector2 center = head - new Vector2(r, 0f);

        // chord spacing
        float dTheta = 2f * Mathf.Asin(Mathf.Clamp(segmentLength / (2f * r), 0f, 1f));
        if (dTheta < 1e-3f) dTheta = segmentLength / Mathf.Max(r, 0.0001f);
        dTheta *= (clockwise ? -1f : 1f);

        float radiusDrop = (spiralInwards ? (r * 0.6f) / (pos.Length - 1) : 0f);
        float angle = 0f;

        for (int i = 1; i < pos.Length; i++)
        {
            float ri = Mathf.Max(0.05f, r - radiusDrop * i);
            angle += dTheta;
            Vector2 p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ri;
            pos[i] = p;
        }

        WriteInitialTransforms();
    }

    private void WriteInitialTransforms()
    {
        for (int i = 1; i < links.Length; i++)
        {
            var t = links[i];
            var p = pos[i]; var pp = pos[i - 1];
            t.position = new Vector3(p.x, p.y, segmentZ);
            Vector2 dir = pp - p;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            t.rotation = Quaternion.Euler(0, 0, ang + rotationOffsetDeg);
        }
    }

    // ----- public reseed helpers -----

    /// <summary>Immediately reseed into a coil using the current head position.</summary>
    public void CoilNow()
    {
        startCoiled = true;
        SeedPositions();
    }

    /// <summary>Immediately reseed into a straight chain using the current head position.</summary>
    public void StraightenNow()
    {
        startCoiled = false;
        SeedPositions();
    }

    // handy in-Editor buttons
    [ContextMenu("Reseed → Coil")]
    private void _CM_Coil() => CoilNow();
    [ContextMenu("Reseed → Straight")]
    private void _CM_Straight() => StraightenNow();
}
