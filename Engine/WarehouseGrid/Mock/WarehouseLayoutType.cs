namespace Engine.WarehouseGrid;

public enum WarehouseLayoutType
{
    /// <summary>Długie, równoległe alejki z regałami po obu stronach i korytarzami poprzecznymi (klasyczny magazyn kompletacyjny).</summary>
    ParallelAisles,

    /// <summary>Pasma regałów podzielone na krótkie sekcje z przesuniętymi przejściami (magazyn strefowy / blokowy).</summary>
    StaggeredBlocks,

    /// <summary>Alejki z regałami oraz otwarta strefa przyjęć/wydań (dock) przy jednej krawędzi.</summary>
    CrossDock,

    /// <summary>Układ nieregularny: bazowy układ z polanami (np. strefy pakowania) i losowymi lukami w regałach.</summary>
    Irregular
}