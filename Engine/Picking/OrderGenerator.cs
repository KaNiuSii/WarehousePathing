using System;
using System.Collections.Generic;
using System.Linq;
// Alias - patrz komentarz w DistanceMatrix.cs.
using WarehouseMap = Engine.WarehouseGrid.WarehouseGrid;

namespace Engine.Picking;

/// <summary>Losuje listę zamówienia z produktów, które faktycznie leżą w magazynie (ilość nie przekracza stanu).</summary>
public static class OrderGenerator
{
    public static IReadOnlyList<OrderLine> Generate(
        WarehouseMap warehouse, int lineCount, int maxQuantityPerLine, int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(warehouse);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxQuantityPerLine);

        var stock = CountStockByProduct(warehouse);
        var rng = new Random(seed ?? Random.Shared.Next());

        return stock.Keys
            .OrderBy(_ => rng.Next())
            .Take(lineCount)
            .Select(product => new OrderLine(product, rng.Next(1, Math.Min(maxQuantityPerLine, stock[product]) + 1)))
            .ToList();
    }

    private static Dictionary<string, int> CountStockByProduct(WarehouseMap warehouse)
    {
        var stock = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var shelfCell in warehouse.Shelves())
        {
            foreach (var item in shelfCell.Shelf!.Items)
            {
                if (item.Quantity <= 0) continue;
                stock[item.Name] = stock.GetValueOrDefault(item.Name) + item.Quantity;
            }
        }

        return stock;
    }
}