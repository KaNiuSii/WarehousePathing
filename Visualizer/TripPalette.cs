namespace Visualizer;

/// <summary>Kolor kursu: odcienie rozstawione złotym podziałem, więc sąsiednie kursy dobrze się różnią.</summary>
internal static class TripPalette
{
    private const float GoldenRatio = 0.618034f;

    public static Color ColorFor(int tripIndex) =>
        Color.FromHsla((tripIndex * GoldenRatio) % 1f, 0.7f, 0.5f);
}