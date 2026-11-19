//made with chatgpt
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D), typeof(Animator))]
public class CollapsingFloorTrap : MonoBehaviour
{
    [Header("Scene player")]
    public CharacterStats playerStats;
    public Collider2D playerCollider;
    public CharacterMovement playerMovement;
    public Transform roomEntranceRespawn;
    public Transform feetPoint;

    [Header("Timing")]
    [Min(0.1f)] public float collapseDelay = 2f;

    [Header("Damage")]
    [Min(1)] public int damage = 3;
    public string deathSceneName = "Game Over";

    private Animator animator;
    private BoxCollider2D area;
    private Rigidbody2D playerBody;

    private bool collapsing;
    private bool restoreMovement;
    private float elapsed;
    private Vector2 frozenPosition;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        area = GetComponent<BoxCollider2D>();
        area.isTrigger = true;

        if (playerStats == null || playerCollider == null ||
            playerMovement == null || roomEntranceRespawn == null)
        {
            Debug.LogError(
                name + ": assign the player references and respawn point.",
                this);

            enabled = false;
            return;
        }

        playerBody = playerCollider.attachedRigidbody;
    }

    private void Update()
    {
        bool canAnimate = collapsing ||
            (playerMovement != null &&
             playerMovement.isActiveAndEnabled);

        animator.speed = canAnimate ? 1f : 0f;
    }

    private void FixedUpdate()
    {
        if (collapsing)
        {
            // Keep the player still while the opening plays.
            if (playerBody != null)
            {
                playerBody.linearVelocity = Vector2.zero;
                playerBody.position = frozenPosition;
            }

            elapsed += Time.fixedDeltaTime;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

            bool animationFinished =
                state.IsName("FloorBreak") &&
                !animator.IsInTransition(0) &&
                state.normalizedTime >= 1f;

            if (elapsed >= collapseDelay && animationFinished)
                FinishCollapse();

            return;
        }

        if (playerStats.CurrentHP <= 0 ||
            !playerStats.gameObject.activeInHierarchy ||
            !playerMovement.isActiveAndEnabled ||
            !playerCollider.enabled || !area.enabled)
            return;

        // Activate only when the player's feet are inside the tile.
        if (feetPoint == null || !area.OverlapPoint(feetPoint.position))
            return;

        collapsing = true;
        elapsed = 0f;

        restoreMovement = playerMovement.enabled;
        frozenPosition = playerBody != null
            ? playerBody.position
            : (Vector2)playerMovement.transform.position;

        playerMovement.enabled = false;

        if (playerBody != null)
            playerBody.linearVelocity = Vector2.zero;

        animator.speed = 1f;
        animator.SetTrigger("Break");
    }

    private void FinishCollapse()
    {
        playerStats.TakeDamage(Mathf.Max(1, damage));

        Debug.Log(
            $"Floor damage: HP {playerStats.CurrentHP}/{playerStats.MaxHP}",
            this);

        if (playerStats.CurrentHP <= 0)
        {
            collapsing = false;

            if (string.IsNullOrWhiteSpace(deathSceneName))
                return;

            if (Application.CanStreamedLevelBeLoaded(deathSceneName))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(deathSceneName);
            }
            else
            {
                Debug.LogError(
                    "Add '" + deathSceneName + "' to the build scene list.",
                    this);
            }

            return;
        }

        // Respawn the player.
        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = roomEntranceRespawn.position;
        }
        else
        {
            playerMovement.transform.position =
                roomEntranceRespawn.position;
        }

        // Reset the tile immediately.
        animator.ResetTrigger("Break");
        animator.Play("FloorIdle", 0, 0f);
        animator.Update(0f);

        collapsing = false;
        elapsed = 0f;
        playerMovement.enabled = restoreMovement;
    }

    private void OnDisable()
    {
        if (collapsing && playerMovement != null &&
            playerStats != null && playerStats.CurrentHP > 0)
        {
            playerMovement.enabled = restoreMovement;
        }

        collapsing = false;

        if (animator != null)
            animator.speed = 0f;
    }
}