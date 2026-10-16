using Engine.Picking;

namespace Visualizer;

/// <summary>Wiersz listy kursów.</summary>
public record TripRow(int Index, Trip Trip)
{
    public Color Color => TripPalette.ColorFor(Index);

    public string Title =>
        $"Kurs {Index + 1}: {Trip.Tasks.Count} pobrań, {Trip.Load} szt., {Trip.Distance} kroków";
}