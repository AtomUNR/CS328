//make with ai help 
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class CharacterMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Animation")]
    [SerializeField] private float secondsPerFrame = 0.18f;

    [Header("Idle Sprites")]
    [SerializeField] private Sprite idleDown;
    [SerializeField] private Sprite idleUp;
    [SerializeField] private Sprite idleLeft;
    [SerializeField] private Sprite idleRight;

    [Header("Walking Sprites")]
    [SerializeField] private Sprite[] walkDown;
    [SerializeField] private Sprite[] walkUp;
    [SerializeField] private Sprite[] walkLeft;
    [SerializeField] private Sprite[] walkRight;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    private Vector2 moveInput;
    private Vector2 facingDirection = Vector2.down;

    private float animationTimer;
    private int frameIndex;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // Called by Player Input using Send Messages
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
    }

    private void Update()
    {
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = moveInput * moveSpeed;
    }

    private void UpdateAnimation()
    {
        if (moveInput.sqrMagnitude < 0.01f)
        {
            animationTimer = 0f;
            frameIndex = 0;
            spriteRenderer.sprite = GetIdleSprite();
            return;
        }

        Vector2 newDirection = GetDirection(moveInput);

        if (newDirection != facingDirection)
        {
            facingDirection = newDirection;
            frameIndex = 0;
            animationTimer = 0f;
        }

        Sprite[] frames = GetWalkSprites();

        if (frames == null || frames.Length == 0)
            return;

        animationTimer += Time.deltaTime;

        if (animationTimer >= secondsPerFrame)
        {
            animationTimer -= secondsPerFrame;
            frameIndex = (frameIndex + 1) % frames.Length;
        }

        spriteRenderer.sprite = frames[frameIndex];
    }

    private Vector2 GetDirection(Vector2 input)
    {
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            return input.x > 0 ? Vector2.right : Vector2.left;

        return input.y > 0 ? Vector2.up : Vector2.down;
    }

    private Sprite[] GetWalkSprites()
    {
        if (facingDirection == Vector2.up)
            return walkUp;

        if (facingDirection == Vector2.down)
            return walkDown;

        if (facingDirection == Vector2.left)
            return walkLeft;

        return walkRight;
    }

    private Sprite GetIdleSprite()
    {
        if (facingDirection == Vector2.up)
            return idleUp;

        if (facingDirection == Vector2.down)
            return idleDown;

        if (facingDirection == Vector2.left)
            return idleLeft;

        return idleRight;
    }
}
