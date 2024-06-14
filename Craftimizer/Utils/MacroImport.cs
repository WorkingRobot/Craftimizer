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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using static Craftimizer.Utils.CommunityMacros;

namespace Craftimizer.Utils;

public static class MacroImport
{
    public static IReadOnlyList<ActionType>? TryParseMacro(string inputMacro)
    {
        var actions = new List<ActionType>();
        foreach (var line in inputMacro.ReplaceLineEndings("\n").Split("\n"))
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

        foreach (var action in Enum.GetValues<ActionType>())
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

    public static Task<CommunityMacro> RetrieveUrl(string url, CancellationToken token)
    {
        if (!TryParseUrl(url, out var uri))
            throw new ArgumentException("Unsupported url", nameof(url));

        return uri.DnsSafeHost switch
        {
            "ffxivteamcraft.com" => RetrieveTeamcraftUrl(uri, token),
            "craftingway.app" => RetrieveCraftingwayUrl(uri, token),
            _ => throw new UnreachableException("TryParseUrl should handle miscellaneous edge cases"),
        };
    }

    private static async Task<CommunityMacro> RetrieveTeamcraftUrl(Uri uri, CancellationToken token)
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

        var resp = await client.GetFromJsonAsync<TeamcraftMacro>(
            $"https://firestore.googleapis.com/v1beta1/projects/ffxivteamcraft/databases/(default)/documents/rotations/{id}",
            token).
            ConfigureAwait(false);
        if (resp is null)
            throw new Exception("Internal error; failed to retrieve macro");
        if (resp.Error is { } error)
            throw new Exception($"Internal server error ({error.Status}); {error.Message}");
        return new(resp);
    }

    private static async Task<CommunityMacro> RetrieveCraftingwayUrl(Uri uri, CancellationToken token)
    {
        using var heCallback = new HappyEyeballsCallback();
        using var client = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = heCallback.ConnectCallback,
        });

        // https://craftingway.app/rotation/variable-blueprint-KmrvS

        var path = uri.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped);
        if (!path.StartsWith("rotation/", StringComparison.Ordinal))
            throw new ArgumentException("Craftingway macro url should start with /rotation", nameof(uri));
        path = path[9..];

        var lastSlash = path.LastIndexOf('/');
        if (lastSlash != -1)
            throw new ArgumentException("Craftingway macro url is not in the right format", nameof(uri));

        var id = path;

        var resp = await client.GetFromJsonAsync<CraftingwayMacro>(
            $"https://servingway.fly.dev/rotation/{id}",
            token)
            .ConfigureAwait(false);
        if (resp is null)
            throw new Exception("Internal error; failed to retrieve macro");
        if (resp.Error is { } error)
            throw new Exception($"Internal server error; {error}");

        return new(resp);
    }
}
