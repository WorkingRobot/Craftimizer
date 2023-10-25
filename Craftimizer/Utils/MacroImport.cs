using Craftimizer.Plugin;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Dalamud.Networking.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Craftimizer.Utils;

public static class MacroImport
{
    public static IReadOnlyList<ActionType>? TryParseMacro(string inputMacro)
    {
        var actions = new List<ActionType>();
        foreach(var line in inputMacro.ReplaceLineEndings("\n").Split("\n"))
        {
            if (TryParseLine(line) is { } action)
                actions.Add(action);
        }
        return actions.Count > 0 ? actions : null;
    }

    private static ActionType? TryParseLine(string line)
    {
        if (line.StartsWith("/ac", StringComparison.OrdinalIgnoreCase))
            line = line[3..];
        else if (line.StartsWith("/action", StringComparison.OrdinalIgnoreCase))
            line = line[7..];
        else
            return null;

        line = line.TrimStart();

        // get first word
        if (line.StartsWith('"'))
        {
            line = line[1..];

            var end = line.IndexOf('"', 1);
            if (end != -1)
                line = line[..end];
        }
        else
        {
            var end = line.IndexOf(' ', 1);
            if (end != -1)
                line = line[..end];
        }

        foreach(var action in Enum.GetValues<ActionType>())
        {
            if (line.Equals(action.GetName(ClassJob.Carpenter), StringComparison.OrdinalIgnoreCase))
                return action;
        }
        return null;
    }

