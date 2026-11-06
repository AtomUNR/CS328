using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField, Min(0f)] private float moveSpeed = 4f;

    private Rigidbody2D rb;
    private Animator animator;
    private InputAction moveAction;
    private Vector2 moveInput;

    private string currentAnimation = "WalkDown";
    private bool isWalking = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        if (inputActions == null)
        {
            Debug.LogError("Assign Battle Tower Controls.", this);
            enabled = false;
            return;
        }

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

    private void Start()
    {
        // Start standing still, facing down
        animator.Play("WalkDown", 0, 0f);
        animator.Update(0f);
        animator.speed = 0f;
    }

    private void Update()
    {
        if (moveAction != null)
        {
            moveInput = Vector2.ClampMagnitude(
                moveAction.ReadValue<Vector2>(), 1f);
        }

        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        // Move the player
        rb.linearVelocity = moveInput * moveSpeed;
    }

    private void UpdateAnimation()
    {
        // Standing still
        if (moveInput.sqrMagnitude < 0.01f)
        {
            if (isWalking)
            {
                animator.speed = 1f;
                animator.Play(currentAnimation, 0, 0f);
                animator.Update(0f);
            }

            animator.speed = 0f;
            isWalking = false;
            return;
        }

        string nextAnimation;

        // Walking left or right
        if (Mathf.Abs(moveInput.x) >= Mathf.Abs(moveInput.y))
        {
            if (moveInput.x < 0)
                nextAnimation = "WalkLeft";
            else
                nextAnimation = "WalkRight";
        }
        // Walking up or down
        else
        {
            if (moveInput.y > 0)
                nextAnimation = "WalkUp";
            else
                nextAnimation = "WalkDown";
        }

        animator.speed = 1f;

        // Switch animations when direction changes
        if (currentAnimation != nextAnimation || !isWalking)
        {
            animator.Play(nextAnimation, 0, 0f);
            currentAnimation = nextAnimation;
        }

        isWalking = true;
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        moveInput = Vector2.zero;

        if (animator != null)
            animator.speed = 1f;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    private void OnDestroy()
    {
        moveAction?.Dispose();
    }
}