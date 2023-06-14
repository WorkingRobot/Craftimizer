using Lumina.Excel.GeneratedSheets;

namespace Craftimizer.Simulator;

public enum ClassJob
{
    Carpenter,
    Blacksmith,
    Armorer,
    Goldsmith,
    Leatherworker,
    Weaver,
    Alchemist,
    Culinarian
}

internal static class ClassJobExtensions
{
    public static bool IsClassJob(this ClassJobCategory me, ClassJob classJob) =>
        classJob switch
        {
            ClassJob.Carpenter => me.CRP,
            ClassJob.Blacksmith => me.BSM,
            ClassJob.Armorer => me.ARM,
            ClassJob.Goldsmith => me.GSM,
            ClassJob.Leatherworker => me.LTW,
            ClassJob.Weaver => me.WVR,
            ClassJob.Alchemist => me.ALC,
            ClassJob.Culinarian => me.CUL,
            _ => false
        };
}
