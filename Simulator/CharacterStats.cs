namespace Craftimizer.Simulator;

public sealed record CharacterStats
{
    public int Craftsmanship { get; init; }
    public int Control { get; init; }
    public int CP { get; init; }
    public int Level { get; init; }
    public bool HasSplendorousBuff { get; init; }
    public bool IsSpecialist { get; init; }
    public int CLvl { get; init; }
}
