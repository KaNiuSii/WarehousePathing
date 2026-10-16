using Microsoft.VisualBasic;

namespace Engine.WarehouseGrid;

public class Cell
{
    public int X { get; }
    public int Y { get; }
    public CellType Type { get; }

    /// <summary>Półka znajdująca się w tej komórce (null dla ścieżki).</summary>
    public Shelf? Shelf { get; }

    public bool IsWalkable => Type == CellType.Path;

    public Cell(int x, int y, CellType type, Shelf? shelf = null)
    {
        X = x;
        Y = y;
        Type = type;
        Shelf = shelf;
    }
}