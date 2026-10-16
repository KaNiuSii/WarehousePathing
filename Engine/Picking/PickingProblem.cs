using System;
using System.Collections.Generic;
using System.Linq;
using Engine.WarehouseGrid;
// Alias - patrz komentarz w DistanceMatrix.cs.
using WarehouseMap = Engine.WarehouseGrid.WarehouseGrid;

namespace Engine.Picking;

/// <summary>
/// Porcja zapotrzebowania na produkt (nie większa niż pojemność wózka).
/// Duże pozycje zamówienia są dzielone na porcje, żeby dało się je rozłożyć na kilka kursów.
/// </summary>
internal readonly record struct Chunk(int Product, int Amount);

/// <summary>
/// Zadanie przygotowane do optymalizacji: porcje do zebrania, półki-źródła każdego produktu
/// (posortowane od najbliższej targetu) i macierz odległości.
///
/// Przystanki: indeks 0 = target, kolejne = półki zawierające potrzebne produkty.
/// Źródła są trzymane w płaskich tablicach: produkt p ma źródła w przedziale
/// [SourceOffsets[p], SourceOffsets[p + 1]).
/// </summary>
internal sealed class PickingProblem
{
    public const int TargetStop = 0;

    public int CartCapacity { get; }
    public string[] Products { get; }
    public Chunk[] Chunks { get; }
    public int[] SourceOffsets { get; }
    public int[] SourceStops { get; }
    public int[] SourceStock { get; }
    public Cell[] StopCells { get; }
    public DistanceMatrix Distances { get; }

    private PickingProblem(
        int cartCapacity, string[] products, Chunk[] chunks,
        int[] sourceOffsets, int[] sourceStops, int[] sourceStock,
        Cell[] stopCells, DistanceMatrix distances)
    {
        CartCapacity = cartCapacity;
        Products = products;
        Chunks = chunks;
        SourceOffsets = sourceOffsets;
        SourceStops = sourceStops;
        SourceStock = sourceStock;
        StopCells = stopCells;
        Distances = distances;
    }

    public static PickingProblem Create(WarehouseMap warehouse, PickingRequest request)
    {
        Validate(warehouse, request);

        var (products, quantities) = AggregateDemand(request.Lines);
        var candidates = FindCandidateShelves(warehouse, products);

        var stopCells = BuildStops(warehouse, request.Target, candidates, out var stopIndex);
        var distances = DistanceMatrix.Build(warehouse, stopCells);

        var sources = BuildSources(products, quantities, candidates, stopIndex, distances);
        var chunks = SplitIntoChunks(quantities, request.CartCapacity);

        return new PickingProblem(
            request.CartCapacity, products.ToArray(), chunks,
            sources.Offsets, sources.Stops, sources.Stock,
            stopCells.ToArray(), distances);
    }

    private static void Validate(WarehouseMap warehouse, PickingRequest request)
    {
        ArgumentNullException.ThrowIfNull(warehouse);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Lines is null || request.Lines.Count == 0)
            throw new ArgumentException("Lista kompletacji jest pusta.", nameof(request));

        if (request.CartCapacity <= 0)
            throw new ArgumentException("Pojemność wózka musi być dodatnia.", nameof(request));

        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.Product) || line.Quantity <= 0)
                throw new ArgumentException(
                    $"Niepoprawna pozycja zamówienia: '{line.Product}' x{line.Quantity}.", nameof(request));
        }

        if (!warehouse.InBounds(request.Target) || !warehouse[request.Target].IsWalkable)
            throw new ArgumentException("Target musi być ścieżką wewnątrz magazynu.", nameof(request));
    }

    /// <summary>Łączy powtórzone produkty (nazwy bez rozróżniania wielkości liter, jak Shelf.Find).</summary>
    private static (List<string> Products, List<int> Quantities) AggregateDemand(IReadOnlyList<OrderLine> lines)
    {
        var products = new List<string>();
        var quantities = new List<int>();
        var indexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            if (indexByName.TryGetValue(line.Product, out int index))
            {
                quantities[index] += line.Quantity;
                continue;
            }

            indexByName[line.Product] = products.Count;
            products.Add(line.Product);
            quantities.Add(line.Quantity);
        }

        return (products, quantities);
    }

    /// <summary>Dla każdego produktu: półki, na których leży, wraz ze stanem (suma po itemach tej półki).</summary>
    private static List<(Cell Shelf, int Stock)>[] FindCandidateShelves(WarehouseMap warehouse, List<string> products)
    {
        var indexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(Cell Shelf, int Stock)>[products.Count];
        for (int i = 0; i < products.Count; i++)
        {
            indexByName[products[i]] = i;
            result[i] = new List<(Cell, int)>();
        }

        foreach (var shelfCell in warehouse.Shelves())
        {
            foreach (var item in shelfCell.Shelf!.Items)
            {
                if (item.Quantity <= 0 || !indexByName.TryGetValue(item.Name, out int product)) continue;

                var list = result[product];
                if (list.Count > 0 && list[^1].Shelf == shelfCell)
                    list[^1] = (shelfCell, list[^1].Stock + item.Quantity);
                else
                    list.Add((shelfCell, item.Quantity));
            }
        }

        return result;
    }

    private static List<Cell> BuildStops(
        WarehouseMap warehouse, (int X, int Y) target,
        List<(Cell Shelf, int Stock)>[] candidates, out Dictionary<Cell, int> stopIndex)
    {
        var stops = new List<Cell> { warehouse[target] };
        stopIndex = new Dictionary<Cell, int>();

        foreach (var list in candidates)
        {
            foreach (var (shelf, _) in list)
            {
                if (stopIndex.ContainsKey(shelf)) continue;
                stopIndex[shelf] = stops.Count;
                stops.Add(shelf);
            }
        }

        return stops;
    }

    private static (int[] Offsets, int[] Stops, int[] Stock) BuildSources(
        List<string> products, List<int> quantities,
        List<(Cell Shelf, int Stock)>[] candidates,
        Dictionary<Cell, int> stopIndex, DistanceMatrix distances)
    {
        var offsets = new int[products.Count + 1];
        var stops = new List<int>();
        var stock = new List<int>();
        var shortages = new List<string>();

        for (int p = 0; p < products.Count; p++)
        {
            offsets[p] = stops.Count;

            var reachable = candidates[p]
                .Where(c => distances[TargetStop, stopIndex[c.Shelf]] < DistanceMatrix.Unreachable)
                .OrderBy(c => distances[TargetStop, stopIndex[c.Shelf]])
                .ToList();

            int available = reachable.Sum(c => c.Stock);
            if (available < quantities[p])
                shortages.Add($"{products[p]} (potrzeba {quantities[p]}, dostępne {available})");

            foreach (var (shelf, shelfStock) in reachable)
            {
                stops.Add(stopIndex[shelf]);
                stock.Add(shelfStock);
            }
        }

        offsets[products.Count] = stops.Count;

        if (shortages.Count > 0)
            throw new InvalidOperationException("Za mały stan magazynowy: " + string.Join("; ", shortages));

        return (offsets, stops.ToArray(), stock.ToArray());
    }

    private static Chunk[] SplitIntoChunks(List<int> quantities, int cartCapacity)
    {
        var chunks = new List<Chunk>();
        for (int p = 0; p < quantities.Count; p++)
        {
            for (int left = quantities[p]; left > 0;)
            {
                int amount = Math.Min(left, cartCapacity);
                chunks.Add(new Chunk(p, amount));
                left -= amount;
            }
        }

        return chunks.ToArray();
    }
}