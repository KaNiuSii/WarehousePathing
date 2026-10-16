using System;
using System.Collections.Generic;

namespace Engine.WarehouseGrid;

/// <summary>
/// Mockuje duży magazyn: losowy układ (WarehouseGenerator) + losowe produkty.
///
/// Nazwa produktu to losowy hash (12 znaków hex). Jeden produkt leży na kilku półkach:
///  - skupisko: półki połączone ze sobą (obok siebie),
///  - rozproszenie: półki w różnych miejscach magazynu,
///  - mieszany: skupisko + reszta rozproszona.
/// Wszystkie losowania zależą od Seed, więc wynik jest powtarzalny.
/// </summary>
public class WarehouseMock
{
    private readonly WarehouseMockOptions _options;
    private readonly List<string> _productNames = new();

    public int Seed { get; }

    /// <summary>Typ układu użyty w ostatnim wywołaniu Build().</summary>
    public WarehouseLayoutType LayoutType { get; private set; }

    /// <summary>Nazwy (hashe) produktów wygenerowanych w ostatnim wywołaniu Build().</summary>
    public IReadOnlyList<string> ProductNames => _productNames;

    public WarehouseMock(WarehouseMockOptions? options = null)
    {
        _options = options ?? new WarehouseMockOptions();

        if (_options.FillRatio <= 0 || _options.FillRatio > 1)
            throw new ArgumentOutOfRangeException(nameof(options), "FillRatio musi mieścić się w przedziale (0, 1].");

        Seed = _options.Seed ?? Random.Shared.Next();
    }

    public WarehouseGrid Build()
    {
        var rng = new Random(Seed);
        var generator = new WarehouseGenerator(rng.Next());

        LayoutType = _options.LayoutType ?? generator.PickLayoutType();
        var grid = generator.Generate(_options.Width, _options.Height, LayoutType, _options.ShelfCapacity);

        Populate(grid, rng);
        return grid;
    }

    private void Populate(WarehouseGrid grid, Random rng)
    {
        _productNames.Clear();

        var available = new List<Cell>(grid.Shelves());
        if (available.Count == 0) return;

        long targetSlots = (long)(available.Count * _options.ShelfCapacity * _options.FillRatio);
        long filled = 0;
        int nextItemId = 1;
        int emptyRounds = 0;
        var usedNames = new HashSet<string>();

        while (filled < targetSlots && emptyRounds < 1000)
        {
            string name = NextHash(rng, usedNames);
            var targets = PickShelves(grid, available, rng, PickLocationCount(rng));

            if (targets.Count == 0)
            {
                emptyRounds++;
                continue;
            }

            foreach (var cell in targets)
            {
                if (cell.Shelf!.TryAdd(new Item(nextItemId++, name, rng.Next(1, 201))))
                    filled++;
            }

            _productNames.Add(name);
        }
    }

    /// <summary>Na ilu półkach leży produkt: zwykle 2-5, czasem 1, często 6-10, rzadko do 20.</summary>
    private static int PickLocationCount(Random rng)
    {
        int roll = rng.Next(100);
        if (roll < 15) return 1;
        if (roll < 70) return rng.Next(2, 6);
        if (roll < 95) return rng.Next(6, 11);
        return rng.Next(11, 21);
    }

    private static List<Cell> PickShelves(WarehouseGrid grid, List<Cell> available, Random rng, int count)
    {
        var result = new List<Cell>(count);
        var chosen = new HashSet<Cell>();
        int roll = rng.Next(100);

        if (roll < 45)
        {
            GatherCluster(grid, available, rng, count, chosen, result);
        }
        else if (roll < 75)
        {
            GatherScattered(available, rng, count, chosen, result);
        }
        else
        {
            GatherCluster(grid, available, rng, Math.Max(2, count / 2), chosen, result);
            GatherScattered(available, rng, count, chosen, result);
        }

        return result;
    }

    /// <summary>Zbiera do `limit` połączonych półek (BFS po sąsiadujących półkach) od losowego startu.</summary>
    private static void GatherCluster(
        WarehouseGrid grid, List<Cell> available, Random rng, int limit,
        HashSet<Cell> chosen, List<Cell> result)
    {
        var start = TakeRandomAvailable(available, rng, chosen);
        if (start is null) return;

        var queue = new Queue<Cell>();
        var seen = new HashSet<Cell> { start };
        queue.Enqueue(start);

        while (queue.Count > 0 && result.Count < limit)
        {
            var cell = queue.Dequeue();

            if (!cell.Shelf!.IsFull && chosen.Add(cell))
                result.Add(cell);

            foreach (var (nx, ny) in grid.Neighbors4(cell.X, cell.Y))
            {
                var neighbor = grid[nx, ny];
                if (neighbor.Type == CellType.Shelf && seen.Add(neighbor))
                    queue.Enqueue(neighbor);
            }
        }
    }

    /// <summary>Dobiera losowe półki z całego magazynu, aż result będzie miał `total` elementów.</summary>
    private static void GatherScattered(
        List<Cell> available, Random rng, int total,
        HashSet<Cell> chosen, List<Cell> result)
    {
        while (result.Count < total)
        {
            var cell = TakeRandomAvailable(available, rng, chosen);
            if (cell is null) return;

            chosen.Add(cell);
            result.Add(cell);
        }
    }

    /// <summary>Losuje półkę, która nie jest pełna ani już wybrana. Pełne półki usuwa z puli.</summary>
    private static Cell? TakeRandomAvailable(List<Cell> available, Random rng, HashSet<Cell> exclude)
    {
        for (int attempt = 0; attempt < 50 && available.Count > 0; attempt++)
        {
            int i = rng.Next(available.Count);
            var cell = available[i];

            if (cell.Shelf!.IsFull)
            {
                available[i] = available[^1];
                available.RemoveAt(available.Count - 1);
                continue;
            }

            if (exclude.Contains(cell)) continue;
            return cell;
        }

        return null;
    }

    private static string NextHash(Random rng, HashSet<string> used)
    {
        Span<byte> buffer = stackalloc byte[6];
        string hash;
        do
        {
            rng.NextBytes(buffer);
            hash = Convert.ToHexString(buffer).ToLowerInvariant();
        }
        while (!used.Add(hash));

        return hash;
    }
}