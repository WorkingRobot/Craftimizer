using FFXIVClientStructs.FFXIV.Client.Game.Event;
using System.Runtime.InteropServices;

namespace Craftimizer.Utils;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct CSCraftEventHandler
{
    [FieldOffset(0x48A)] public unsafe fixed ushort WKSClassLevels[2];
    [FieldOffset(0x48E)] public unsafe fixed byte WKSClassJobs[2];

    public static CSCraftEventHandler* Instance()
    {
        return (CSCraftEventHandler*)EventFramework.Instance()->GetCraftEventHandler();
    }
}
