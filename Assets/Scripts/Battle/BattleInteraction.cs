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

    // After a battle ends, ignore Z for a moment, then ignore presses that follow another press too
    // quickly. Otherwise mashing Z through the victory dialogue starts the next fight (for example
    // the boss, whose trigger covers the whole room) the moment the last enemy is beaten.
    private const float PostBattleCooldown = 1.5f;   // seconds Z is ignored after a battle
    private const float MashWindow = 5f;             // how long after a battle the mash filter applies
    private const float MashGap = 0.35f;             // a deliberate press needs this long since the last one

    private static int trackedFrame = -1;
    private static float lastZTime = -999f, previousZTime = -999f;

    // Shared by every enemy so each Z press is recorded once per frame, even during battles.
    private static void TrackZ()
    {
        if (trackedFrame == Time.frameCount) return;
        trackedFrame = Time.frameCount;
        Keyboard keys = Keyboard.current;
        if (keys == null || !keys.zKey.wasPressedThisFrame) return;
        previousZTime = lastZTime;
        lastZTime = Time.unscaledTime;
    }

    private bool PressAllowedAfterRecentBattle()
    {
        float sinceBattle = Time.unscaledTime - battleManager.LastBattleEndedAt;
        if (sinceBattle < PostBattleCooldown) return false;
        if (sinceBattle < MashWindow && lastZTime - previousZTime < MashGap) return false;
        return true;
    }

    private void Update()
    {
        TrackZ();

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

        if (Keyboard.current.zKey.wasPressedThisFrame && PressAllowedAfterRecentBattle())
            battleManager.StartBattle(player, monster);
    }

    private void OnDisable()
    {
        nearbyPlayerColliders.Clear();
    }
}