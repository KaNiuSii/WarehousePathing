using System.Diagnostics.CodeAnalysis;
using Engine.Picking;
using Engine.WarehouseGrid;
using Cell = Engine.WarehouseGrid.Cell; // Microsoft.Maui.Controls też ma klasę Cell

namespace Visualizer;

/// <summary>
/// Rysuje magazyn dopasowany do rozmiaru widoku:
///  - ścieżki jasne, półki brązowe (im bardziej zapełniona, tym ciemniejsza),
///  - półki z planu kompletacji w kolorze swojego kursu,
///  - po wybraniu kursu: kolorowe tylko jego półki i czarna linia trasy,
///  - półki wybranego produktu pomarańczowe, target zielony, zaznaczona komórka z niebieską ramką.
/// </summary>
public class WarehouseDrawable : IDrawable
{
    private static readonly Color PathColor = Color.FromArgb("#F4F4F4");
    private static readonly Color ShelfColor = Color.FromArgb("#8D6E63");
    private static readonly Color HighlightColor = Color.FromArgb("#FF9800");
    private static readonly Color SelectionColor = Color.FromArgb("#1E88E5");
    private static readonly Color TargetColor = Color.FromArgb("#2E7D32");

    private HashSet<Cell> _highlighted = new();
    private PickingPlan? _plan;
    private Dictionary<Cell, int> _tripByShelf = new();
    private int? _selectedTrip;
    private HashSet<Cell>? _selectedTripShelves;
    private IReadOnlyList<(int X, int Y)>? _selectedRoute;

    private float _cellSize;
    private float _offsetX;
    private float _offsetY;

    public WarehouseGrid? Warehouse { get; set; }
    public Cell? SelectedCell { get; set; }
    public (int X, int Y)? Target { get; set; }
    public int HighlightedShelfCount => _highlighted.Count;

    public PickingPlan? Plan
    {
        get => _plan;
        set
        {
            _plan = value;
            RebuildTripIndex();
            ShowTrip(null, null);
        }
    }

    /// <summary>Podświetla wszystkie półki z danym produktem (null = bez podświetlenia).</summary>
    public void Highlight(string? productName)
    {
        _highlighted = productName is null || Warehouse is null
            ? new HashSet<Cell>()
            : Warehouse.FindShelvesWith(productName).ToHashSet();
    }

    /// <summary>Wyróżnia jeden kurs planu wraz z jego trasą (null = pokaż wszystkie kursy).</summary>
    public void ShowTrip(int? tripIndex, IReadOnlyList<(int X, int Y)>? route)
    {
        _selectedTrip = tripIndex;
        _selectedRoute = route;
        _selectedTripShelves = tripIndex is null || _plan is null
            ? null
            : _plan.Trips[tripIndex.Value].Tasks.Select(task => task.Shelf).ToHashSet();
    }

    public bool TryGetCell(PointF point, [NotNullWhen(true)] out Cell? cell)
    {
        cell = null;
        if (Warehouse is null || _cellSize <= 0) return false;

        int x = (int)Math.Floor((point.X - _offsetX) / _cellSize);
        int y = (int)Math.Floor((point.Y - _offsetY) / _cellSize);
        if (!Warehouse.InBounds(x, y)) return false;

        cell = Warehouse[x, y];
        return true;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Warehouse is null) return;

        UpdateLayout(dirtyRect);

        foreach (var cell in Warehouse.Cells())
        {
            canvas.FillColor = GetFillColor(cell);
            canvas.FillRectangle(GetCellRect(cell.X, cell.Y));
        }

        DrawTarget(canvas);
        DrawSelectedRoute(canvas);

        if (SelectedCell is not null)
        {
            canvas.StrokeColor = SelectionColor;
            canvas.StrokeSize = 2;
            canvas.DrawRectangle(GetCellRect(SelectedCell.X, SelectedCell.Y));
        }
    }

    private void DrawTarget(ICanvas canvas)
    {
        if (Target is not { } target) return;

        var rect = GetCellRect(target.X, target.Y);
        float grow = _cellSize * 0.4f;

        canvas.FillColor = TargetColor;
        canvas.FillRectangle(rect.X - grow, rect.Y - grow, rect.Width + 2 * grow, rect.Height + 2 * grow);
    }

    private void DrawSelectedRoute(ICanvas canvas)
    {
        if (_selectedRoute is null || _selectedRoute.Count < 2) return;

        var path = new PathF();
        path.MoveTo(GetCellCenter(_selectedRoute[0]));
        for (int i = 1; i < _selectedRoute.Count; i++)
            path.LineTo(GetCellCenter(_selectedRoute[i]));

        canvas.StrokeColor = Colors.Black;
        canvas.StrokeSize = Math.Max(1.5f, _cellSize * 0.35f);
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawPath(path);
    }

    private void RebuildTripIndex()
    {
        _tripByShelf = new Dictionary<Cell, int>();
        if (_plan is null) return;

        for (int t = 0; t < _plan.Trips.Count; t++)
            foreach (var task in _plan.Trips[t].Tasks)
                _tripByShelf.TryAdd(task.Shelf, t);
    }

    private void UpdateLayout(RectF bounds)
    {
        _cellSize = Math.Min(bounds.Width / Warehouse!.Width, bounds.Height / Warehouse.Height);
        _offsetX = bounds.X + (bounds.Width - _cellSize * Warehouse.Width) / 2;
        _offsetY = bounds.Y + (bounds.Height - _cellSize * Warehouse.Height) / 2;
    }

    private RectF GetCellRect(int x, int y)
    {
        float gap = _cellSize >= 4 ? 0.5f : 0f;
        return new RectF(
            _offsetX + x * _cellSize + gap,
            _offsetY + y * _cellSize + gap,
            _cellSize - 2 * gap,
            _cellSize - 2 * gap);
    }

    private PointF GetCellCenter((int X, int Y) cell) =>
        new(_offsetX + (cell.X + 0.5f) * _cellSize, _offsetY + (cell.Y + 0.5f) * _cellSize);

    private Color GetFillColor(Cell cell)
    {
        if (cell.Shelf is null) return PathColor;
        if (_highlighted.Contains(cell)) return HighlightColor;
        if (TryGetTripColor(cell, out var tripColor)) return tripColor;

        float fill = (float)cell.Shelf.Items.Count / cell.Shelf.Capacity;
        return ShelfColor.WithAlpha(0.3f + 0.7f * fill);
    }

    private bool TryGetTripColor(Cell cell, [NotNullWhen(true)] out Color? color)
    {
        color = null;

        if (_selectedTrip is { } selected)
        {
            if (_selectedTripShelves!.Contains(cell))
                color = TripPalette.ColorFor(selected);
        }
        else if (_tripByShelf.TryGetValue(cell, out int trip))
        {
            color = TripPalette.ColorFor(trip);
        }

        return color is not null;
    }
}