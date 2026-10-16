using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Engine.WarehouseGrid;
// Sama nazwa "WarehouseGrid" jest przysłonięta przez namespace Engine.WarehouseGrid,
// dlatego w namespace Engine.* odwołujemy się do klasy przez alias.
using WarehouseMap = Engine.WarehouseGrid.WarehouseGrid;

namespace Engine.Picking;

/// <summary>
/// Odległości (liczba kroków po ścieżkach) między przystankami.
/// Przystanek to ścieżka (target) albo półka. Do półki podchodzi się z dowolnej sąsiadującej ścieżki,
/// więc odległość liczy się między polami dostępu (dwie półki o wspólnym polu dostępu dzieli 0 kroków).
/// </summary>
internal sealed class DistanceMatrix
{
    public const int Unreachable = int.MaxValue / 4;

    private static readonly (int dx, int dy)[] Directions = { (0, -1), (1, 0), (0, 1), (-1, 0) };

    private readonly int[,] _distances;

    private DistanceMatrix(int[,] distances) => _distances = distances;

    public int this[int from, int to] => _distances[from, to];

    public static DistanceMatrix Build(WarehouseMap warehouse, IReadOnlyList<Cell> stops)
    {
        int width = warehouse.Width;
        int height = warehouse.Height;

        var walkable = BuildWalkableMap(warehouse);
        var accessCells = new int[stops.Count][];
        for (int i = 0; i < stops.Count; i++)
            accessCells[i] = FindAccessCells(stops[i], walkable, width, height);

        var matrix = new int[stops.Count, stops.Count];

        Parallel.For(
            0, stops.Count,
            () => new BfsBuffers(width * height),
            (i, _, buffers) =>
            {
                RunBfs(walkable, width, height, accessCells[i], buffers);
                for (int j = 0; j < stops.Count; j++)
                    matrix[i, j] = MinDistance(buffers.Distance, accessCells[j]);
                return buffers;
            },
            _ => { });

        return new DistanceMatrix(matrix);
    }

    private static bool[] BuildWalkableMap(WarehouseMap warehouse)
    {
        var walkable = new bool[warehouse.Width * warehouse.Height];
        for (int y = 0; y < warehouse.Height; y++)
            for (int x = 0; x < warehouse.Width; x++)
                walkable[y * warehouse.Width + x] = warehouse[x, y].IsWalkable;
        return walkable;
    }

    private static int[] FindAccessCells(Cell stop, bool[] walkable, int width, int height)
    {
        if (stop.IsWalkable)
            return new[] { stop.Y * width + stop.X };

        var cells = new List<int>(4);
        foreach (var (dx, dy) in Directions)
        {
            int x = stop.X + dx, y = stop.Y + dy;
            if ((uint)x < (uint)width && (uint)y < (uint)height && walkable[y * width + x])
                cells.Add(y * width + x);
        }

        return cells.ToArray();
    }

    private static void RunBfs(bool[] walkable, int width, int height, int[] sources, BfsBuffers buffers)
    {
        var distance = buffers.Distance;
        var queue = buffers.Queue;
        Array.Fill(distance, -1);

        int head = 0, tail = 0;
        foreach (int source in sources)
        {
            distance[source] = 0;
            queue[tail++] = source;
        }

        while (head < tail)
        {
            int current = queue[head++];
            int x = current % width, y = current / width;

            foreach (var (dx, dy) in Directions)
            {
                int nx = x + dx, ny = y + dy;
                if ((uint)nx >= (uint)width || (uint)ny >= (uint)height) continue;

                int next = ny * width + nx;
                if (!walkable[next] || distance[next] >= 0) continue;

                distance[next] = distance[current] + 1;
                queue[tail++] = next;
            }
        }
    }

    private static int MinDistance(int[] distance, int[] accessCells)
    {
        int best = Unreachable;
        foreach (int cell in accessCells)
        {
            int d = distance[cell];
            if (d >= 0 && d < best) best = d;
        }

        return best;
    }

    private sealed class BfsBuffers
    {
        public int[] Distance { get; }
        public int[] Queue { get; }

        public BfsBuffers(int size)
        {
            Distance = new int[size];
            Queue = new int[size];
        }
    }
}