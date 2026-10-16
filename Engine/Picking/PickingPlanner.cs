using System;
using System.Threading;
using Engine.Picking.Genetic;
// Alias - patrz komentarz w DistanceMatrix.cs.
using WarehouseMap = Engine.WarehouseGrid.WarehouseGrid;

namespace Engine.Picking;

/// <summary>
/// Punkt wejścia modułu kompletacji: dzieli listę zamówienia na kursy (buckety) i układa trasę każdego z nich,
/// minimalizując łączną długość wszystkich kursów (każdy zaczyna i kończy się w targecie).
/// </summary>
public static class PickingPlanner
{
    public static PickingPlan Plan(
        WarehouseMap warehouse,
        PickingRequest request,
        GeneticOptions? options = null,
        IProgress<GeneticProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var problem = PickingProblem.Create(warehouse, request);
        var solver = new PickingGeneticSolver(problem, options ?? new GeneticOptions());
        return solver.Solve(progress, cancellationToken);
    }
}