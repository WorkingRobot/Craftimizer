using Dalamud.Utility;
using ExdSheets;
using ExdSheets.Sheets;
using Lumina.Data;

namespace Craftimizer.Plugin;

public static class LuminaSheets
{
    private static readonly Module Module = new(Service.DataManager.GameData, Service.DataManager.Language.ToLumina());

    public static readonly Sheet<Recipe> RecipeSheet = Module.GetSheet<Recipe>();
    public static readonly Sheet<Action> ActionSheet = Module.GetSheet<Action>();
    public static readonly Sheet<CraftAction> CraftActionSheet = Module.GetSheet<CraftAction>();
    public static readonly Sheet<Status> StatusSheet = Module.GetSheet<Status>();
    public static readonly Sheet<Addon> AddonSheet = Module.GetSheet<Addon>();
    public static readonly Sheet<ClassJob> ClassJobSheet = Module.GetSheet<ClassJob>();
    public static readonly Sheet<Item> ItemSheet = Module.GetSheet<Item>();
    public static readonly Sheet<Item> ItemSheetEnglish = Module.GetSheet<Item>(Language.English)!;
    public static readonly Sheet<ENpcResident> ENpcResidentSheet = Module.GetSheet<ENpcResident>();
    public static readonly Sheet<Level> LevelSheet = Module.GetSheet<Level>();
    public static readonly Sheet<Quest> QuestSheet = Module.GetSheet<Quest>();
    public static readonly Sheet<Materia> MateriaSheet = Module.GetSheet<Materia>();
    public static readonly Sheet<BaseParam> BaseParamSheet = Module.GetSheet<BaseParam>();
    public static readonly Sheet<ItemFood> ItemFoodSheet = Module.GetSheet<ItemFood>();
    public static readonly Sheet<SatisfactionSupply> SatisfactionSupplySheet = Module.GetSheet<SatisfactionSupply>();
}
