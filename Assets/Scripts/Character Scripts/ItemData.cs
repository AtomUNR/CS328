using UnityEngine;

public enum ItemEffect
{
    RestoreHP,
    RestoreMP
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

    public string ItemName => itemName;
    public string Description => description;
    public Sprite Icon => icon;
    public ItemEffect Effect => effect;
    public int RestoreAmount => restoreAmount;
}