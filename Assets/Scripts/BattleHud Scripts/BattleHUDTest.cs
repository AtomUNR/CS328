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
            "ADAM", 1,
            20, 20,     // Current HP, maximum HP
            10, 10      // Current MP, maximum MP
        );

        battleHUD.BeginBattle(
            enemySprite,
            "Tower Guardian",
            "* A tower guardian blocks your path."
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