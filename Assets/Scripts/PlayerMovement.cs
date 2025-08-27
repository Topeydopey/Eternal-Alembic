using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float MoveSpeed;
    public Rigidbody2D body;

    public InputActionReference move; // Link this to the "Move" InputAction in the Inspector

    private Vector2 _moveDirection;

    private void OnEnable()
    {
        move.action.Enable();
    }

    private void OnDisable()
    {
        move.action.Disable();
    }

    private void Update()
    {
        _moveDirection = move.action.ReadValue<Vector2>();

        // Normalize so diagonals aren't faster (0,0 stays (0,0))
        if (_moveDirection.sqrMagnitude > 0)
            _moveDirection = _moveDirection.normalized;

        float xInput = _moveDirection.x;
        float yInput = _moveDirection.y;

        if (Mathf.Abs(xInput) > 0)
        {
            body.linearVelocity = new Vector2(xInput * MoveSpeed, body.linearVelocity.y);
        }

        if (Mathf.Abs(yInput) > 0)
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, yInput * MoveSpeed);
        }

        /*
                // Optional: if there's no input at all, stop moving
                if (xInput == 0 && yInput == 0)
                {
                    body.linearVelocity = Vector2.zero;
                }
        */
    }
}