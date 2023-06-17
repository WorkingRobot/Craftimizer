namespace Craftimizer.Simulator;

public record CharacterStats
{
    public int Craftsmanship { get; init; }
    public int Control { get; init; }
    public int CP { get; init; }
    public int Level { get; init; }
    public bool HasRelic { get; init; }
    public bool IsSpecialist { get; init; }
    public int CLvl { get; init; }
}
