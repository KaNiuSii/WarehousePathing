using System;

namespace Engine.WarehouseGrid;

public class Item
{
    public int Id { get; }
    public string Name { get; }
    public int Quantity { get; set; }

    public Item(int id, string name, int quantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);

        Id = id;
        Name = name;
        Quantity = quantity;
    }

    public override string ToString() => $"{Name} x{Quantity} (#{Id})";
}