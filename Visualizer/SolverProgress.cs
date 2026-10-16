using Engine.Picking.Genetic;

namespace Visualizer;

/// <summary>
/// Odbiera raporty algorytmu na jego wątku, opcjonalnie go spowalnia (żeby przebieg dało się obejrzeć)
/// i przekazuje raport dalej. Przełączenie na wątek UI robi wywołujący.
/// </summary>
internal sealed class SolverProgress : IProgress<GeneticProgress>
{
    private readonly Action<GeneticProgress> _onReport;
    private readonly int _delayMs;

    public SolverProgress(Action<GeneticProgress> onReport, int delayMs)
    {
        _onReport = onReport;
        _delayMs = delayMs;
    }

    public void Report(GeneticProgress value)
    {
        if (_delayMs > 0) Thread.Sleep(_delayMs);
        _onReport(value);
    }
}