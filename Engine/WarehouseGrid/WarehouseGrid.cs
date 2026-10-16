using System;
using System.Collections.Generic;

namespace Engine.WarehouseGrid;

/// <summary>
/// Plan magazynu: siatka komórek, w której każda komórka jest albo ścieżką (Path),
/// albo półką (Shelf) z itemami.
///
/// Układ domyślny (konstruktor width/height):
///  - obwód siatki oraz jeden poprzeczny korytarz na środku to ścieżki,
///  - co trzecia kolumna (x % 3 == 0) to korytarz pionowy,
///  - pozostałe komórki to półki, po dwie kolumny między korytarzami.
///
/// Dowolny układ można zbudować z maski (true = półka) - korzysta z tego WarehouseGenerator.
/// Półki są puste; wypełnianie itemami należy do kodu wywołującego (np. WarehouseMock).
/// </summary>
public class WarehouseGrid
{
    public const int DefaultWidth = 19;
    public const int DefaultHeight = 12;
    public const int DefaultShelfCapacity = 10;

    private static readonly (int dx, int dy)[] Dirs4 = { (0, -1), (1, 0), (0, 1), (-1, 0) };

    private readonly Cell[,] _cells;

    public int Width { get; }
    public int Height { get; }

    /// <summary>Układ domyślny.</summary>
    public WarehouseGrid(int width = DefaultWidth, int height = DefaultHeight)
        : this(BuildDefaultMask(width, height), DefaultShelfCapacity)
    {
    }

    /// <summary>Układ z maski: isShelf[x, y] == true oznacza półkę, false - ścieżkę.</summary>
    public WarehouseGrid(bool[,] isShelf, int shelfCapacity = DefaultShelfCapacity)
    {
        ArgumentNullException.ThrowIfNull(isShelf);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shelfCapacity);

        Width = isShelf.GetLength(0);
        Height = isShelf.GetLength(1);
        if (Width < 3) throw new ArgumentException("Szerokość maski musi wynosić co najmniej 3.", nameof(isShelf));
        if (Height < 3) throw new ArgumentException("Wysokość maski musi wynosić co najmniej 3.", nameof(isShelf));

        _cells = new Cell[Width, Height];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                _cells[x, y] = isShelf[x, y]
                    ? new Cell(x, y, CellType.Shelf, new Shelf(shelfCapacity))
                    : new Cell(x, y, CellType.Path);
            }
        }
    }

    public Cell this[int x, int y]
    {
        get
        {
            if (!InBounds(x, y))
                throw new ArgumentOutOfRangeException($"({x}, {y})", $"Pozycja poza siatką {Width}x{Height}.");
            return _cells[x, y];
        }
    }

    public Cell this[(int x, int y) pos] => this[pos.x, pos.y];

    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public bool InBounds((int x, int y) pos) => InBounds(pos.x, pos.y);

    public IEnumerable<Cell> Cells()
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                yield return _cells[x, y];
    }

    public IEnumerable<Cell> Shelves()
    {
        foreach (var cell in Cells())
            if (cell.Type == CellType.Shelf)
                yield return cell;
    }

    /// <summary>Wszystkie półki zawierające item o podanej nazwie.</summary>
    public IEnumerable<Cell> FindShelvesWith(string itemName)
    {
        foreach (var cell in Shelves())
            if (cell.Shelf!.Contains(itemName))
                yield return cell;
    }

    public IEnumerable<(int x, int y)> Neighbors4(int x, int y)
    {
        foreach (var (dx, dy) in Dirs4)
        {
            int nx = x + dx, ny = y + dy;
            if (InBounds(nx, ny))
                yield return (nx, ny);
        }
    }

    /// <summary>
    /// Najkrótsza trasa (BFS) po ścieżkach od start do goal, włącznie z obiema komórkami.
    /// Cel może być półką (np. do pobrania itemu) - wtedy trasa kończy się na niej.
    /// Zwraca pustą listę, jeśli trasa nie istnieje.
    /// </summary>
    public IReadOnlyList<(int x, int y)> FindPath((int x, int y) start, (int x, int y) goal)
    {
        if (!InBounds(start)) throw new ArgumentOutOfRangeException(nameof(start));
        if (!InBounds(goal)) throw new ArgumentOutOfRangeException(nameof(goal));
        if (!this[start].IsWalkable) return Array.Empty<(int, int)>();

        var visited = new bool[Width, Height];
        var previous = new (int x, int y)?[Width, Height];
        var queue = new Queue<(int x, int y)>();

        visited[start.x, start.y] = true;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current == goal)
                return Reconstruct(previous, start, goal);

            foreach (var next in Neighbors4(current.x, current.y))
            {
                if (visited[next.x, next.y]) continue;
                if (next != goal && !_cells[next.x, next.y].IsWalkable) continue;

                visited[next.x, next.y] = true;
                previous[next.x, next.y] = current;
                queue.Enqueue(next);
            }
        }

        return Array.Empty<(int, int)>();
    }

    private static List<(int x, int y)> Reconstruct(
        (int x, int y)?[,] previous, (int x, int y) start, (int x, int y) goal)
    {
        var path = new List<(int x, int y)>();
        var current = goal;

        while (current != start)
        {
            path.Add(current);
            current = previous[current.x, current.y]!.Value;
        }

        path.Add(start);
        path.Reverse();
        return path;
    }

    private static bool[,] BuildDefaultMask(int width, int height)
    {
        if (width < 3) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 3) throw new ArgumentOutOfRangeException(nameof(height));

        var mask = new bool[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isPath =
                    x == 0 || x == width - 1 ||
                    y == 0 || y == height - 1 ||
                    y == height / 2 ||
                    x % 3 == 0;

                mask[x, y] = !isPath;
            }
        }

        return mask;
    }
}