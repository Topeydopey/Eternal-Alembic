using UnityEngine;
using UnityEngine.InputSystem;

public class MovementProbe : MonoBehaviour
{
    public Rigidbody2D rb;
    public InputActionReference move;

    void Awake() { if (!rb) rb = GetComponent<Rigidbody2D>(); }

    void Update()
    {
        var v = move.action.enabled ? move.action.ReadValue<Vector2>() : Vector2.zero;
        Debug.Log($"[Probe] enabled:{move.action.enabled} map:{move.action.actionMap.enabled} dir:{v} rbType:{rb.bodyType} sim:{rb.simulated} vel:{rb.linearVelocity} ts:{Time.timeScale}");
    }
}
