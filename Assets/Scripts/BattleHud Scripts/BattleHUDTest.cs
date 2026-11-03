using UnityEngine;

public class BattleHUDTest : MonoBehaviour
{
    [SerializeField] private BattleHudController battleHUD;
    [SerializeField] private Sprite enemySprite;

    private void Start()
    {
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