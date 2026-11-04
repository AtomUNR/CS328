using UnityEngine;

[RequireComponent(typeof(CharacterStats))]
public class Monster : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string monsterName = "Tower Guardian";
    [SerializeField] private Sprite battleSprite;

    [Header("Combat")]
    [SerializeField, Min(0)] private int attackDamage = 5;

    [Header("Victory Rewards")]
    [SerializeField, Min(0)] private int xpReward = 25;
    [SerializeField, Min(0)] private int moneyReward = 10;

    [Header("Low Health")]
    [Tooltip("0.25 means 25% of maximum HP.")]
    [SerializeField, Range(0f, 1f)]
    private float lowHealthThreshold = 0.25f;

    [Header("Normal Dialogue")]
    [SerializeField, TextArea(2, 4)]
    private string[] normalDialogue =
    {
        "* A tower guardian blocks your path.",
        "* The guardian tightens its grip on its spear.",
        "* The guardian watches your every move."
    };

    [Header("Low Health Dialogue")]
    [SerializeField, TextArea(2, 4)]
    private string[] lowHealthDialogue =
    {
        "* The guardian staggers.",
        "* Its shield trembles, but it stands firm.",
        "* The guardian refuses to surrender."
    };

    [Header("Defeated Dialogue")]
    [SerializeField, TextArea(2, 4)]
    private string[] defeatedDialogue =
    {
        "* The guardian falls to its knees.",
        "* Its spear clatters against the floor."
    };

    public string DisplayName => monsterName;
    public Sprite BattleSprite => battleSprite;
    public int AttackDamage => attackDamage;
    public int XPReward => xpReward;
    public int MoneyReward => moneyReward;

    public CharacterStats Stats { get; private set; }

    public bool IsDefeated =>
        Stats != null && Stats.CurrentHP <= 0;

    public bool IsLowHealth =>
        Stats != null &&
        !IsDefeated &&
        Stats.CurrentHP <= Stats.MaxHP * lowHealthThreshold;

    private int normalDialogueIndex;
    private int lowHealthDialogueIndex;
    private int defeatedDialogueIndex;

    private void Awake()
    {
        Stats = GetComponent<CharacterStats>();

        attackDamage = Mathf.Max(0, attackDamage);
        xpReward = Mathf.Max(0, xpReward);
        moneyReward = Mathf.Max(0, moneyReward);

        ResetDialogue();
    }

    public void ResetDialogue()
    {
        normalDialogueIndex = 0;
        lowHealthDialogueIndex = 0;
        defeatedDialogueIndex = 0;
    }

    public string GetNextDialogue()
    {
        // Defeat takes priority over low health.
        if (IsDefeated)
        {
            return GetNextLine(
                defeatedDialogue,
                ref defeatedDialogueIndex,
                $"* {monsterName} was defeated."
            );
        }

        if (IsLowHealth)
        {
            return GetNextLine(
                lowHealthDialogue,
                ref lowHealthDialogueIndex,
                $"* {monsterName} is badly wounded."
            );
        }

        return GetNextLine(
            normalDialogue,
            ref normalDialogueIndex,
            $"* {monsterName} watches you."
        );
    }

    private static string GetNextLine(
        string[] lines,
        ref int nextIndex,
        string fallback)
    {
        if (lines == null || lines.Length == 0)
            return fallback;

        nextIndex = Mathf.Clamp(nextIndex, 0, lines.Length - 1);

        string line = lines[nextIndex];
        nextIndex++;

        if (nextIndex >= lines.Length)
        {
            // Skip the opening line when repeating a multi-line list.
            nextIndex = lines.Length > 1 ? 1 : 0;
        }

        return string.IsNullOrWhiteSpace(line) ? fallback : line;
    }
    public string[] GetDefeatDialogueLines()
    {
    if (defeatedDialogue == null || defeatedDialogue.Length == 0)
    {
        return new[]
        {
            $"* {DisplayName} was defeated."
        };
    }

    return (string[])defeatedDialogue.Clone();
    }
}