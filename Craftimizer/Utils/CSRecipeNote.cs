using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using System.Runtime.InteropServices;

namespace Craftimizer.Utils;

[StructLayout(LayoutKind.Explicit, Size = 2880)]
public unsafe struct CSRecipeNote
{
    [FieldOffset(0x118)] public ushort ActiveCraftRecipeId;

    public static CSRecipeNote* Instance()
    {
        return (CSRecipeNote*)RecipeNote.Instance();
    }

    [StructLayout(LayoutKind.Explicit, Size = 136)]
    public struct RecipeIngredient
    {
        [FieldOffset(8)]
        public byte NQCount;

        [FieldOffset(9)]
        public byte HQCount;

        [FieldOffset(16)]
        public Utf8String Name;

        [FieldOffset(120)]
        public uint ItemId;

        [FieldOffset(124)]
        public uint IconId;

        [FieldOffset(130)]
        public byte Amount;

        [FieldOffset(131)]
        public byte Flags;
    }
}
