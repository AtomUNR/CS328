using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public const int Capacity = 10;

    [Header("Starting Items — maximum 10")]
    [SerializeField] private List<ItemData> startingItems =
        new List<ItemData>();

    private readonly List<ItemData> items = new List<ItemData>();

    public int Count => items.Count;
    public bool IsFull => items.Count >= Capacity;
    public IReadOnlyList<ItemData> Items => items.AsReadOnly();

    private void Awake()
    {
        items.Clear();

        foreach (ItemData item in startingItems)
        {
            // Empty Inspector entries do not occupy inventory slots.
            if (item == null)
                continue;

            if (!TryAddItem(item))
            {
                Debug.LogWarning(
                    "Starting inventory exceeds 10 items. " +
                    "Extra items were skipped.",
                    this
                );

                break;
            }
        }
    }

    public bool TryAddItem(ItemData item)
    {
        if (item == null || IsFull)
            return false;

        items.Add(item);
        return true;
    }

    public bool TryRemoveItem(int index)
    {
        if (index < 0 || index >= items.Count)
            return false;

        items.RemoveAt(index);
        return true;
    }
}