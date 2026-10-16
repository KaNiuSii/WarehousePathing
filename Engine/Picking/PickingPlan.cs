using System.Collections.Generic;
using Engine.WarehouseGrid;

namespace Engine.Picking;

/// <summary>Jedno pobranie: ile sztuk produktu zabrać z danej półki.</summary>
public record PickTask(Cell Shelf, string Product, int Quantity);

/// <summary>
/// Jeden kurs (bucket): target -> kolejne pobrania (w tej kolejności) -> target.
/// Load = liczba sztuk na wózku, Distance = długość trasy w krokach (z powrotem do targetu).
/// </summary>
public record Trip(IReadOnlyList<PickTask> Tasks, int Load, int Distance);

/// <summary>
/// Wynik optymalizacji. TotalDistance to suma długości wszystkich kursów,
/// BaselineDistance - wynik prostej heurystyki najbliższego sąsiada (punkt odniesienia dla algorytmu genetycznego).
/// </summary>
public record PickingPlan(
    IReadOnlyList<Trip> Trips,
    int TotalDistance,
    int BaselineDistance,
    int Generations);