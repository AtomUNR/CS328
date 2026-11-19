using UnityEngine;

[RequireComponent(typeof(CharacterStats), typeof(PlayerInventory))]
public class Player : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string playerName = "DEFAULT";

    [Header("Starting Progress")]
    [SerializeField, Min(1)] private int startingLevel = 1;
    [SerializeField, Min(0)] private int startingXP = 0;

    [Header("Money")]
    [SerializeField, Min(0)] private int startingMoney = 0;

    [Header("Leveling")]
    [Tooltip("XP needed for the next level = current level × this value.")]
    [SerializeField, Min(1)] private int xpPerLevel = 100;

    [Header("Combat")]
    [SerializeField, Min(1)] private int attackDamage = 5;

    public string DisplayName => playerName;
    public int Level { get; private set; }
    public int CurrentXP { get; private set; }
    public int XPToNextLevel => Level * xpPerLevel;
    public int Money { get; private set; }

    public int AttackDamage => attackDamage;

    public CharacterStats Stats { get; private set; }

    public PlayerInventory Inventory { get; private set; }
    private void Awake()
    {
        Stats = GetComponent<CharacterStats>();
        Inventory = GetComponent<PlayerInventory>();

        xpPerLevel = Mathf.Max(1, xpPerLevel);
        Level = Mathf.Max(1, startingLevel);
        CurrentXP = 0;
        Money = Mathf.Max(0, startingMoney);

        GainXP(startingXP);
    }

    public void GainXP(int amount)
    {
        if (amount <= 0)
            return;

        CurrentXP += amount;

        while (CurrentXP >= XPToNextLevel)
        {
            CurrentXP -= XPToNextLevel;
            Level++;
        }
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0)
            return;

        // Prevent exceeding the largest value an int can hold.
        Money += Mathf.Min(amount, int.MaxValue - Money);
    }

    public bool TrySpendMoney(int amount)
    {
        if (amount < 0 || Money < amount)
            return false;

        Money -= amount;
        return true;
    }
}