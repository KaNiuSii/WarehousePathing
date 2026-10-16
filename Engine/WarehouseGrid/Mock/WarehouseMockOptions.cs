namespace Engine.WarehouseGrid;

public record WarehouseMockOptions
{
    public int Width { get; init; } = 160;
    public int Height { get; init; } = 100;

    /// <summary>Null = losowy seed. Ten sam seed i opcje dają identyczny magazyn.</summary>
    public int? Seed { get; init; }

    /// <summary>Null = losowy typ układu.</summary>
    public WarehouseLayoutType? LayoutType { get; init; }

    public int ShelfCapacity { get; init; } = WarehouseGrid.DefaultShelfCapacity;

    /// <summary>Jaka część łącznej pojemności półek ma zostać zapełniona (0-1].</summary>
    public double FillRatio { get; init; } = 0.6;
}