using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField, Min(0f)] private float moveSpeed = 4f;

    private Rigidbody2D rb;
    private InputAction moveAction;
    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (inputActions == null)
        {
            Debug.LogError("Assign Battle Tower Controls.", this);
            enabled = false;
            return;
        }

        // Own a copy so enabling/disabling movement does not
        // change the action used by another component.
        InputAction sourceAction =
            inputActions.FindAction("Gameplay/Move", false);

        if (sourceAction == null)
        {
            Debug.LogError("Could not find Gameplay/Move.", this);
            enabled = false;
            return;
        }

        moveAction = sourceAction.Clone();
    }

    private void OnEnable()
    {
        moveAction?.Enable();
    }

    private void Update()
    {
        if (moveAction != null)
        {
            moveInput = Vector2.ClampMagnitude(
                moveAction.ReadValue<Vector2>(), 1f);
        }
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = moveInput * moveSpeed;
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        moveInput = Vector2.zero;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    private void OnDestroy()
    {
        moveAction?.Dispose();
    }
}