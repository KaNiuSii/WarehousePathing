namespace Engine.Picking.Genetic;

public record GeneticOptions
{
    public int PopulationSize { get; init; } = 150;
    public int Generations { get; init; } = 400;

    /// <summary>Ilu najlepszych osobników przechodzi do następnego pokolenia bez zmian.</summary>
    public int EliteCount { get; init; } = 5;

    public int TournamentSize { get; init; } = 4;
    public double CrossoverRate { get; init; } = 0.9;

    /// <summary>Szansa na mutację kolejności porcji (zamiana, odwrócenie fragmentu, przesunięcie).</summary>
    public double SequenceMutationRate { get; init; } = 0.5;

    /// <summary>Szansa na mutację wyboru półek-źródeł.</summary>
    public double SourceMutationRate { get; init; } = 0.3;

    /// <summary>
    /// Ile ruchów mutacji dostaje zmutowany osobnik: 1 + (liczba porcji / MutationMoveDivisor); 0 = zawsze jeden ruch.
    /// Uwaga: w testach więcej ruchów PSUJE wynik (rozbija dobre trasy), więc domyślnie jeden.
    /// </summary>
    public int MutationMoveDivisor { get; init; } = 0;

    /// <summary>
    /// Zasięg ruchów mutacji jako ułamek długości kolejności. Ruchy lokalne (blisko siebie) rzadziej psują dobrą trasę
    /// niż losowe z całej listy. 0 = zawsze losowe z całej listy.
    /// </summary>
    public double MoveLocality { get; init; } = 0.1;

    /// <summary>
    /// Ile prób poprawy najlepszego osobnika (losowe ruchy z zachowaniem tylko poprawy) w każdym pokoleniu.
    /// Próby idą równolegle na kilku niezależnych kopiach najlepszego (po tyle prób na kopię),
    /// więc czas pokolenia rośnie o czas tylu ocen. 0 = wyłączone.
    /// </summary>
    public int LocalSearchIterations { get; init; } = 0;

    /// <summary>Przerwij, jeśli wynik nie poprawił się przez tyle pokoleń (0 = nie przerywaj).</summary>
    public int StagnationLimit { get; init; } = 100;

    /// <summary>Null = losowy seed. Ten sam seed i dane dają ten sam wynik.</summary>
    public int? Seed { get; init; }

    /// <summary>Szybka optymalizacja: ustawienia domyślne.</summary>
    public static GeneticOptions Fast => new();

    /// <summary>
    /// Agresywna optymalizacja: więcej pokoleń, cierpliwsze zatrzymanie i lokalne dopieszczanie najlepszego
    /// osobnika w każdym pokoleniu. W testach (magazyn 160x100) dawało o 2-4 punkty procentowe krótszą trasę
    /// niż ustawienia domyślne, kosztem kilkukrotnie dłuższego liczenia.
    /// </summary>
    public static GeneticOptions Aggressive => new()
    {
        Generations = 1500,
        LocalSearchIterations = 300,
        StagnationLimit = 300
    };
}

/// <summary>
/// Raport z przebiegu algorytmu (pokolenie 0 = populacja początkowa).
/// BestPlan jest wypełniony tylko wtedy, gdy najlepsze rozwiązanie zmieniło się od poprzedniego raportu.
/// </summary>
public record GeneticProgress(int Generation, int BestDistance, int BaselineDistance, PickingPlan? BestPlan);