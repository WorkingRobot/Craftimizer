using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Craftimizer.Solver.Crafty;
internal static class Intrinsics
{
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // https://stackoverflow.com/a/73439472
    private static Vector128<float> HMax(Vector256<float> v1)
    {
        var v2 = Avx.Permute(v1, 0b10110001);
        var v3 = Avx.Max(v1, v2);
        var v4 = Avx.Permute(v3, 0b00001010);
        var v5 = Avx.Max(v3, v4);
        var v6 = Avx.ExtractVector128(v5, 1);
        var v7 = Sse.Max(v5.GetLower(), v6);
        return v7;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HMaxIndexScalar(Vector<float> v, int len)
    {
        var m = 0;
        for (var i = 1; i < len; ++i)
        {
            if (v[i] >= v[m])
                m = i;
        }
        return m;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // https://stackoverflow.com/a/23592221
    private static int HMaxIndexAVX2(Vector<float> v, int len)
    {
        // Remove NaNs
        var vfilt = Avx.Blend(v.AsVector256(), Vector256<float>.Zero, (byte)~((1 << len) - 1));

        // Find max value and broadcast to all lanes
        var vmax128 = HMax(vfilt);
        var vmax = Vector256.Create(vmax128, vmax128);

        // Find the highest index with that value, respecting len
        var vcmp = Avx.CompareEqual(vfilt, vmax);
        var mask = unchecked((uint)Avx2.MoveMask(vcmp.AsByte()));

        var inverseIdx = BitOperations.LeadingZeroCount(mask << ((8 - len) << 2)) >> 2;

        return len - 1 - inverseIdx;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HMaxIndex(Vector<float> v, int len) =>
        Avx2.IsSupported ?
        HMaxIndexAVX2(v, len) :
        HMaxIndexScalar(v, len);
}
