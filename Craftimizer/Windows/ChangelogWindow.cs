using Craftimizer.Plugin;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;

namespace Craftimizer.Windows;

public sealed class ChangelogWindow : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoCollapse;

    private sealed record Section(string Header, string[] Entries);
    private sealed record Release(string Version, string Title, Section[] Sections);

    private static readonly Release[] Releases =
    [
        // Add new releases at the top
        new("2.11.0.2", "Attempted Crash Hotfix",
        [
            new("Bug Fixes",
            [
                "An attempted hotfix for a rare GC corruption crash that may occur randomly.",
            ]),
        ]),
        new("2.11.0.1", "Hotfix for 2.11.0",
        [
            new("Bug Fixes",
            [
                "\"Cap Quality to Max Collectable Threshold\" no longer applies to Cosmic Exploration collectables. These crafts give bonuses past the highest tier, so the solver will now push for max quality.",
            ]),
        ]),
        new("2.11.0.0", "A Smarter Synthesis Helper!",
        [
            new("New Features / Changes",
            [
                "Added a new \"Next Action\" solver that the Synthesis Helper now uses by default. Instead of solving the whole macro every time, it puts all of its effort into figuring out just the single best next step.",
                "The \"Next Action\" solver has a new \"Time Limit\" setting, so giving you a suggestion within a set amount of time no matter how fast or slow your PC is.",
                "New \"Quality Target (%)\" setting: aim for a set percentage of a recipe's max quality and stop there instead of always pushing for 100%",
                "New \"Cap Quality to Max Collectable Threshold\" setting: on collectables, the solver stops once it reaches the highest collectability tier instead of burning extra steps on quality you don't need.",
                "The solver is faster across the board, and on lower-core PCs the Next Action solver takes a quick first look at the options and then spends its time on the most promising ones (tweakable in the advanced settings).",
                "Removed the old Score Weights settings, since the scoring rework left them doing nothing."
            ]),
            new("Bug Fixes",
            [
                "Fixed a threading issue where the forked/genetic solvers would share one random number generator, which caused lots of annoying rare crashes.",
                "The solver no longer pads the end of a craft with pointless extra actions just to spend leftover durability or CP. It now finishes first, then maximizes quality, then uses as few steps as possible.",
            ]),
        ]),
    ];

    private static string LatestVersion => Releases[0].Version;

    public ChangelogWindow() : base("Craftimizer Changelog", WindowFlags)
    {
        Service.WindowSystem.AddWindow(this);

        IsOpen = false;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(500, 400),
            MaximumSize = new(800, 1400),
        };
    }

    // Opens the window once if a newer changelog exists than the user last saw. Call on startup.
    public void OpenIfUpdated()
    {
        if (string.Equals(Service.Configuration.LastSeenChangelogVersion, LatestVersion, StringComparison.Ordinal))
            return;

        Service.Configuration.LastSeenChangelogVersion = LatestVersion;
        Service.Configuration.Save();

        Open();
    }

    public void Open() => IsOpen = true;

    public override void Draw()
    {
        for (var i = 0; i < Releases.Length; ++i)
        {
            var release = Releases[i];

            var flags = i == 0 ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
            if (ImGui.CollapsingHeader($"{release.Version}\t\t{release.Title}###release_{release.Version}", flags))
                DrawRelease(release);
        }
    }

    private static void DrawRelease(Release release)
    {
        ImGui.Indent();
        foreach (var section in release.Sections)
        {
            ImGui.Spacing();
            using (ImRaii.PushFont(UiBuilder.DefaultFont))
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange))
                ImGui.TextUnformatted(section.Header);
            foreach (var line in section.Entries)
                DrawBullet(line);
        }
        ImGui.Unindent();
        ImGui.Spacing();
    }

    private static void DrawBullet(string text)
    {
        ImGui.TextUnformatted("•");
        ImGui.SameLine();
        ImGui.TextWrapped(text);
    }

    public void Dispose() =>
        Service.WindowSystem.RemoveWindow(this);
}
