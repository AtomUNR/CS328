using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Monster))]
public class BattleInteraction : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private Player player;

    [Header("Enemy")]
    [SerializeField] private Monster monster;

    private readonly HashSet<Collider2D> nearbyPlayerColliders =
        new HashSet<Collider2D>();

    private void Awake()
    {
        if (monster == null)
            monster = GetComponent<Monster>();
    }

    private void Start()
    {
        if (battleManager == null || player == null || monster == null)
        {
            Debug.LogError(
                "Assign Battle Manager, Player, and Monster.",
                this
            );

            enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (player != null &&
            other.GetComponentInParent<Player>() == player)
        {
            nearbyPlayerColliders.Add(other);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        nearbyPlayerColliders.Remove(other);
    }

    private void Update()
    {
        if (battleManager == null || player == null || monster == null ||
            battleManager.IsInBattle || monster.IsDefeated || Time.timeScale == 0f)
            return;

        nearbyPlayerColliders.RemoveWhere(
            collider => collider == null ||
                        !collider.enabled ||
                        !collider.gameObject.activeInHierarchy
        );

        if (nearbyPlayerColliders.Count == 0 || Keyboard.current == null)
            return;

        ExplorationInventoryMenu inventoryMenu = player.GetComponent<ExplorationInventoryMenu>();
        if (inventoryMenu != null && inventoryMenu.IsOpen)
            return;

        // Give inventory open/close priority even if both keys arrive this frame.
        // This avoids depending on which component's Update runs first.
        if (Keyboard.current.cKey.wasPressedThisFrame || Keyboard.current.xKey.wasPressedThisFrame)
            return;

        if (Keyboard.current.zKey.wasPressedThisFrame)
            battleManager.StartBattle(player, monster);
    }

    private void OnDisable()
    {
        nearbyPlayerColliders.Clear();
    }
}