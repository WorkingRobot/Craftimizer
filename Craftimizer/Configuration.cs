using Craftimizer.Simulator.Actions;
using Craftimizer.Solver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Craftimizer.Plugin;

public class StoredActionTypeConverter : JsonConverter<ActionType[]>
{
    public override ActionType[] Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException();

        var ret = new List<ActionType>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return [.. ret];
            else if (reader.TokenType == JsonTokenType.String)
            {
                var name = reader.GetString();
                if (Enum.TryParse(name, ignoreCase: false, out ActionType key))
                    ret.Add(key);
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                // https://github.com/WorkingRobot/Craftimizer/blob/90f53de3d88344084bb2413161c8051ef073dc3d/Simulator/Actions/ActionType.cs#L6
                ActionType? key = reader.GetByte() switch
                {
                    0 => ActionType.AdvancedTouch,
                    1 => ActionType.BasicSynthesis,
                    2 => ActionType.BasicTouch,
                    3 => ActionType.ByregotsBlessing,
                    4 => ActionType.CarefulObservation,
                    5 => ActionType.CarefulSynthesis,
                    6 => ActionType.DelicateSynthesis,
                    7 => ActionType.FinalAppraisal,
                    // 8 => ActionType.FocusedSynthesis,
                    // 9 => ActionType.FocusedTouch,
                    10 => ActionType.GreatStrides,
                    11 => ActionType.Groundwork,
                    12 => ActionType.HastyTouch,
                    13 => ActionType.HeartAndSoul,
                    14 => ActionType.Innovation,
                    15 => ActionType.IntensiveSynthesis,
                    16 => ActionType.Manipulation,
                    17 => ActionType.MastersMend,
                    18 => ActionType.MuscleMemory,
                    19 => ActionType.Observe,
                    20 => ActionType.PreciseTouch,
                    21 => ActionType.PreparatoryTouch,
                    22 => ActionType.PrudentSynthesis,
                    23 => ActionType.PrudentTouch,
                    24 => ActionType.RapidSynthesis,
                    25 => ActionType.Reflect,
                    26 => ActionType.StandardTouch,
                    27 => ActionType.TrainedEye,
                    28 => ActionType.TrainedFinesse,
                    29 => ActionType.TricksOfTheTrade,
                    30 => ActionType.Veneration,
                    31 => ActionType.WasteNot,
                    32 => ActionType.WasteNot2,
                    33 => ActionType.StandardTouchCombo,
                    34 => ActionType.AdvancedTouchCombo,
                    // 35 => ActionType.FocusedSynthesisCombo,
                    // 36 => ActionType.FocusedTouchCombo,
                    _ => null,
                };
                if (key is not null)
                    ret.Add(key.Value);
            }
            else
                throw new JsonException();
        }

        throw new JsonException();
    }

    public override void Write(
        Utf8JsonWriter writer,
        ActionType[] value,
        JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
            writer.WriteStringValue(Enum.GetName(item) ?? throw new JsonException());
        writer.WriteEndArray();
    }
}

public class Macro
{
    public static event Action<Macro>? OnMacroChanged;

    public string Name { get; set; } = string.Empty;
    [JsonInclude] [JsonPropertyName("Actions")]
    internal ActionType[] actions { get; set; } = [];
    [JsonIgnore]
    public IReadOnlyList<ActionType> Actions
    {
        get => actions;
        set => ActionEnumerable = value;
    }
    [JsonIgnore]
    public IEnumerable<ActionType> ActionEnumerable
    {
        set
        {
            actions = [.. value];
            OnMacroChanged?.Invoke(this);
        }
    }
}

public class MacroCopyConfiguration
{
    public enum CopyType
    {
        OpenWindow, // useful for big macros
        CopyToMacro, // (add option for down or right) (max macro count; open copy-paste window if too much)
        CopyToClipboard,
    }

    public CopyType Type { get; set; } = CopyType.OpenWindow;

