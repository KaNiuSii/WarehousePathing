using System;
using System.Threading.Tasks;

namespace Engine.Picking.Genetic;

/// <summary>
/// Lokalne dopieszczanie osobnika (hill climbing): losowy ruch, zostawiamy go tylko jeśli skraca trasę.
/// Odpalane równolegle na kilku kopiach, każda z własnym generatorem losowym (wynik jest powtarzalny).
/// </summary>
internal sealed class LocalSearch
{
    private readonly GeneticOperators _operators;
    private readonly ChromosomeDecoder _decoder;

    public LocalSearch(GeneticOperators operators, ChromosomeDecoder decoder)
    {
        _operators = operators;
        _decoder = decoder;
    }

    /// <summary>Zwraca poprawioną kopię najlepszej z prób albo null, jeśli żadna nie poprawiła wyniku.</summary>
    public Chromosome? Improve(Chromosome start, int iterations, int[] branchSeeds)
    {
        var results = new Chromosome[branchSeeds.Length];

        Parallel.For(0, branchSeeds.Length, branch =>
            results[branch] = Climb(start, iterations, new Random(branchSeeds[branch])));

        Chromosome? best = null;
        foreach (var result in results) // przy remisie wygrywa kopia o niższym numerze (determinizm)
            if (result.Cost < start.Cost && (best is null || result.Cost < best.Cost))
                best = result;

        return best;
    }

    private Chromosome Climb(Chromosome start, int iterations, Random rng)
    {
        var current = start.Clone();
        var trial = start.Clone();

        for (int i = 0; i < iterations; i++)
        {
            Array.Copy(current.Sequence, trial.Sequence, current.Sequence.Length);
            Array.Copy(current.SourceShift, trial.SourceShift, current.SourceShift.Length);

            _operators.ApplyRandomMove(trial, rng);
            trial.Cost = _decoder.Decode(trial).Cost;

            if (trial.Cost < current.Cost)
                (current, trial) = (trial, current);
        }

        return current;
    }
}