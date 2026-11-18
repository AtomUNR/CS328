using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
public class BattleManager : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private BattleHudController battleHUD;
    [SerializeField] private AttackTimer attackTiming;

    [SerializeField] private DodgePhaseController dodgePhase;
    private enum BattleState
    {
        Inactive,
        ChoosingCommand,
        ChoosingAttack,
        ChoosingTarget,
        TimingAttack,
        Dodging,
        PlayerAttackMessage,
        EnemyAttackMessage,
        DefeatDialogue,
        VictoryMessage,
        PlayerDefeated,
        ChoosingItem,
        ItemUsedMessage
    }

    private BattleState state = BattleState.Inactive;

    private Player player;
    private Monster monster;
    private PlayerMovement playerMovement;
    private PlayerInventory inventory;
    private int selectedItemIndex;
    private float nextDodgeSpeedMultiplier = 1f;

    private bool movementWasEnabled;
    private int lastTransitionFrame = -1;

    private readonly Queue<string> defeatLines = new Queue<string>();

    public bool IsInBattle => state != BattleState.Inactive;

    private void OnEnable()
    {
        if (battleHUD != null)
        {
            battleHUD.CommandChosen += HandleCommand;
            battleHUD.CancelChosen += HandleCancel;
            battleHUD.ItemNavigation += HandleItemNavigation;
        }
        if(attackTiming != null)
        {
            attackTiming.AttackFinished += ResolveTimedAttack;
        }
        if(dodgePhase != null)
        {
            dodgePhase.PhaseFinished += HandleDodgeFinished;
        }
    }

    private void OnDisable()
    {
        if (battleHUD != null)
        {
            battleHUD.CommandChosen -= HandleCommand;
            battleHUD.CancelChosen -= HandleCancel;
            battleHUD.ItemNavigation -= HandleItemNavigation;
        }
        if (attackTiming != null)
        {
            attackTiming.AttackFinished -= HandleAttackFinished;
            attackTiming.CancelAttack();
        }

        if(attackTiming != null)
        {
            attackTiming.AttackFinished -= ResolveTimedAttack;
            attackTiming.CancelAttack();
        }
        if(dodgePhase != null)
        {
            dodgePhase.PhaseFinished -= HandleDodgeFinished;
        }

        EndBattle();
    }

    public bool StartBattle(Player newPlayer, Monster newMonster)
    {
        if (IsInBattle || Time.frameCount == lastTransitionFrame)
            return false;

        if (battleHUD == null || newPlayer == null || newMonster == null)
        {
            Debug.LogError("Battle references are missing.", this);
            return false;
        }

        if (newPlayer.Stats == null || newMonster.Stats == null)
        {
            Debug.LogError(
                "Both characters need CharacterStats.",
                this
            );

            return false;
        }

        if (newPlayer.Stats.CurrentHP <= 0 || newMonster.IsDefeated)
            return false;

        PlayerMovement movement =
            newPlayer.GetComponent<PlayerMovement>();

        if (movement == null)
        {
            Debug.LogError(
                "The Player needs PlayerMovement.",
                newPlayer
            );

            return false;
        }

        inventory = newPlayer.GetComponent<PlayerInventory>();
        nextDodgeSpeedMultiplier = 1f;
        player = newPlayer;
        monster = newMonster;
        playerMovement = movement;

        movementWasEnabled = playerMovement.enabled;
        playerMovement.enabled = false;

        defeatLines.Clear();
        monster.ResetDialogue();
        monster.ResetAttackSequence();

        state = BattleState.ChoosingCommand;
        lastTransitionFrame = Time.frameCount;

        RefreshPlayerStatus();

        battleHUD.BeginBattle(
            monster.BattleSprite,
            monster.DisplayName,
            monster.GetNextDialogue()
        );

        return true;
    }

    private void LateUpdate()
    {
        if (!IsInBattle)
            return;

        if (player == null || monster == null || battleHUD == null)
        {
            EndBattle();
            return;
        }

        RefreshPlayerStatus();
    }

    private void RefreshPlayerStatus()
    {
        battleHUD.SetPlayerStatus(
            player.DisplayName,
            player.Level,
            player.Stats.CurrentHP,
            player.Stats.MaxHP,
            player.Stats.CurrentMP,
            player.Stats.MaxMP
        );
    }

    private void HandleCommand(BattleCommand command)
    {
        if (!IsInBattle)
            return;

        // Prevent one press from passing through multiple screens.
        if (Time.frameCount == lastTransitionFrame)
            return;

        lastTransitionFrame = Time.frameCount;

        if (player == null || monster == null)
        {
            EndBattle();
            return;
        }
        if (state == BattleState.TimingAttack)
        {
            if (attackTiming != null)
                attackTiming.ConfirmHit();

            return;
        }
        // Outside the main menu, Z confirms or advances the current screen.
        if (state != BattleState.ChoosingCommand)
        {
            AdvanceBattle();
            return;
        }

        switch (command)
        {
            case BattleCommand.Fight:
                OpenAttackSelection();
                break;

            case BattleCommand.Item:
                OpenItemSelection();
                break;

            case BattleCommand.Stats:
                battleHUD.SetDialogue(
                    $"* {player.DisplayName} — LV {player.Level}\n" +
                    $"* ATK {player.AttackDamage} | Money {player.Money}"
                );
                break;

            case BattleCommand.Flee:
                EndBattle();
                break;
        }
    }

    private void HandleCancel()
    {
        if (!IsInBattle || Time.frameCount == lastTransitionFrame)
            return;

        switch (state)
        {
            case BattleState.ChoosingItem:
                lastTransitionFrame = Time.frameCount;
                state = BattleState.ChoosingCommand;
                battleHUD.ShowMainMenu();
                battleHUD.SetDialogue("* Choose your action.");
                break;

            case BattleState.ChoosingAttack:
                lastTransitionFrame = Time.frameCount;
                state = BattleState.ChoosingCommand;
                battleHUD.ShowMainMenu();
                break;

            case BattleState.ChoosingTarget:
                lastTransitionFrame = Time.frameCount;
                OpenAttackSelection();
                break;
        }
    }

    private void OpenItemSelection()
    {
        state = BattleState.ChoosingItem;
        selectedItemIndex = 0;
        RefreshItemSelection();
    }

    private void HandleItemNavigation(int direction)
    {
        if (state != BattleState.ChoosingItem || inventory == null || inventory.Count == 0)
            return;
        selectedItemIndex = (selectedItemIndex + direction + inventory.Count) % inventory.Count;
        RefreshItemSelection();
    }

    private void RefreshItemSelection(string notice = null)
    {
        if (inventory == null || inventory.Count == 0)
        {
            battleHUD.ShowItemSelection("Your inventory is empty.", "Press X to return.", false);
            return;
        }
        selectedItemIndex = Mathf.Clamp(selectedItemIndex, 0, inventory.Count - 1);
        ItemData item = inventory.Items[selectedItemIndex];
        string name = item != null ? item.ItemName : "Unavailable item";
        string effect = "Cannot use this item.";
        if (item != null)
        {
            switch (item.Effect)
            {
                case ItemEffect.RestoreHP:
                    effect = $"Restore {item.RestoreAmount} HP.";
                    break;
                case ItemEffect.RestoreMP:
                    effect = $"Restore {item.RestoreAmount} MP.";
                    break;
                case ItemEffect.RestoreHPAndDodgeSpeed:
                    effect = $"+{item.RestoreAmount} HP / {item.DodgeSpeedMultiplier:0.##}x dodge speed for 1 turn.";
                    break;
            }
        }
        string description = item != null && !string.IsNullOrWhiteSpace(item.Description)
            ? item.Description
            : effect;

        battleHUD.ShowItemSelection(
            $"[{selectedItemIndex + 1}/{inventory.Count}] {name}",
            notice ?? description,
            item != null);
    }

    private void UseSelectedItem()
    {
        if (inventory == null || inventory.Count == 0)
        {
            RefreshItemSelection();
            return;
        }
        if (player == null || player.Stats == null || player.Stats.CurrentHP <= 0)
            return;
        selectedItemIndex = Mathf.Clamp(selectedItemIndex, 0, inventory.Count - 1);
        ItemData item = inventory.Items[selectedItemIndex];
        if (item == null)
        {
            RefreshItemSelection("This item is unavailable.");
            return;
        }

        bool heal = item.Effect == ItemEffect.RestoreHP ||
                    item.Effect == ItemEffect.RestoreHPAndDodgeSpeed;
        bool magic = item.Effect == ItemEffect.RestoreMP;
        bool haste = item.Effect == ItemEffect.RestoreHPAndDodgeSpeed &&
                     item.DodgeSpeedMultiplier > 1f;
        int amount = Mathf.Max(0, item.RestoreAmount);
        bool helps = haste || (heal && amount > 0 && player.Stats.CurrentHP < player.Stats.MaxHP)
                           || (magic && amount > 0 && player.Stats.CurrentMP < player.Stats.MaxMP);
        if (!helps)
        {
            RefreshItemSelection("No effect right now; item kept.");
            return;
        }

        // Commit one inventory entry before applying effects; message state blocks reuse.
        if (!inventory.TryRemoveItem(selectedItemIndex))
        {
            RefreshItemSelection("Inventory changed; choose again.");
            return;
        }
        int oldHP = player.Stats.CurrentHP;
        int oldMP = player.Stats.CurrentMP;
        if (heal) player.Stats.RestoreHP(Mathf.Min(amount, player.Stats.MaxHP - oldHP));
        if (magic) player.Stats.RestoreMP(Mathf.Min(amount, player.Stats.MaxMP - oldMP));
        nextDodgeSpeedMultiplier = haste ? item.DodgeSpeedMultiplier : 1f;
        RefreshPlayerStatus();
        string result = heal ? $"Restored {player.Stats.CurrentHP - oldHP} HP."
                             : $"Restored {player.Stats.CurrentMP - oldMP} MP.";
        if (haste) result += $" Dodge speed: {nextDodgeSpeedMultiplier:0.##}x next turn.";
        ShowMessage($"* Used {item.ItemName}.\n* {result}\n* Press Z for the enemy turn.",
            BattleState.ItemUsedMessage);
    }

    private void OpenAttackSelection()
    {
        state = BattleState.ChoosingAttack;
        battleHUD.ShowAttackSelection();
    }

    private void OpenTargetSelection()
    {
        state = BattleState.ChoosingTarget;
        battleHUD.ShowTargetSelection(monster);
    }

    private void AdvanceBattle()
    {
        switch (state)
        {
            case BattleState.ChoosingItem:
                UseSelectedItem();
                break;

            case BattleState.ItemUsedMessage:
                EnemyAttack();
                break;

            case BattleState.ChoosingAttack:
                OpenTargetSelection();
                break;

            case BattleState.ChoosingTarget:
                BeginTimedAttack();
                break;

            case BattleState.TimingAttack:
                attackTiming.ConfirmHit();
                break;

            case BattleState.PlayerAttackMessage:
                if (monster.IsDefeated)
                    BeginDefeatDialogue();
                else
                    EnemyAttack();
                break;

            case BattleState.EnemyAttackMessage:
                if (player.Stats.CurrentHP <= 0)
                {
                    ShowMessage(
                        "* You were defeated.\n" +
                        "* Press Z to recover and return. (Test mode)",
                        BattleState.PlayerDefeated
                    );
                }
                else
                {
                    state = BattleState.ChoosingCommand;
                    battleHUD.ShowMainMenu();
                    battleHUD.SetDialogue(monster.GetNextDialogue());
                }
                break;

            case BattleState.DefeatDialogue:
                ShowNextDefeatLine();
                break;

            case BattleState.VictoryMessage:
                FinishVictory();
                break;

            case BattleState.PlayerDefeated:
                // Temporary recovery until a Game Over screen is added.
                OpenGameOver();
                //player.Stats.RestoreHP(player.Stats.MaxHP);
                //player.Stats.RestoreMP(player.Stats.MaxMP);
                //EndBattle();
                break;
            
            case BattleState.Dodging:
                break;
        }
    }

    // Retained for the next step, when the attack meter is connected.
    // No menu currently calls this method directly.
    private void PlayerAttack()
    {
        if (attackTiming == null)
        {
            Debug.LogError(
                "Assign Attack Timing on the BattleManager.",
                this
            );

            return;
        }

        state = BattleState.TimingAttack;
        battleHUD.HideActionPanels();

        if (!attackTiming.BeginAttack())
        {
            // Return to target selection if the meter could not start.
            OpenTargetSelection();
        }
    }
    private void HandleAttackFinished(float multiplier)
    {
        if (state != BattleState.TimingAttack ||
            player == null ||
            monster == null)
        {
            return;
        }

        int previousHP = monster.Stats.CurrentHP;

        int damage = Mathf.Max(
            0,
            Mathf.RoundToInt(player.AttackDamage * multiplier)
        );

        monster.Stats.TakeDamage(damage);

        int damageDealt = previousHP - monster.Stats.CurrentHP;

        ShowMessage(
            $"* You dealt {damageDealt} damage to " +
            $"{monster.DisplayName}.\n* Press Z to continue.",
            BattleState.PlayerAttackMessage
        );
    }

    private void EnemyAttack()
{
    if (dodgePhase == null)
    {
        Debug.LogError(
            "Assign Dodge Phase on BattleManager.",
            this
        );
        return;
    }

    EnemyAttackData attack = monster.GetNextAttack();

    if (attack == null)
    {
        Debug.LogError(
            "Assign at least one EnemyAttackData asset to this monster.",
            monster
        );
        return;
    }
        //animation script made with chatGPT
        Animator monsterAnimator = monster.GetComponent<Animator>();

        if (monsterAnimator != null)
        {
            monsterAnimator.enabled = true;
            monsterAnimator.Rebind();
            monsterAnimator.Play("Guardian_Spear_Thrust_12", 0, 0f);
        }

        if (!dodgePhase.BeginPhase(attack, monster.BattleSprite, player.Stats, nextDodgeSpeedMultiplier)){ return; } 

    nextDodgeSpeedMultiplier = 1f;
    state = BattleState.Dodging;
    lastTransitionFrame = Time.frameCount;

    battleHUD.ShowDodgePhase();
    }

    private void BeginDefeatDialogue()
    {
        defeatLines.Clear();

        foreach (string line in monster.GetDefeatDialogueLines())
        {
            if (!string.IsNullOrWhiteSpace(line))
                defeatLines.Enqueue(line);
        }

        if (defeatLines.Count == 0)
        {
            defeatLines.Enqueue(
                $"* {monster.DisplayName} was defeated."
            );
        }

        ShowNextDefeatLine();
    }

    private void ShowNextDefeatLine()
    {
        if (defeatLines.Count > 0)
        {
            ShowMessage(
                defeatLines.Dequeue(),
                BattleState.DefeatDialogue
            );

            return;
        }

        ShowMessage(
            $"* Victory! Rewards: {monster.XPReward} XP and " +
            $"{monster.MoneyReward} coins.\n* Press Z to collect.",
            BattleState.VictoryMessage
        );
    }

    private void FinishVictory()
    {
        Monster defeatedMonster = monster;

        player.GainXP(defeatedMonster.XPReward);
        player.AddMoney(defeatedMonster.MoneyReward);

        EndBattle();

        defeatedMonster.gameObject.SetActive(false);
    }

    private void ShowMessage(string message, BattleState nextState)
    {
        state = nextState;
        if (nextState == BattleState.ItemUsedMessage)
            battleHUD.ShowItemResult(message);
        else
        {
            battleHUD.ShowDialogueOnly();
            battleHUD.SetDialogue(message);
        }
    }

    public void EndBattle()
    {
        nextDodgeSpeedMultiplier = 1f;
        inventory = null;
        if(dodgePhase != null)
        {
            dodgePhase.CancelPhase();
        }
        if(attackTiming != null)
        {
            attackTiming.CancelAttack();
        }
        if (!IsInBattle)
            return;

        state = BattleState.Inactive;
        lastTransitionFrame = Time.frameCount;

        if (battleHUD != null)
            battleHUD.EndBattle();

        if (playerMovement != null)
            playerMovement.enabled = movementWasEnabled;

        defeatLines.Clear();

        player = null;
        monster = null;
        playerMovement = null;
    }
    private void ResolveTimedAttack(float multiplier)
{
    if (state != BattleState.TimingAttack)
        return;

    lastTransitionFrame = Time.frameCount;

    if (player == null || monster == null)
    {
        EndBattle();
        return;
    }

    if (multiplier <= 0f)
    {
        ShowMessage(
            "* Your attack missed!\n* Press Z to continue.",
            BattleState.PlayerAttackMessage
        );

        return;
    }

    int damage = Mathf.Max(
        1,
        Mathf.RoundToInt(player.AttackDamage * multiplier)
    );

    int previousHP = monster.Stats.CurrentHP;
    monster.Stats.TakeDamage(damage);

    int damageDealt = previousHP - monster.Stats.CurrentHP;

    ShowMessage(
        $"* You dealt {damageDealt} damage to " +
        $"{monster.DisplayName}.\n* Press Z to continue.",
        BattleState.PlayerAttackMessage
    );
    }
