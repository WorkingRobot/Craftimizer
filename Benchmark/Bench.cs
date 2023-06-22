using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Craftimizer.Benchmark;

[SimpleJob(RuntimeMoniker.Net70)]
[SimpleJob(RuntimeMoniker.NativeAot70)]
public class Bench
{
    private float[] data;
    private int[] dataLengths;

    [Params(1000, 10000)]
    public int N;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random();
        data = new float[N * 8];
        dataLengths = new int[N];
        for (var i = 0; i < data.Length; i += 8)
        {
            var len = rand.NextSingle() > .5 ? 8 : rand.Next(1, 9);
            dataLengths[i / 8] = len;
            for (var j = 0; j < len; ++j)
                data[i + j] = rand.NextSingle();
            for (var j = len; j < 8; ++j)
                data[i + j] = float.NaN;
        }
    }

    [Benchmark]
    public int[] Scalar()
    {
        var d = new int[N];
        var dataSpan = data.AsSpan();
        for (var i = 0; i < N; ++i)
            d[i] = Solver.Crafty.Solver.HMaxIndexScalar(new Vector<float>(dataSpan.Slice(i * 8, 8)), dataLengths[i]);
        return d;
    }

    [Benchmark]
    public int[] AVX2()
    {
        var d = new int[128];
        var dataSpan = data.AsSpan();
        for (var i = 0; i < 128; ++i)
            d[i] = Solver.Crafty.Solver.HMaxIndexAVX2(new Vector<float>(dataSpan.Slice(i * 8, 8)), dataLengths[i]);
        return d;
    }
}
