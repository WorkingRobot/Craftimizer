using Dalamud.Networking.Http;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Craftimizer.Simulator.Actions;
using Craftimizer.Simulator;
using Craftimizer.Solver;

namespace Craftimizer.Utils;

public sealed class CommunityMacros
{
    public sealed record BooleanValue
    {
        [JsonPropertyName("booleanValue")]
        [JsonRequired]
        public required bool Value { get; set; }

        public static implicit operator bool(BooleanValue v) => v.Value;
    }

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
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
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
            public T[]? Values { get; set; }
        }

        [JsonPropertyName("arrayValue")]
        [JsonRequired]
        public required ValueData Data { get; set; }

        public T[] Value => Data.Values ?? [];
        public static implicit operator T[](ArrayValue<T> v) => v.Value;
    }

    public sealed record ErrorData
    {
        public required int Code { get; set; }
        public required string Message { get; set; }
        public required string Status { get; set; }
    }

    public sealed record StructuredQuery
    {
        [JsonRequired]
        public required List<CollectionSelector> From { get; set; }
        [JsonRequired]
        public required Filter Where { get; set; }
        [JsonRequired]
        public required List<Order> OrderBy { get; set; }
    }

    public sealed record CollectionSelector
    {
        public required string CollectionId { get; set; }
    }

    public sealed record Filter
    {
        public CompositeFilter? CompositeFilter { get; set; }
        public FieldFilter? FieldFilter { get; set; }
    }

    public sealed record CompositeFilter
    {
        [JsonRequired]
        public required List<Filter> Filters { get; set; }
        [JsonRequired]
        public required CompositeOperator Op { get; set; }
    }

    public enum CompositeOperator
    {
        OPERATOR_UNSPECIFIED,
        AND,
        OR
    }

    public sealed record FieldFilter
    {
        [JsonRequired]
        public required FieldReference Field { get; set; }
        [JsonRequired]
        public required FieldOperator Op { get; set; }
        public object? Value { get; set; }
    }

    public enum FieldOperator
    {
        OPERATOR_UNSPECIFIED,
        LESS_THAN,
        LESS_THAN_OR_EQUAL,
        GREATER_THAN,
        GREATER_THAN_OR_EQUAL,
        EQUAL,
        NOT_EQUAL,
        ARRAY_CONTAINS,
        IN,
        ARRAY_CONTAINS_ANY,
        NOT_IN
    }

    public sealed record Order
    {
        [JsonRequired]
        public required FieldReference Field { get; set; }
        [JsonRequired]
        public required Direction Direction { get; set; }
    }

    public sealed record FieldReference
    {
        [JsonRequired]
        public required string FieldPath { get; set; }
    }

    public enum Direction
    {
        DIRECTION_UNSPECIFIED,
        ASCENDING,
        DESCENDING
    }

    private sealed record RunQueryRequest
    {
        [JsonRequired]
        public required StructuredQuery StructuredQuery { get; set; }
    }

    public sealed record TeamcraftMacro
    {
        public sealed record RecipeFieldData
        {
            [JsonRequired]
            [JsonPropertyName("rlvl")]
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

        public string? Name { get; set; }

        public FieldData? Fields { get; set; }

        public ErrorData? Error { get; set; }
    }

    private sealed record QueriedTeamcraftMacro
    {
        public TeamcraftMacro? Document { get; set; }

        public DateTimeOffset? ReadTime { get; set; }

        public ErrorData? Error { get; set; }
    }

    public sealed record CraftingwayMacro
    {
        public int Id { get; set; }
        public string? Slug { get; set; }
        public string? Version { get; set; }
        public string? Job { get; set; }
        [JsonPropertyName("job_level")]
        public int JobLevel { get; set; }
        public int Craftsmanship { get; set; }
        public int Control { get; set; }
        public int CP { get; set; }
        public string? Food { get; set; }
        public string? Potion { get; set; }
        [JsonPropertyName("recipe_job_level")]
        public int RecipeJobLevel { get; set; }
        public string? Recipe { get; set; }
        // HqIngredients
        public string? Actions { get; set; }
        [JsonPropertyName("created_at")]
        public long CreatedAt { get; set; }
        public string? Error { get; set; }
    }

    public sealed record CommunityMacro
    {
        public string Name { get; }
        public string? Url { get; }
        public IReadOnlyList<ActionType> Actions { get; }

        public CommunityMacro(TeamcraftMacro macro)
        {
            if (macro.Fields is not { } rotation)
                throw new Exception($"Internal error; No fields were returned");

            // https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/67f453041c6b2b31d32fcf6e1fd53aa38ed7a12b/apps/client/src/app/model/other/crafting-rotation.ts#L49
            Name = rotation.Name?.Value ??
                (rotation.Recipe is { Value: var recipe } ?
                    $"rlvl{recipe.RLvl.Value} - {rotation.Rotation.Value.Length} steps, {recipe.Durability.Value} dur" :
                    "New Teamcraft Rotation");

            var actions = new List<ActionType>();
            foreach (var action in rotation.Rotation.Value)
            {
                ActionType? actionType = action.Value switch
                {
                    "BasicSynthesis" => ActionType.BasicSynthesis,
                    "CarefulSynthesis" => ActionType.CarefulSynthesis,
                    "PrudentSynthesis" => ActionType.PrudentSynthesis,
                    "RapidSynthesis" => ActionType.RapidSynthesis,
                    "Groundwork" => ActionType.Groundwork,
                    "MuscleMemory" => ActionType.MuscleMemory,
                    "IntensiveSynthesis" => ActionType.IntensiveSynthesis,
                    "BasicTouch" => ActionType.BasicTouch,
                    "StandardTouch" => ActionType.StandardTouch,
                    "AdvancedTouch" => ActionType.AdvancedTouch,
                    "HastyTouch" => ActionType.HastyTouch,
                    "DaringTouch" => ActionType.DaringTouch,
                    "ByregotsBlessing" => ActionType.ByregotsBlessing,
                    "PreciseTouch" => ActionType.PreciseTouch,
                    "PrudentTouch" => ActionType.PrudentTouch,
                    "TrainedEye" => ActionType.TrainedEye,
                    "PreparatoryTouch" => ActionType.PreparatoryTouch,
                    "Reflect" => ActionType.Reflect,
                    "TrainedFinesse" => ActionType.TrainedFinesse,
                    "RefinedTouch" => ActionType.RefinedTouch,
                    "TrainedPerfection" => ActionType.TrainedPerfection,
                    "TricksOfTheTrade" => ActionType.TricksOfTheTrade,
                    "MastersMend" => ActionType.MastersMend,
                    "Manipulation" => ActionType.Manipulation,
                    "ImmaculateMend" => ActionType.ImmaculateMend,
                    "WasteNot" => ActionType.WasteNot,
                    "WasteNotII" => ActionType.WasteNot2,
                    "GreatStrides" => ActionType.GreatStrides,
                    "Innovation" => ActionType.Innovation,
                    "Veneration" => ActionType.Veneration,
                    "FinalAppraisal" => ActionType.FinalAppraisal,
                    "QuickInnovation" => ActionType.QuickInnovation,
                    "Observe" => ActionType.Observe,
                    "HeartAndSoul" => ActionType.HeartAndSoul,
                    "CarefulObservation" => ActionType.CarefulObservation,
                    "DelicateSynthesis" => ActionType.DelicateSynthesis,

                    "RemoveFinalAppraisal" => null,
                    // Old actions?
                    _ => null
                };
                if (actionType.HasValue)
                    actions.Add(actionType.Value);
            }

            Actions = actions;

            if (!string.IsNullOrEmpty(macro.Name))
            {
                if (Uri.TryCreate(macro.Name, UriKind.Relative, out _))
                {
                    var rotationId = macro.Name.Split('/')[^1];
                    if (!string.IsNullOrEmpty(rotationId))
                        Url = $"https://ffxivteamcraft.com/simulator/custom/{rotationId}";
                }
            }
        }

        public CommunityMacro(CraftingwayMacro macro)
        {
            if (macro.Actions is not { } rotation)
                throw new Exception($"Internal error; No actions were returned");

            Name = macro.Slug ?? "New Craftingway Rotation";
            var actions = new List<ActionType>();
            foreach (var action in rotation.Split(','))
            {
                ActionType? actionType = action switch
                {
                    "MuscleMemory" => ActionType.MuscleMemory,
                    "Reflect" => ActionType.Reflect,
                    "TrainedEye" => ActionType.TrainedEye,
                    "Veneration" => ActionType.Veneration,
                    "GreatStrides" => ActionType.GreatStrides,
                    "Innovation" => ActionType.Innovation,
                    "QuickInnovation" => ActionType.QuickInnovation,
                    "BasicSynthesis" => ActionType.BasicSynthesis,
                    "BasicSynthesisTraited" => ActionType.BasicSynthesis,
                    "CarefulSynthesis" => ActionType.CarefulSynthesis,
                    "CarefulSynthesisTraited" => ActionType.CarefulSynthesis,
                    "PrudentSynthesis" => ActionType.PrudentSynthesis,
                    "Groundwork" => ActionType.Groundwork,
                    "GroundworkTraited" => ActionType.Groundwork,
                    "BasicTouch" => ActionType.BasicTouch,
                    "StandardTouch" => ActionType.StandardTouch,
                    "AdvancedTouch" => ActionType.AdvancedTouch,
                    "ByregotsBlessing" => ActionType.ByregotsBlessing,
                    "PrudentTouch" => ActionType.PrudentTouch,
                    "PreparatoryTouch" => ActionType.PreparatoryTouch,
                    "TrainedFinesse" => ActionType.TrainedFinesse,
                    "RefinedTouch" => ActionType.RefinedTouch,
                    "MastersMend" => ActionType.MastersMend,
                    "ImmaculateMend" => ActionType.ImmaculateMend,
                    "WasteNot" => ActionType.WasteNot,
                    "WasteNotII" => ActionType.WasteNot2,
                    "Manipulation" => ActionType.Manipulation,
                    "TrainedPerfection" => ActionType.TrainedPerfection,
                    "Observe" => ActionType.Observe,
                    "DelicateSynthesis" => ActionType.DelicateSynthesis,
                    "DelicateSynthesisTraited" => ActionType.DelicateSynthesis,

                    // Old actions?
                    _ => null
                };
                if (actionType.HasValue)
                    actions.Add(actionType.Value);
            }
            Actions = actions;
        }

        public (float Score, SimulationState FinalState) CalculateScore(SimulatorNoRandom simulator, in SimulationState startingState, in MCTSConfig mctsConfig)
        {
            return CalculateScore(Actions, simulator, startingState, mctsConfig);
        }

        public static (float Score, SimulationState FinalState) CalculateScore(IReadOnlyCollection<ActionType> actions, SimulatorNoRandom simulator, in SimulationState startingState, in MCTSConfig mctsConfig)
        {
            var (resp, outState, failedIdx) = simulator.ExecuteMultiple(startingState, actions);
            outState.ActionCount = actions.Count;
            var score = SimulationNode.CalculateScoreForState(outState, simulator.CompletionState, mctsConfig) ?? 0;
            if (resp != ActionResponse.SimulationComplete)
            {
                if (failedIdx != -1)
                    score /= 2;
            }
            return (score, outState);
        }
    }

    private Dictionary<int, List<CommunityMacro>> CachedRotations { get; } = [];

    public async Task<IReadOnlyList<CommunityMacro>> RetrieveRotations(int rlvl, CancellationToken token)
    {
        lock (CachedRotations)
        {
            if (CachedRotations.TryGetValue(rlvl, out var cachedMacros))
                return cachedMacros;
        }

        var tcMacros = await RetrieveRotationsInternal(rlvl, token).ConfigureAwait(false);
        var macros = tcMacros.Select(macro => new CommunityMacro(macro)).ToList();
        lock (CachedRotations)
            CachedRotations.TryAdd(rlvl, macros);
        return macros;
    }

    private static async Task<List<TeamcraftMacro>> RetrieveRotationsInternal(int rlvl, CancellationToken token)
    {
        using var heCallback = new HappyEyeballsCallback();
        using var client = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = heCallback.ConnectCallback,
        });

        var request = new RunQueryRequest
        {
            StructuredQuery = new StructuredQuery
            {
                From =
                [
                    new() { CollectionId = "rotations" }
                ],
                Where = new Filter
                {
                    CompositeFilter = new CompositeFilter
                    {
                        Op = CompositeOperator.AND,
                        Filters =
                        [
                            new()
                            {
                                FieldFilter = new FieldFilter
                                {
                                    Field = new FieldReference { FieldPath = "public" },
                                    Op = FieldOperator.EQUAL,
                                    Value = new BooleanValue { Value = true }
                                }
                            },
                            new()
                            {
                                FieldFilter = new FieldFilter
                                {
                                    Field = new FieldReference { FieldPath = "community.rlvl" },
                                    Op = FieldOperator.EQUAL,
                                    Value = new IntegerValue { Value = rlvl }
                                }
                            }
                        ]
                    },
                },
                OrderBy =
                [
                    new()
                    {
                        Field = new FieldReference { FieldPath = "xivVersion" },
                        Direction = Direction.DESCENDING
                    },
                    new()
                    {
                        Field = new FieldReference { FieldPath = "__name__" },
                        Direction = Direction.DESCENDING
                    }
                ]
            },
        };

        var resp = await PostFromJsonAsync<RunQueryRequest, List<QueriedTeamcraftMacro>>(
            client,
            $"https://firestore.googleapis.com/v1beta1/projects/ffxivteamcraft/databases/(default)/documents:runQuery",
            request, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }, token).
            ConfigureAwait(false);
        if (resp is null)
            throw new Exception("Internal server error; failed to retrieve macro");

        foreach(var macro in resp)
        {
            if (macro.Error is { } error)
                throw new Exception($"Internal server error ({error.Status}); {error.Message}");
        }

        return resp.Where(macro => macro.Document is not null).Select(macro => macro.Document!).ToList();
    }

    private static async Task<TResponse?> PostFromJsonAsync<TRequest, TResponse>(HttpClient client, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, TRequest value, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        var resp = client.PostAsJsonAsync(requestUri, value, options, cancellationToken);
        using var message = await resp.ConfigureAwait(false);
        message.EnsureSuccessStatusCode();

        return await message.Content!.ReadFromJsonAsync<TResponse>(options, cancellationToken).ConfigureAwait(false);
    }
}