    // CopyToMacro
    public bool CopyDown { get; set; }
    public bool SharedMacro { get; set; }
    public int StartMacroIdx { get; set; } = 1;
    public int MaxMacroCount { get; set; } = 5;

    // Add /nextmacro [down]
    public bool UseNextMacro { get; set; }

    // Add /mlock
    public bool UseMacroLock { get; set; }

    public bool AddNotification { get; set; } = true;

    // Requires AddNotification
    public bool ForceNotification { get; set; }
    public bool AddNotificationSound { get; set; } = true;
    public int IntermediateNotificationSound { get; set; } = 10;
    public int EndNotificationSound { get; set; } = 6;

    // For SND
    public bool RemoveWaitTimes { get; set; }

    // For SND; Cannot use CopyToMacro
    public bool CombineMacro { get; set; }
}

[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public partial class Configuration
{
    public static event Action? OnMacroListChanged;

    [JsonInclude] [JsonPropertyName("Macros")]
    internal List<Macro> macros { get; private set; } = [];
    [JsonIgnore]
    public IReadOnlyList<Macro> Macros => macros;
    public int ReliabilitySimulationCount { get; set; } = 500;
    public bool ConditionRandomness { get; set; } = true;

    [JsonPropertyName("SimulatorSolverConfig")]
    public SolverConfig RecipeNoteSolverConfig { get; set; } = SolverConfig.RecipeNoteDefault;
    public SolverConfig EditorSolverConfig { get; set; } = SolverConfig.EditorDefault;
    public SolverConfig SynthHelperSolverConfig { get; set; } = SolverConfig.SynthHelperDefault;

    public bool EnableSynthHelper { get; set; } = true;
    public bool DisableSynthHelperOnMacro { get; set; } = true;
    public bool ShowOptimalMacroStat { get; set; } = true;
    public bool SuggestMacroAutomatically { get; set; }
    public bool ShowCommunityMacros { get; set; } = true;
    public bool SearchCommunityMacroAutomatically { get; set; }
    public int SynthHelperStepCount { get; set; } = 5;
    public bool SynthHelperDisplayOnlyFirstStep { get; set; }

    public bool PinSynthHelperToWindow { get; set; } = true;
    public bool PinRecipeNoteToWindow { get; set; } = true;

    public MacroCopyConfiguration MacroCopy { get; set; } = new();

    public void AddMacro(Macro macro)
    {
        macros.Add(macro);
        Save();
        OnMacroListChanged?.Invoke();
    }

    public void RemoveMacro(Macro macro)
    {
        if (macros.Remove(macro))
        {
            Save();
            OnMacroListChanged?.Invoke();
        }
    }

    public void SwapMacros(int i, int j)
    {
        (macros[i], macros[j]) = (macros[j], macros[i]);
        Save();
        OnMacroListChanged?.Invoke();
    }

    public void MoveMacro(int fromIdx, int toIdx)
    {
        var macro = macros[fromIdx];
        macros.RemoveAt(fromIdx);
        macros.Insert(toIdx, macro);
        Save();
        OnMacroListChanged?.Invoke();
    }

    [JsonSourceGenerationOptions(Converters = [typeof(StoredActionTypeConverter)])]
    [JsonSerializable(typeof(Configuration))]
    internal sealed partial class JsonContext : JsonSerializerContext { }

    public void Save()
    {
        var f = Service.PluginInterface.ConfigFile;
        using var stream = new FileStream(f.FullName, FileMode.Create, FileAccess.Write);
        JsonSerializer.Serialize(stream, this, JsonContext.Default.Configuration);
    }

    public static Configuration Load()
    {
        var f = Service.PluginInterface.ConfigFile;
        if (f.Exists)
        {
            using var stream = f.OpenRead();

            // System.InvalidOperationException: Setting init-only properties is not supported in source generation mode.
            return JsonSerializer.Deserialize<Configuration>(stream, JsonContext.Default.Options) ?? new();
        }
        return new();
    }
}
