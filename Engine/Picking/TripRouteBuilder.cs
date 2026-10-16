using System.Collections.Generic;
// Alias - patrz komentarz w DistanceMatrix.cs.
using WarehouseMap = Engine.WarehouseGrid.WarehouseGrid;

namespace Engine.Picking;

/// <summary>
/// Rozwija kurs na listę komórek-ścieżek (do rysowania): target -> kolejne pobrania -> target.
/// Przy półce zbieracz staje na ostatniej komórce ścieżki prowadzącej do półki
/// i stamtąd rusza dalej. Trasa może być o kilka kroków dłuższa niż Trip.Distance,
/// bo Trip.Distance zakłada najlepsze pole dostępu dla każdej pary półek.
/// </summary>
public static class TripRouteBuilder
{
    public static IReadOnlyList<(int X, int Y)> Build(WarehouseMap warehouse, (int X, int Y) target, Trip trip)
    {
        var route = new List<(int X, int Y)> { target };
        var current = target;

        foreach (var task in trip.Tasks)
        {
            var leg = warehouse.FindPath(current, (task.Shelf.X, task.Shelf.Y));
            if (leg.Count < 2) continue; // półka nieosiągalna

            // Ostatnia komórka odcinka to sama półka - zostajemy na polu przed nią.
            for (int i = 1; i < leg.Count - 1; i++)
                route.Add(leg[i]);

            current = leg[^2];
        }

        var back = warehouse.FindPath(current, target);
        for (int i = 1; i < back.Count; i++)
            route.Add(back[i]);

        return route;
    }
}