using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Engine.Picking.Genetic;

/// <summary>
/// Algorytm genetyczny: selekcja turniejowa, krzyżowanie OX, mutacje, elitaryzm.
/// Ocena osobnika = łączna długość wszystkich kursów (mniej = lepiej), liczona równolegle.
/// </summary>
internal sealed class PickingGeneticSolver
{
    private readonly PickingProblem _problem;
    private readonly GeneticOptions _options;
    private readonly Random _rng;
    private readonly ChromosomeDecoder _decoder;
    private readonly GeneticOperators _operators;
    private readonly LocalSearch _localSearch;

    public PickingGeneticSolver(PickingProblem problem, GeneticOptions options)
    {
        ValidateOptions(options);

        _problem = problem;
        _options = options;
        _rng = new Random(options.Seed ?? Random.Shared.Next());
        _decoder = new ChromosomeDecoder(problem);
        _operators = new GeneticOperators(problem, options, _rng);
        _localSearch = new LocalSearch(_operators, _decoder);
    }

    public PickingPlan Solve(IProgress<GeneticProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var nearestNeighbor = _operators.CreateNearestNeighbor();
        var population = CreateInitialPopulation(nearestNeighbor);
        Evaluate(population);
        population.Sort(CompareByCost);
        ImproveBest(population);

        var best = population[0];
        int baseline = nearestNeighbor.Cost;
        int stagnation = 0;
        int generation = 0;

        progress?.Report(new GeneticProgress(0, best.Cost, baseline, progress is null ? null : BuildPlan(best, baseline, 0)));

        while (generation < _options.Generations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            generation++;

            population = NextGeneration(population);
            ImproveBest(population);

            PickingPlan? improvedPlan = null;
            if (population[0].Cost < best.Cost)
            {
                best = population[0];
                stagnation = 0;
                if (progress is not null) improvedPlan = BuildPlan(best, baseline, generation);
            }
            else
            {
                stagnation++;
            }

            progress?.Report(new GeneticProgress(generation, best.Cost, baseline, improvedPlan));

            if (_options.StagnationLimit > 0 && stagnation >= _options.StagnationLimit)
                break;
        }

        return BuildPlan(best, baseline, generation);
    }

    /// <summary>Lokalne dopieszczanie najlepszego osobnika; poprawiona kopia zastępuje go na czele populacji.</summary>
    private void ImproveBest(List<Chromosome> sortedPopulation)
    {
        if (_options.LocalSearchIterations <= 0) return;

        int branches = Math.Clamp(Environment.ProcessorCount, 1, 8);
        var seeds = Enumerable.Range(0, branches).Select(_ => _rng.Next()).ToArray();

        var improved = _localSearch.Improve(sortedPopulation[0], _options.LocalSearchIterations, seeds);
        if (improved is not null)
            sortedPopulation[0] = improved;
    }

    private List<Chromosome> CreateInitialPopulation(Chromosome nearestNeighbor)
    {
        var population = new List<Chromosome>(_options.PopulationSize) { nearestNeighbor };
        while (population.Count < _options.PopulationSize)
            population.Add(_operators.CreateRandom());

        return population;
    }

    private List<Chromosome> NextGeneration(List<Chromosome> sortedPopulation)
    {
        var next = new List<Chromosome>(_options.PopulationSize);
        next.AddRange(sortedPopulation.Take(_options.EliteCount));

        var children = new List<Chromosome>();
        while (next.Count + children.Count < _options.PopulationSize)
            children.Add(CreateChild(sortedPopulation));

        Evaluate(children);
        next.AddRange(children);
        next.Sort(CompareByCost);
        return next;
    }

    private Chromosome CreateChild(List<Chromosome> sortedPopulation)
    {
        var parentA = SelectByTournament(sortedPopulation);
        var parentB = SelectByTournament(sortedPopulation);

        var child = _rng.NextDouble() < _options.CrossoverRate
            ? _operators.Crossover(parentA, parentB)
            : parentA.Clone();

        _operators.Mutate(child);
        return child;
    }

    /// <summary>Populacja jest posortowana, więc zwycięzca turnieju to losowany osobnik o najmniejszym indeksie.</summary>
    private Chromosome SelectByTournament(List<Chromosome> sortedPopulation)
    {
        int winner = sortedPopulation.Count - 1;
        for (int i = 0; i < _options.TournamentSize; i++)
            winner = Math.Min(winner, _rng.Next(sortedPopulation.Count));

        return sortedPopulation[winner];
    }

    private void Evaluate(List<Chromosome> chromosomes) =>
        Parallel.ForEach(chromosomes, chromosome => chromosome.Cost = _decoder.Decode(chromosome).Cost);

    private PickingPlan BuildPlan(Chromosome best, int baselineDistance, int generations)
    {
        var solution = _decoder.Decode(best);
        var trips = new List<Trip>(solution.TripStarts.Length);

        for (int t = 0; t < solution.TripStarts.Length; t++)
        {
            int begin = solution.TripStarts[t];
            int end = t + 1 < solution.TripStarts.Length ? solution.TripStarts[t + 1] : solution.Tasks.Length;
            trips.Add(BuildTrip(solution.Tasks, begin, end));
        }

        return new PickingPlan(trips, solution.Cost, baselineDistance, generations);
    }

    private Trip BuildTrip(PickAssignment[] assignments, int begin, int end)
    {
        var tasks = new List<PickTask>(end - begin);
        int load = 0;

        for (int i = begin; i < end; i++)
        {
            var a = assignments[i];
            tasks.Add(new PickTask(_problem.StopCells[a.Stop], _problem.Products[a.Product], a.Amount));
            load += a.Amount;
        }

        return new Trip(tasks, load, _decoder.TripDistance(assignments, begin, end));
    }

    private static int CompareByCost(Chromosome a, Chromosome b) => a.Cost.CompareTo(b.Cost);

    private static void ValidateOptions(GeneticOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.PopulationSize < 2)
            throw new ArgumentOutOfRangeException(nameof(options), "PopulationSize musi wynosić co najmniej 2.");
        if (options.EliteCount < 0 || options.EliteCount >= options.PopulationSize)
            throw new ArgumentOutOfRangeException(nameof(options), "EliteCount musi być w przedziale [0, PopulationSize).");
        if (options.TournamentSize < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "TournamentSize musi wynosić co najmniej 1.");
        if (options.Generations < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Generations nie może być ujemne.");
    }
}