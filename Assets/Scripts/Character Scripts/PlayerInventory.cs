using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public const int MaxSlots = 10;

    [SerializeField] private List<ItemData> items = new List<ItemData>();

    public IReadOnlyList<ItemData> Items => items;
    public int ItemCount => items.Count;

    public bool TryAddItem(ItemData item)
    {
        if (item == null || items.Count >= MaxSlots)
            return false;

        items.Add(item);
        return true;
    }

    public bool RemoveItem(ItemData item)
    {
        if (item == null)
            return false;

        return items.Remove(item);
    }
}