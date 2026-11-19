using UnityEngine;

public enum ItemEffect
{
    RestoreHP,
    RestoreMP,
    RestoreHPAndDodgeSpeed
}

[CreateAssetMenu(fileName = "New Item", menuName = "Battle Tower/Item")]
public class ItemData : ScriptableObject
{
    [SerializeField] private string itemName = "New Item";
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private ItemEffect effect;
    [SerializeField, Min(1)] private int restoreAmount = 20;

    [SerializeField, Range(1f, 3f)] private float dodgeSpeedMultiplier = 1.5f;

    public float DodgeSpeedMultiplier => Mathf.Clamp(dodgeSpeedMultiplier, 1f, 3f);

    public string ItemName => itemName;
    public string Description => description;
    public Sprite Icon => icon;
    public ItemEffect Effect => effect;
    public int RestoreAmount => restoreAmount;
}