    public static bool TryParseUrl(string url, out Uri uri)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out uri!))
            return false;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!uri.IsDefaultPort)
            return false;

        return uri.DnsSafeHost is "ffxivteamcraft.com" or "craftingway.app";
    }

    public static Task<RetrievedMacro> RetrieveUrl(string url, CancellationToken token)
    {
        if (!TryParseUrl(url, out var uri))
            throw new ArgumentException("Unsupported url", nameof(url));

        switch (uri.DnsSafeHost)
        {
            case "ffxivteamcraft.com":
                return RetrieveTeamcraftUrl(uri, token);
            case "craftingway.app":
                return RetrieveCraftingwayUrl(uri, token);
            default:
                throw new UnreachableException("TryParseUrl should handle miscellaneous edge cases");
        }
    }

    private sealed record TeamcraftMacro
    {
        public sealed record StringValue
        {
            [JsonPropertyName("stringValue")]
            [JsonRequired]
            public required string Value { get; set; }

            public static implicit operator string(StringValue v) => v.Value;
        }

        public sealed record IntegerValue
        {
            [JsonPropertyName("integerValue")]
            [JsonRequired]
            public required int Value { get; set; }

            public static implicit operator int(IntegerValue v) => v.Value;
        }

        public sealed record MapValue<T>
        {
            public sealed record ValueData
            {
                [JsonRequired]
                public required T Fields { get; set; }
            }

            [JsonPropertyName("mapValue")]
            [JsonRequired]
            public required ValueData Data { get; set; }

            public T Value => Data.Fields;
            public static implicit operator T(MapValue<T> v) => v.Value;
        }

        public sealed record ArrayValue<T>
        {
            public sealed record ValueData
            {
                [JsonRequired]
                public required T[] Values { get; set; }
            }

            [JsonPropertyName("arrayValue")]
            [JsonRequired]
            public required ValueData Data { get; set; }

            public T[] Value => Data.Values;
            public static implicit operator T[](ArrayValue<T> v) => v.Value;
        }

        public sealed record RecipeFieldData
        {
            [JsonRequired]
            public required IntegerValue RLvl { get; set; }
            [JsonRequired]
            public required IntegerValue Durability { get; set; }
        }

        public sealed record FieldData
        {
            public StringValue? Name { get; set; }
            [JsonRequired]
            public required ArrayValue<StringValue> Rotation { get; set; }
            public MapValue<RecipeFieldData>? Recipe { get; set; }
        }

        public sealed record ErrorData
        {
            public required int Code { get; set; }
            public required string Message { get; set; }
            public required string Status { get; set; }
        }

        public FieldData? Fields { get; set; }

        public ErrorData? Error { get; set; }
    }

    public readonly record struct RetrievedMacro(string Name, IReadOnlyList<ActionType> Actions);

    private static async Task<RetrievedMacro> RetrieveTeamcraftUrl(Uri uri, CancellationToken token)
    {
        using var heCallback = new HappyEyeballsCallback();
        using var client = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = heCallback.ConnectCallback,
        });

        var path = uri.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped);
        if (!path.StartsWith("simulator/", StringComparison.Ordinal))
            throw new ArgumentException("Teamcraft macro url should start with /simulator", nameof(uri));
        path = path[10..];

        var lastSlash = path.LastIndexOf('/');
        if (lastSlash == -1)
            throw new ArgumentException("Teamcraft macro url is not in the right format", nameof(uri));

        var id = path[(lastSlash + 1)..];

        var resp = await client.GetFromJsonAsync<TeamcraftMacro>($"https://firestore.googleapis.com/v1beta1/projects/ffxivteamcraft/databases/(default)/documents/rotations/{id}", token).ConfigureAwait(false);
        if (resp is null)
            throw new Exception("Internal error; failed to retrieve macro");
        if (resp.Error is { } error)
            throw new Exception($"Internal server error ({error.Status}); {error.Message}");
        if (resp.Fields is not { } rotation)
            throw new Exception($"Internal error; No fields or error was returned");
        // https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/67f453041c6b2b31d32fcf6e1fd53aa38ed7a12b/apps/client/src/app/model/other/crafting-rotation.ts#L49
        var name = rotation.Name?.Value ??
            (rotation.Recipe is { Value: var recipe } ?
                $"rlvl{recipe.RLvl.Value} - {rotation.Rotation.Value.Length} steps, {recipe.Durability.Value} dur" :
                "New Teamcraft Rotation");
        var actions = new List<ActionType>();
        foreach(var action in rotation.Rotation.Value)
        {
            ActionType? actionType = action.Value switch
            {
                "BasicSynthesis" =>		    ActionType.BasicSynthesis,
                "CarefulSynthesis" =>	    ActionType.CarefulSynthesis,
                "PrudentSynthesis" =>	    ActionType.PrudentSynthesis,
                "RapidSynthesis" =>		    ActionType.RapidSynthesis,
                "Groundwork" =>		        ActionType.Groundwork,
                "FocusedSynthesis" =>	    ActionType.FocusedSynthesis,
                "MuscleMemory" =>		    ActionType.MuscleMemory,
                "IntensiveSynthesis" =>	    ActionType.IntensiveSynthesis,
                "BasicTouch" =>		        ActionType.BasicTouch,
                "StandardTouch" =>		    ActionType.StandardTouch,
                "AdvancedTouch" =>		    ActionType.AdvancedTouch,
                "HastyTouch" =>		        ActionType.HastyTouch,
                "ByregotsBlessing" =>	    ActionType.ByregotsBlessing,
                "PreciseTouch" =>		    ActionType.PreciseTouch,
                "FocusedTouch" =>		    ActionType.FocusedTouch,
                "PrudentTouch" =>		    ActionType.PrudentTouch,
                "TrainedEye" =>		        ActionType.TrainedEye,
                "PreparatoryTouch" =>	    ActionType.PreparatoryTouch,
                "Reflect" =>		        ActionType.Reflect,
                "TrainedFinesse" =>		    ActionType.TrainedFinesse,
                "TricksOfTheTrade" =>	    ActionType.TricksOfTheTrade,
                "MastersMend" =>		    ActionType.MastersMend,
                "Manipulation" =>		    ActionType.Manipulation,
                "WasteNot" =>		        ActionType.WasteNot,
                "WasteNotII" =>		        ActionType.WasteNot2,
                "GreatStrides" =>		    ActionType.GreatStrides,
                "Innovation" =>		        ActionType.Innovation,
                "Veneration" =>		        ActionType.Veneration,
                "FinalAppraisal" =>		    ActionType.FinalAppraisal,
                "Observe" =>		        ActionType.Observe,
                "HeartAndSoul" =>		    ActionType.HeartAndSoul,
                "CarefulObservation" =>		ActionType.CarefulObservation,
                "DelicateSynthesis" =>		ActionType.DelicateSynthesis,
                "RemoveFinalAppraisal" =>	throw new Exception("Removing Final Appraisal is an unsupported action"),
                null => null,
                { } actionValue => throw new Exception($"Unknown action {actionValue}"),
            };
            if (actionType.HasValue)
                actions.Add(actionType.Value);
        }
        return new(name, actions);
    }

    private static async Task<RetrievedMacro> RetrieveCraftingwayUrl(Uri uri, CancellationToken token)
    {
        using var heCallback = new HappyEyeballsCallback();
        using var client = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = heCallback.ConnectCallback,
        });

        throw new NotImplementedException();
    }
}
