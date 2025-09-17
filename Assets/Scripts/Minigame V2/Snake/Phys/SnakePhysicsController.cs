using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class SnakePhysicsController : MonoBehaviour
{
    public enum DriveMode { AISeekTargets, PlayerSteer }

    [Header("Movement")]
    [Min(0f)] public float maxSpeed = 5f;
    [Min(0f)] public float maxForce = 30f;
    [Min(0f)] public float arriveRadius = 0.15f;
    [Min(0f)] public float slowRadius = 1.2f;
    [Min(0f)] public float faceTurnSpeed = 720f;
    public bool faceVelocity = true;

    [Header("Game Logic")]
    [Min(1)] public int seedsToWin = 5;
    public bool autoFreezeOnComplete = true;
    public bool clearTargetsOnComplete = true;

    [Header("Mode")]
    public DriveMode driveMode = DriveMode.PlayerSteer;   // <-- default to player

    // Events
    public event Action<int> OnSeedsEaten;
    public event Action OnCompleted;

    // Runtime
    private readonly Queue<Vector2> targets = new();
    private Rigidbody2D rb;
    private Vector2? currentTarget;
    private float zKeep;

    private int  seedsEaten = 0;
    private bool allowTargets = true;
    private bool frozen = false;
    private bool completedFired = false;

    // Player input (set by SnakeMouseDrive)
    private Vector2 inputHeading = Vector2.zero;
    private float  inputThrottle = 0f;

    public int  SeedsEaten => seedsEaten;
    public bool IsFrozen   => frozen;
    public bool IsComplete => seedsEaten >= Mathf.Max(1, seedsToWin);

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
#if UNITY_6_0_OR_NEWER
        rb.linearDamping = 4f;
        rb.angularDamping = 5f;
#else
        rb.linearDamping = 4f;
        rb.angularDamping = 5f;
#endif
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        zKeep = transform.position.z;
    }

    void FixedUpdate()
    {
        // keep z flat
        var p2 = transform.position; p2.z = zKeep; transform.position = p2;

        if (frozen)
        {
#if UNITY_6_0_OR_NEWER
            rb.linearVelocity = Vector2.zero;
#else
            rb.linearVelocity = Vector2.zero;
#endif
            return;
        }

        if (driveMode == DriveMode.PlayerSteer)
        {
            Step_PlayerSteer();
        }
        else
        {
            Step_AISeek();
        }
    }

    private void Step_PlayerSteer()
    {
        // desired velocity from input
        Vector2 desiredVel = inputHeading.normalized * (maxSpeed * Mathf.Clamp01(inputThrottle));

#if UNITY_6_0_OR_NEWER
        Vector2 curVel = rb.linearVelocity;
#else
        Vector2 curVel = rb.linearVelocity;
#endif
        Vector2 steer = Vector2.ClampMagnitude(desiredVel - curVel, maxForce);
        rb.AddForce(steer, ForceMode2D.Force);

#if UNITY_6_0_OR_NEWER
        rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, maxSpeed);
        Vector2 v = rb.linearVelocity;
#else
        rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, maxSpeed);
        Vector2 v = rb.linearVelocity;
#endif
        if (faceVelocity && v.sqrMagnitude > 0.0001f)
        {
            float ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            float z = Mathf.MoveTowardsAngle(transform.eulerAngles.z, ang, faceTurnSpeed * Time.fixedDeltaTime);
            transform.rotation = Quaternion.Euler(0, 0, z);
        }
    }

    private void Step_AISeek()
    {
        if (currentTarget == null && targets.Count > 0)
            currentTarget = targets.Dequeue();

        if (currentTarget == null)
        {
#if UNITY_6_0_OR_NEWER
            rb.linearVelocity *= 0.98f;
#else
            rb.linearVelocity *= 0.98f;
#endif
            return;
        }

        Vector2 pos = rb.position;
        Vector2 tgt = currentTarget.Value;
        Vector2 to = tgt - pos;
        float dist = to.magnitude;

        if (dist <= arriveRadius)
        {
            currentTarget = null;
#if UNITY_6_0_OR_NEWER
            rb.linearVelocity = Vector2.zero;
#else
            rb.linearVelocity = Vector2.zero;
#endif
            return;
        }

        Vector2 dir = (dist > 0.0001f) ? (to / dist) : Vector2.zero;
        float desiredSpeed = (dist < slowRadius)
            ? Mathf.Lerp(0.1f, maxSpeed, dist / Mathf.Max(0.0001f, slowRadius))
            : maxSpeed;

        Vector2 desiredVel = dir * desiredSpeed;

#if UNITY_6_0_OR_NEWER
        Vector2 curVel = rb.linearVelocity;
#else
        Vector2 curVel = rb.linearVelocity;
#endif
        Vector2 steer = Vector2.ClampMagnitude(desiredVel - curVel, maxForce);
        rb.AddForce(steer, ForceMode2D.Force);

#if UNITY_6_0_OR_NEWER
        rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, maxSpeed);
        Vector2 v = rb.linearVelocity;
#else
        rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, maxSpeed);
        Vector2 v = rb.linearVelocity;
#endif
        if (faceVelocity && v.sqrMagnitude > 0.0001f)
        {
            float ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            float z = Mathf.MoveTowardsAngle(transform.eulerAngles.z, ang, faceTurnSpeed * Time.fixedDeltaTime);
            transform.rotation = Quaternion.Euler(0, 0, z);
        }
    }

    // --- Called by SnakeMouseDrive each frame ---
    public void SetPlayerInput(Vector2 heading, float throttle)
    {
        inputHeading = heading;
        inputThrottle = Mathf.Clamp01(throttle);
    }

    // --- Public API used elsewhere ---
    public void EnqueueTarget(Vector2 worldPos)
    {
        if (!allowTargets || frozen || IsComplete || driveMode != DriveMode.AISeekTargets) return;
        targets.Enqueue(worldPos);
    }

    public void NotifyAteSeed(SeedWorld seed)
    {
        if (seed) Destroy(seed.gameObject);
        seedsEaten = Mathf.Max(0, seedsEaten + 1);
        OnSeedsEaten?.Invoke(seedsEaten);

        if (IsComplete && !completedFired)
        {
            completedFired = true;
            allowTargets = false;

            if (clearTargetsOnComplete) { currentTarget = null; targets.Clear(); }
            if (autoFreezeOnComplete) SetFrozen(true);

            OnCompleted?.Invoke();
        }
    }

    public void SetFrozen(bool on)
    {
        frozen = on;
#if UNITY_6_0_OR_NEWER
        if (on) rb.linearVelocity = Vector2.zero;
#else
        if (on) rb.linearVelocity = Vector2.zero;
#endif
    }

    public void SetDriveMode(DriveMode mode) => driveMode = mode;

    public void ResetSession(int newSeedsToWin = -1)
    {
        if (newSeedsToWin > 0) seedsToWin = newSeedsToWin;

        seedsEaten   = 0;
        allowTargets = true;
        frozen       = false;
        completedFired = false;

        inputHeading = Vector2.zero;
        inputThrottle = 0f;

        currentTarget = null;
        targets.Clear();

#if UNITY_6_0_OR_NEWER
        rb.linearVelocity = Vector2.zero;
#else
        rb.linearVelocity = Vector2.zero;
#endif
        var p = transform.position; p.z = zKeep; transform.position = p;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        seedsToWin = Mathf.Max(1, seedsToWin);
        slowRadius = Mathf.Max(arriveRadius, slowRadius);
    }
#endif
}
