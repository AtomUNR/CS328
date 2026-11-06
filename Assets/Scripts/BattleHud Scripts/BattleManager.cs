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
        PlayerDefeated
    }

    private BattleState state = BattleState.Inactive;

    private Player player;
    private Monster monster;
    private PlayerMovement playerMovement;

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
                battleHUD.SetDialogue(
                    "* The item menu is not connected yet."
                );
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

        if (!dodgePhase.BeginPhase(attack, monster.BattleSprite, player.Stats)){ return; } 

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
        battleHUD.ShowDialogueOnly();
        battleHUD.SetDialogue(message);
    }

    public void EndBattle()
    {
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