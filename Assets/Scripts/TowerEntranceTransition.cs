using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class TowerEntranceTransition : MonoBehaviour
{
    [Header("Player in this scene")]
    [SerializeField] private Rigidbody2D playerBody;
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private CharacterStats playerStats;

    [Header("Unlock after guard victory")]
    [SerializeField] private Monster guard;
    [SerializeField] private BattleManager battleManager;
    [Tooltip("The courtyard's solid EntranceBlocker collider, NOT this trigger.")]
    [SerializeField] private Collider2D exteriorBlocker;

    [Header("Interior destination")]
    [SerializeField] private Transform interiorSpawn;
    [SerializeField] private SpriteRenderer interiorBackground;
    [SerializeField] private CameraFollow cameraFollow;
    [Tooltip("The interior's solid EntranceBlocker collider. Kept enabled.")]
    [SerializeField] private Collider2D interiorBlocker;

    private BoxCollider2D entrance;
    private bool unlocked;
    private bool used;

    private void Awake()
    {
        entrance = GetComponent<BoxCollider2D>();
        entrance.isTrigger = true;
        if (playerBody == null || playerCollider == null || playerInput == null ||
            playerStats == null || guard == null || battleManager == null ||
            exteriorBlocker == null || interiorSpawn == null || interiorBackground == null ||
            cameraFollow == null || interiorBlocker == null)
        {
            Debug.LogError("Tower entrance: assign every reference in the Inspector.", this);
            enabled = false;
            return;
        }
        if (exteriorBlocker == entrance || interiorBlocker == entrance ||
            exteriorBlocker == interiorBlocker || playerCollider.attachedRigidbody != playerBody ||
            playerStats.gameObject != playerBody.gameObject || playerInput.gameObject != playerBody.gameObject)
        {
            Debug.LogError("Tower entrance: use separate entrance/blocker colliders and components belonging to the same player.", this);
            enabled = false;
            return;
        }
        exteriorBlocker.isTrigger = false;
        exteriorBlocker.enabled = true;
        interiorBlocker.isTrigger = false;
        interiorBlocker.enabled = true;
    }

    private void Update()
    {
        if (used || unlocked || battleManager.IsInBattle || guard == null) return;
        // The supplied BattleManager disables the defeated monster in FinishVictory,
        // after awarding XP/money and closing the battle HUD. Fleeing does not unlock.
        if (!guard.IsDefeated || guard.gameObject.activeSelf) return;
        unlocked = true;
        exteriorBlocker.enabled = false;
        Debug.Log("Tower entrance unlocked. Walk into the black opening.", this);
    }

    private void FixedUpdate()
    {
        if (!unlocked || used || battleManager.IsInBattle || Time.timeScale <= 0f ||
            !playerInput.isActiveAndEnabled || playerStats.CurrentHP <= 0 ||
            !entrance.enabled || !playerCollider.enabled ||
            !playerBody.gameObject.activeInHierarchy) return;
        ColliderDistance2D overlap = entrance.Distance(playerCollider);
        if (!overlap.isValid || !overlap.isOverlapped) return;

        if (!interiorBackground.gameObject.activeInHierarchy || !interiorBackground.enabled ||
            interiorBackground.sprite == null || !cameraFollow.isActiveAndEnabled)
        {
            Debug.LogError("Enable the interior background and CameraFollow before entering.", this);
            enabled = false;
            return;
        }

        used = true;
        Vector3 destination = interiorSpawn.position;
        destination.z = playerBody.transform.position.z;
        playerBody.linearVelocity = Vector2.zero;
        playerBody.angularVelocity = 0f;
        playerBody.position = new Vector2(destination.x, destination.y);
        playerBody.transform.position = destination;
        Physics2D.SyncTransforms();
        cameraFollow.SetTarget(playerBody.transform);
        cameraFollow.SetMapBackground(interiorBackground); // Snaps immediately, preserving camera Z.
        interiorBlocker.enabled = true;
        entrance.enabled = false; // One-way transition for this scene visit.
        Debug.Log("Entered the tower chamber.", this);
    }
}
