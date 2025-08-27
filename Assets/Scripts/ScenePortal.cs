// ScenePortal.cs
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class ScenePortal : MonoBehaviour
{
    [Header("Destination")]
    public string sceneToLoad;          // e.g. "Bog"
    public string destinationSpawnId;   // e.g. "FromHomeDoor"

    [Header("Interact")]
    public InputActionReference interact; // bind to <Keyboard>/e
    public float radius = 1.2f;
    public LayerMask playerMask;

    private void OnEnable() { interact.action.Enable(); }
    private void OnDisable() { interact.action.Disable(); }

    private void Update()
    {
        if (!interact.action.WasPressedThisFrame()) return;

        // simple proximity check (player within radius)
        var hit = Physics2D.OverlapCircle((Vector2)transform.position, radius, playerMask);
        if (!hit) return;

        // remember where to spawn in the next scene
        SpawnRouter.NextSpawnId = destinationSpawnId;

        // load the scene
        if (!string.IsNullOrEmpty(sceneToLoad))
            SceneManager.LoadScene(sceneToLoad);
        else
            Debug.LogWarning("ScenePortal: sceneToLoad is empty", this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
