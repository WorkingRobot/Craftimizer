using Craftimizer.Simulator.Actions;
using Craftimizer.Solver;
using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Craftimizer.Plugin;

public class Macro
{
    public static event Action<Macro>? OnMacroChanged;

    public string Name { get; set; } = string.Empty;
    [JsonInclude] [JsonPropertyName("Actions")]
    internal List<ActionType> actions { get; set; } = [];
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
            actions = new(value);
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
            return JsonSerializer.Deserialize<Configuration>(stream) ?? new();
        }
        return new();
    }
}
