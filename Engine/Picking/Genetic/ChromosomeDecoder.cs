using System;
using System.Collections.Generic;

namespace Engine.Picking.Genetic;

/// <summary>Pobranie przypisane do konkretnego przystanku (półki).</summary>
internal readonly record struct PickAssignment(int Product, int Stop, int Amount);

/// <summary>Zdekodowany osobnik: kolejność pobrań, początki kursów w tej kolejności i łączny koszt.</summary>
internal sealed record DecodedSolution(PickAssignment[] Tasks, int[] TripStarts, int Cost);

/// <summary>
/// Zamienia osobnika na rozwiązanie w dwóch krokach:
///  1. przypisuje porcjom półki (z uwzględnieniem stanów magazynowych),
///  2. dzieli ciąg pobrań na kursy optymalnie (programowanie dynamiczne, "split"):
///     kurs = kolejne pobrania mieszczące się w wózku, koszt = target -> pobrania -> target.
/// Dekoder jest bezstanowy, więc można go wołać równolegle.
/// </summary>
internal sealed class ChromosomeDecoder
{
    private const int TargetStop = PickingProblem.TargetStop;

    private readonly PickingProblem _problem;

    public ChromosomeDecoder(PickingProblem problem) => _problem = problem;

    public DecodedSolution Decode(Chromosome chromosome)
    {
        var tasks = AssignSources(chromosome);
        var (tripStarts, cost) = SplitIntoTrips(tasks);
        return new DecodedSolution(tasks, tripStarts, cost);
    }

    /// <summary>Długość kursu obejmującego pobrania [begin, end): target -> ... -> target.</summary>
    public int TripDistance(PickAssignment[] tasks, int begin, int end)
    {
        var d = _problem.Distances;
        int distance = d[TargetStop, tasks[begin].Stop] + d[tasks[end - 1].Stop, TargetStop];

        for (int i = begin + 1; i < end; i++)
            distance += d[tasks[i - 1].Stop, tasks[i].Stop];

        return distance;
    }

    private PickAssignment[] AssignSources(Chromosome chromosome)
    {
        var stockLeft = (int[])_problem.SourceStock.Clone();
        var tasks = new List<PickAssignment>(chromosome.Sequence.Length);

        foreach (int chunkIndex in chromosome.Sequence)
        {
            var chunk = _problem.Chunks[chunkIndex];
            int first = _problem.SourceOffsets[chunk.Product];
            int count = _problem.SourceOffsets[chunk.Product + 1] - first;
            int start = chromosome.SourceShift[chunkIndex] % count;
            int need = chunk.Amount;

            // Zaczynamy od preferowanej półki; jeśli nie wystarcza towaru, bierzemy z kolejnych.
            for (int k = 0; k < count && need > 0; k++)
            {
                int source = first + (start + k) % count;
                int take = Math.Min(need, stockLeft[source]);
                if (take == 0) continue;

                stockLeft[source] -= take;
                need -= take;
                tasks.Add(new PickAssignment(chunk.Product, _problem.SourceStops[source], take));
            }
        }

        return tasks.ToArray();
    }

    private (int[] TripStarts, int Cost) SplitIntoTrips(PickAssignment[] tasks)
    {
        int n = tasks.Length;
        var d = _problem.Distances;
        int capacity = _problem.CartCapacity;

        // best[i] = minimalny koszt zebrania pierwszych i pobrań; previous[i] = początek ostatniego kursu.
        var best = new int[n + 1];
        var previous = new int[n + 1];

        for (int end = 1; end <= n; end++)
        {
            best[end] = int.MaxValue;
            int returnLeg = d[tasks[end - 1].Stop, TargetStop];
            int load = 0;
            int inner = 0; // suma odległości między kolejnymi pobraniami w kursie

            for (int begin = end; begin >= 1; begin--)
            {
                var task = tasks[begin - 1];
                load += task.Amount;
                if (load > capacity) break;

                if (begin < end) inner += d[task.Stop, tasks[begin].Stop];

                int cost = best[begin - 1] + d[TargetStop, task.Stop] + inner + returnLeg;
                if (cost < best[end])
                {
                    best[end] = cost;
                    previous[end] = begin - 1;
                }
            }
        }

        var starts = new List<int>();
        for (int end = n; end > 0; end = previous[end])
            starts.Add(previous[end]);
        starts.Reverse();

        return (starts.ToArray(), best[n]);
    }
}