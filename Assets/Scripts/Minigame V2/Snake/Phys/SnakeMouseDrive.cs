using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;   // Mouse.current
#endif

public class SnakeMouseDrive : MonoBehaviour
{
    public Camera worldCamera;
    public SnakePhysicsController snake;

    [Header("Control")]
    [Range(0f, 1f)] public float throttleWhileHeld = 1f;
    public float deadzoneRadius = 0.05f;
    public bool blockWhenPointerOverUI = false; // set true if UI should block steering

    void Awake()
    {
        if (!worldCamera) worldCamera = Camera.main;
    }

    void OnEnable()
    {
        if (snake) snake.SetDriveMode(SnakePhysicsController.DriveMode.PlayerSteer);
    }

    void Update()
    {
        if (!snake || !worldCamera) return;

        // --- Read mouse with whichever input backend is active ---
        Vector2 screen;
        bool held = false;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            screen = Mouse.current.position.ReadValue();
            held   = Mouse.current.leftButton.isPressed;
        }
        else
        {
            // No mouse device -> no input
            snake.SetPlayerInput(Vector2.zero, 0f);
            return;
        }
#else
        screen = Input.mousePosition;
        held = Input.GetMouseButton(0);
#endif

        if (blockWhenPointerOverUI && EventSystem.current && EventSystem.current.IsPointerOverGameObject())
            held = false;

        // Convert to world
        Vector3 w3 = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -worldCamera.transform.position.z));
        w3.z = 0f;

        Vector2 head = snake.transform.position;
        Vector2 to = (Vector2)w3 - head;
        float dist = to.magnitude;

        Vector2 heading = (dist > 0.0001f) ? (to / dist) : Vector2.zero;
        float throttle = (held && dist >= deadzoneRadius) ? throttleWhileHeld : 0f;

        snake.SetPlayerInput(heading, throttle);
    }
}
