namespace Engine.Picking.Genetic;

/// <summary>
/// Osobnik:
///  - Sequence: permutacja indeksów porcji (Chunk) - kolejność odwiedzania (tzw. giant tour),
///  - SourceShift: dla każdej porcji (po indeksie porcji) preferowana półka-źródło,
///    liczona modulo liczba źródeł produktu (0 = najbliższa targetu).
/// Podział na kursy nie jest w genomie - dekoder wyznacza go optymalnie dla danej kolejności.
/// </summary>
internal sealed class Chromosome
{
    public int[] Sequence { get; }
    public int[] SourceShift { get; }
    public int Cost { get; set; } = int.MaxValue;

    public Chromosome(int[] sequence, int[] sourceShift)
    {
        Sequence = sequence;
        SourceShift = sourceShift;
    }

    public Chromosome Clone() => new((int[])Sequence.Clone(), (int[])SourceShift.Clone()) { Cost = Cost };
}