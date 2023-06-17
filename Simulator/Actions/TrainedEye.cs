namespace Craftimizer.Simulator.Actions;

internal class TrainedEye : BaseAction
{
    public override ActionCategory Category => ActionCategory.FirstTurn;
    public override int Level => 80;
    public override uint ActionId => 100283;

    public override int CPCost => 250;
    public override bool IncreasesQuality => true;

    public override bool CanUse =>
        Simulation.IsFirstStep &&
        !Simulation.Input.Recipe.IsExpert &&
        Simulation.Input.Stats.Level >= (Simulation.Input.Recipe.ClassJobLevel + 10) &&
        base.CanUse;

    public override void UseSuccess() =>
        Simulation.IncreaseQualityRaw(Simulation.Input.Recipe.MaxQuality - Simulation.Quality);
}
