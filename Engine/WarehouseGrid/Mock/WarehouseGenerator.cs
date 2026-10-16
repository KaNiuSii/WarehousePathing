using System;

namespace Engine.WarehouseGrid;

/// <summary>
/// Generuje losowe układy magazynów (półki i ścieżki). Nie wypełnia półek itemami.
///
/// Gwarancje (z konstrukcji, nie z walidacji po fakcie):
///  - obwód siatki jest zawsze ścieżką,
///  - regały mają grubość 1-2 komórek, więc każda półka sąsiaduje ze ścieżką,
///  - późniejsze modyfikacje (strefy otwarte, luki) tylko zamieniają półki na ścieżki,
///    więc nigdy nie odcinają żadnej półki od korytarza.
/// Ten sam seed daje ten sam magazyn.
/// </summary>
public class WarehouseGenerator
{
    public const int MinSize = 8;

    private readonly Random _rng;

    public int Seed { get; }

    public WarehouseGenerator(int? seed = null)
    {
        Seed = seed ?? Random.Shared.Next();
        _rng = new Random(Seed);
    }

    public WarehouseLayoutType PickLayoutType()
    {
        var values = Enum.GetValues<WarehouseLayoutType>();
        return values[_rng.Next(values.Length)];
    }

    /// <summary>Generuje magazyn o podanym rozmiarze. Brak layoutType = losowy typ.</summary>
    public WarehouseGrid Generate(
        int width,
        int height,
        WarehouseLayoutType? layoutType = null,
        int shelfCapacity = WarehouseGrid.DefaultShelfCapacity)
    {
        if (width < MinSize) throw new ArgumentOutOfRangeException(nameof(width), $"Minimum to {MinSize}.");
        if (height < MinSize) throw new ArgumentOutOfRangeException(nameof(height), $"Minimum to {MinSize}.");

        var type = layoutType ?? PickLayoutType();

        // Generatory budują układ w jednej orientacji; losowy obrót i odbicia dają resztę wariantów.
        bool transpose = _rng.Next(2) == 0;
        var mask = transpose
            ? Transpose(Build(type, height, width))
            : Build(type, width, height);

        if (_rng.Next(2) == 0) FlipX(mask);
        if (_rng.Next(2) == 0) FlipY(mask);

        return new WarehouseGrid(mask, shelfCapacity);
    }

    private bool[,] Build(WarehouseLayoutType type, int w, int h) => type switch
    {
        WarehouseLayoutType.ParallelAisles => ParallelAisles(w, h),
        WarehouseLayoutType.StaggeredBlocks => StaggeredBlocks(w, h),
        WarehouseLayoutType.CrossDock => CrossDock(w, h),
        WarehouseLayoutType.Irregular => Irregular(w, h),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    /// <summary>
    /// Pionowe alejki: bloki regałów o grubości 1-2 kolumn rozdzielone alejkami (czasem szerszymi),
    /// przecięte poziomymi korytarzami poprzecznymi co 5-10 wierszy.
    /// </summary>
    private bool[,] ParallelAisles(int w, int h)
    {
        var shelfColumn = new bool[w];
        int x = 1;
        while (x < w - 1)
        {
            int thick = Math.Min(_rng.Next(100) < 75 ? 2 : 1, w - 1 - x);
            for (int i = 0; i < thick; i++)
                shelfColumn[x + i] = true;

            x += thick;
            x += _rng.Next(100) < 15 ? 2 : 1; // szerokość alejki
        }

        var crossRow = new bool[h];
        crossRow[0] = true;
        crossRow[h - 1] = true;
        int y = 1;
        while (y < h - 1)
        {
            y += _rng.Next(5, 11); // długość sekcji regałów
            if (y < h - 1) crossRow[y] = true;
            y += _rng.Next(100) < 20 ? 2 : 1; // szerokość korytarza poprzecznego
        }

        var mask = new bool[w, h];
        for (int yy = 0; yy < h; yy++)
            for (int xx = 0; xx < w; xx++)
                mask[xx, yy] = shelfColumn[xx] && !crossRow[yy];

        return mask;
    }

    /// <summary>
    /// Poziome pasma regałów (grubość 1-2) podzielone na sekcje po 3-10 komórek,
    /// z przejściami w losowych miejscach, więc w każdym paśmie są przesunięte względem siebie.
    /// </summary>
    private bool[,] StaggeredBlocks(int w, int h)
    {
        var mask = new bool[w, h];
        int y = 1;
        while (y < h - 1)
        {
            int thick = Math.Min(_rng.Next(100) < 70 ? 2 : 1, h - 1 - y);

            int x = 1;
            while (x < w - 1)
            {
                int len = Math.Min(_rng.Next(3, 11), w - 1 - x);
                for (int i = 0; i < len; i++)
                    for (int t = 0; t < thick; t++)
                        mask[x + i, y + t] = true;

                x += len;
                x += _rng.Next(100) < 20 ? 2 : 1; // przejście między sekcjami
            }

            y += thick;
            y += _rng.Next(100) < 25 ? 2 : 1; // alejka między pasmami
        }

        return mask;
    }

    /// <summary>Alejki równoległe + otwarta strefa przyjęć/wydań o głębokości ok. 1/8-1/4 wysokości.</summary>
    private bool[,] CrossDock(int w, int h)
    {
        var mask = ParallelAisles(w, h);

        int depth = _rng.Next(Math.Max(2, h / 8), Math.Max(3, h / 4) + 1);
        for (int y = 0; y < depth; y++)
            for (int x = 0; x < w; x++)
                mask[x, y] = false;

        return mask;
    }

    /// <summary>Układ bazowy (alejki lub pasma) z prostokątnymi polanami i losowymi lukami w regałach.</summary>
    private bool[,] Irregular(int w, int h)
    {
        var mask = _rng.Next(2) == 0 ? ParallelAisles(w, h) : StaggeredBlocks(w, h);

        int clearings = _rng.Next(2, 3 + w * h / 500);
        for (int i = 0; i < clearings; i++)
        {
            int cw = _rng.Next(3, 13);
            int ch = _rng.Next(3, 11);
            int cx = _rng.Next(1, Math.Max(2, w - cw));
            int cy = _rng.Next(1, Math.Max(2, h - ch));

            for (int y = cy; y < Math.Min(h, cy + ch); y++)
                for (int x = cx; x < Math.Min(w, cx + cw); x++)
                    mask[x, y] = false;
        }

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (mask[x, y] && _rng.NextDouble() < 0.02)
                    mask[x, y] = false;

        return mask;
    }

    private static bool[,] Transpose(bool[,] src)
    {
        int w = src.GetLength(0), h = src.GetLength(1);
        var result = new bool[h, w];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                result[y, x] = src[x, y];
        return result;
    }

    private static void FlipX(bool[,] mask)
    {
        int w = mask.GetLength(0), h = mask.GetLength(1);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w / 2; x++)
                (mask[x, y], mask[w - 1 - x, y]) = (mask[w - 1 - x, y], mask[x, y]);
    }

    private static void FlipY(bool[,] mask)
    {
        int w = mask.GetLength(0), h = mask.GetLength(1);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h / 2; y++)
                (mask[x, y], mask[x, h - 1 - y]) = (mask[x, h - 1 - y], mask[x, y]);
    }
}