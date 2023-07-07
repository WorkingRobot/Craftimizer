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

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NthBitSetScalar(uint value, int n)
    {
        var mask = 0x0000FFFFu;
        var size = 16;
        var _base = 0;

        if (n++ >= BitOperations.PopCount(value))
            return 32;

        while (size > 0)
        {
            var count = BitOperations.PopCount(value & mask);
            if (n > count)
            {
                _base += size;
                size >>= 1;
                mask |= mask << size;
            }
            else
            {
                size >>= 1;
                mask >>= size;
            }
        }

        return _base;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NthBitSetBMI2(uint value, int n) =>
        BitOperations.TrailingZeroCount(Bmi2.ParallelBitDeposit(1u << n, value));

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NthBitSet(uint value, int n)
    {
        if (n >= BitOperations.PopCount(value))
            return 32;

        return Bmi2.IsSupported ?
            NthBitSetBMI2(value, n) :
            NthBitSetScalar(value, n);
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> ReciprocalSqrt(Vector<float> data)
    {
        if (Avx.IsSupported && Vector<float>.Count >= Vector256<float>.Count)
            return Avx.ReciprocalSqrt(data.AsVector256()).AsVector();

        if (Sse.IsSupported && Vector<float>.Count >= Vector128<float>.Count)
            return Sse.ReciprocalSqrt(data.AsVector128()).AsVector();

        Span<float> result = stackalloc float[Vector<float>.Count];
        for (var i = 0; i < Vector<float>.Count; ++i)
            result[i] = MathF.ReciprocalSqrtEstimate(data[i]);
        return new(result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CASMax(ref float location, float newValue)
    {
        float snapshot;
        do
        {
            snapshot = location;
            if (snapshot >= newValue) return;
        } while (Interlocked.CompareExchange(ref location, newValue, snapshot) != snapshot);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CASAdd(ref float location, float value)
    {
        float snapshot;
        float newValue;
        do
        {
            snapshot = location;
            newValue = snapshot + value;
        }
        while (Interlocked.CompareExchange(ref location, newValue, snapshot) != snapshot);
    }
}
