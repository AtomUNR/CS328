using UnityEngine;

public class BattleHUDTest : MonoBehaviour
{
    [SerializeField] private BattleHudController battleHUD;
    [SerializeField] private Sprite enemySprite;
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private Player player;
    [SerializeField] private Monster monster;

    private void Start()
    {
        if (battleManager != null && player != null && monster != null)
        {
            battleManager.StartBattle(player, monster);
            return;
        }

        if (battleHUD == null)
        {
            Debug.LogError("Assign the BattleHUD reference.", this);
            return;
        }

        battleHUD.CommandChosen += OnCommandChosen;

        battleHUD.SetPlayerStatus(
            "ANDREW", 1,
            10, 50,     // Current HP, maximum HP
            20, 50      // Current MP, maximum MP
        );

        battleHUD.BeginBattle(
            enemySprite,
            "Tower Guardian",
            " *YOU SHALL NOT PASS."
        );
    }

    private void OnCommandChosen(BattleCommand command)
    {
        Debug.Log("Selected command: " + command);
    }

    private void OnDestroy()
    {
        if (battleHUD != null)
            battleHUD.CommandChosen -= OnCommandChosen;
    }
}
