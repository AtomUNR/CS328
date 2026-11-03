using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    [Header("Health")]
    [SerializeField, Min(1)] private int maxHP = 100;

    [Header("Magic")]
    [SerializeField, Min(1)] private int MPmax = 100;

    public int MaxHP => maxHP;
    public int CurrentHP { get; private set; }
    
    public int MPCurrent => MPmax;
    public int CurrentMP { get; private set; }

    private void Awake()
    {
        maxHP = Mathf.Max(1, maxHP);
        MPmax = Mathf.Max(1, MPmax);

        CurrentHP = maxHP;
        CurrentMP = MPmax;
    }

    public void TakeDamage(int dmg_amount)
    {
        if(dmg_amount <= 0)
        {
            return;
        }
        CurrentHP = Mathf.Min(CurrentHP - dmg_amount, 0);
    }

    public void RestoreHP(int heal_amount)
    {
        if(heal_amount <= 0)
        {
            return;
        }
        CurrentHP = Mathf.Max(CurrentHP + heal_amount, CurrentHP);
    }
    public bool TrySpendMP(int amount)
    {
        if (amount <= 0 || CurrentMP < amount)
            return false;

        CurrentMP -= amount;
        return true;
    }

    public void RestoreMP(int amount)
    {
        if (amount <= 0)
            return;

        CurrentMP = Mathf.Min(CurrentMP + amount, MPmax);
    }
}