private void BeginTimedAttack()
{
    if (attackTiming == null)
    {
        Debug.LogError(
            "Assign Attack Timing on the BattleManager.",
            this
        );
        return;
    }

    state = BattleState.TimingAttack;
    battleHUD.HideActionPanels();

    if (!attackTiming.BeginAttack())
    {
        OpenTargetSelection();
    }
}
    private void HandleDodgeFinished()
{
    if (state != BattleState.Dodging)
        return;

    lastTransitionFrame = Time.frameCount;

    if (player == null || monster == null)
    {
        EndBattle();
        return;
    }

    RefreshPlayerStatus();

    if (player.Stats.CurrentHP <= 0)
    {
        ShowMessage(
            "* Your essence fades... \n Press Z to continue.",
            BattleState.PlayerDefeated
        );

        return;
    }

    state = BattleState.ChoosingCommand;
    battleHUD.ShowMainMenu();
    battleHUD.SetDialogue(monster.GetNextDialogue());
}
    private void OpenGameOver()
    {
    const string gameOverScene = "Game Over";

    if (!Application.CanStreamedLevelBeLoaded(gameOverScene))
    {
        Debug.LogError(
            "Add the Game Over scene to the build scene list.",
            this
        );
        return;
    }

    GameOverController.RetrySceneName =
        SceneManager.GetActiveScene().name;

    // Clears active attacks and closes the battle HUD.
    EndBattle();

    Time.timeScale = 1f;
    SceneManager.LoadScene(gameOverScene);
    }
}