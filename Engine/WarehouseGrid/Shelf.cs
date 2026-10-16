using System;
using System.Collections.Generic;
using System.Linq;

namespace Engine.WarehouseGrid;

public class Shelf
{
    private readonly List<Item> _items = new();

    public int Capacity { get; }
    public IReadOnlyList<Item> Items => _items;
    public bool IsFull => _items.Count >= Capacity;
    public bool IsEmpty => _items.Count == 0;

    public Shelf(int capacity = 10)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
    }

    /// <summary>Dodaje item na półkę. Zwraca false, jeśli półka jest pełna.</summary>
    public bool TryAdd(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (IsFull) return false;
        _items.Add(item);
        return true;
    }

    public bool Remove(Item item) => _items.Remove(item);

    public Item? Find(string name) =>
        _items.FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public bool Contains(string name) => Find(name) is not null;
}