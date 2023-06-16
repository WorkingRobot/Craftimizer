using Craftimizer.Plugin;
using Dalamud.Utility;
using ImGuiScene;
using System;
using System.Text;

namespace Craftimizer.Simulator;

public record Effect
{
    public EffectType Type { get; init; }
    public int? Duration { get; set; }
    public int? Strength { get; set; }

    public ushort IconId { get
        {
            var status = Type.Status();
            uint iconId = status.Icon;
            if (status.MaxStacks != 0 && Strength != null)
                iconId += (uint)Math.Clamp(Strength.Value, 1, status.MaxStacks) - 1;
            return (ushort)iconId;
        }
    }

    public TextureWrap Icon => Icons.GetIconFromId(IconId);

    public string Tooltip { get
        {
            var status = Type.Status();
            var name = new StringBuilder();
            name.Append(status.Name.ToDalamudString().TextValue);
            if (status.MaxStacks != 0 && Strength != null)
                name.Append($" {Strength}");
            if (!status.IsPermanent && Duration != null)
                name.Append($" > {Duration}");
            return name.ToString();
        }
    }
}
