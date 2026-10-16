using System;
using System.Collections.Generic;
using System.Linq;

namespace Engine.Picking.Genetic;

/// <summary>Tworzenie osobników, krzyżowanie i mutacje. Używa jednego Random - nie jest wielowątkowe.</summary>
internal sealed class GeneticOperators
{
    private const double NearestSourceProbability = 0.6;

    private readonly PickingProblem _problem;
    private readonly GeneticOptions _options;
    private readonly Random _rng;

    public GeneticOperators(PickingProblem problem, GeneticOptions options, Random rng)
    {
        _problem = problem;
        _options = options;
        _rng = rng;
    }

    public Chromosome CreateRandom()
    {
        int n = _problem.Chunks.Length;
        var sequence = Enumerable.Range(0, n).ToArray();
        Shuffle(sequence);

        var shifts = new int[n];
        for (int i = 0; i < n; i++)
            shifts[i] = RandomShift(_problem.Chunks[i].Product, _rng);

        return new Chromosome(sequence, shifts);
    }

    /// <summary>Heurystyka najbliższego sąsiada z najbliższych targetu źródeł - dobry punkt startowy populacji.</summary>
    public Chromosome CreateNearestNeighbor()
    {
        int n = _problem.Chunks.Length;
        var remaining = Enumerable.Range(0, n).ToList();
        var sequence = new int[n];
        int currentStop = PickingProblem.TargetStop;

        for (int i = 0; i < n; i++)
        {
            int bestPosition = 0;
            int bestDistance = int.MaxValue;

            for (int r = 0; r < remaining.Count; r++)
            {
                int distance = _problem.Distances[currentStop, NearestSourceStop(remaining[r])];
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPosition = r;
                }
            }

            sequence[i] = remaining[bestPosition];
            currentStop = NearestSourceStop(sequence[i]);

            remaining[bestPosition] = remaining[^1];
            remaining.RemoveAt(remaining.Count - 1);
        }

        return new Chromosome(sequence, new int[n]);
    }

    /// <summary>Order crossover (OX) dla kolejności + losowe dziedziczenie wyboru źródeł po porcjach.</summary>
    public Chromosome Crossover(Chromosome a, Chromosome b)
    {
        int n = a.Sequence.Length;
        int p1 = _rng.Next(n), p2 = _rng.Next(n);
        if (p1 > p2) (p1, p2) = (p2, p1);

        var child = new int[n];
        var used = new bool[n];
        for (int i = p1; i <= p2; i++)
        {
            child[i] = a.Sequence[i];
            used[child[i]] = true;
        }

        int position = (p2 + 1) % n;
        for (int k = 0; k < n; k++)
        {
            int gene = b.Sequence[(p2 + 1 + k) % n];
            if (used[gene]) continue;

            child[position] = gene;
            position = (position + 1) % n;
        }

        var shifts = new int[n];
        for (int i = 0; i < n; i++)
            shifts[i] = _rng.Next(2) == 0 ? a.SourceShift[i] : b.SourceShift[i];

        return new Chromosome(child, shifts);
    }

    public void Mutate(Chromosome chromosome)
    {
        if (_rng.NextDouble() < _options.SequenceMutationRate)
        {
            int moves = _options.MutationMoveDivisor > 0
                ? 1 + chromosome.Sequence.Length / _options.MutationMoveDivisor
                : 1;
            for (int i = 0; i < moves; i++)
                ApplySequenceMove(chromosome.Sequence, _rng);
        }

        if (_rng.NextDouble() < _options.SourceMutationRate)
            ApplySourceMove(chromosome, _rng, changes: 1 + _rng.Next(3));
    }

    /// <summary>Jeden losowy ruch (zwykle kolejność, czasem wybór źródła) - używany przez lokalne dopieszczanie.</summary>
    public void ApplyRandomMove(Chromosome chromosome, Random rng)
    {
        if (rng.NextDouble() < 0.85)
            ApplySequenceMove(chromosome.Sequence, rng);
        else
            ApplySourceMove(chromosome, rng, changes: 1);
    }

    private void ApplySequenceMove(int[] sequence, Random rng)
    {
        int n = sequence.Length;
        if (n < 2) return;

        int i = rng.Next(n);
        int j = PickPartner(i, n, rng);
        if (i == j) return;

        switch (rng.Next(3))
        {
            case 0: // zamiana dwóch porcji
                (sequence[i], sequence[j]) = (sequence[j], sequence[i]);
                break;
            case 1: // odwrócenie fragmentu (ruch 2-opt)
                Array.Reverse(sequence, Math.Min(i, j), Math.Abs(i - j) + 1);
                break;
            default: // przesunięcie jednej porcji w inne miejsce
                MoveElement(sequence, i, j);
                break;
        }
    }

    /// <summary>Drugi koniec ruchu: najczęściej blisko pierwszego (ruch lokalny), czasem gdziekolwiek.</summary>
    private int PickPartner(int i, int n, Random rng)
    {
        if (_options.MoveLocality > 0 && rng.NextDouble() < 0.7)
        {
            int window = Math.Max(2, (int)(n * _options.MoveLocality));
            return Math.Clamp(i + rng.Next(-window, window + 1), 0, n - 1);
        }

        return rng.Next(n);
    }

    private void ApplySourceMove(Chromosome chromosome, Random rng, int changes)
    {
        for (int k = 0; k < changes; k++)
        {
            int chunkIndex = rng.Next(chromosome.SourceShift.Length);
            chromosome.SourceShift[chunkIndex] = RandomShift(_problem.Chunks[chunkIndex].Product, rng);
        }
    }

    private int RandomShift(int product, Random rng)
    {
        int sourceCount = _problem.SourceOffsets[product + 1] - _problem.SourceOffsets[product];
        return rng.NextDouble() < NearestSourceProbability ? 0 : rng.Next(sourceCount);
    }

    private int NearestSourceStop(int chunkIndex)
    {
        int product = _problem.Chunks[chunkIndex].Product;
        return _problem.SourceStops[_problem.SourceOffsets[product]];
    }

    private void Shuffle(int[] array)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }

    private static void MoveElement(int[] array, int from, int to)
    {
        int value = array[from];

        if (from < to)
            Array.Copy(array, from + 1, array, from, to - from);
        else
            Array.Copy(array, to, array, to + 1, from - to);

        array[to] = value;
    }
}