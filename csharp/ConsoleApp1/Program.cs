using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System;

public static class StringExtensions
{
    public static string ReverseSpans(this string input)
    {
        char[] arr = input.AsSpan().ToArray();
        Array.Reverse(arr);
        return new string(arr);
    }

    public static string Reverse(this string input)
    {
        char[] arr = input.ToCharArray();
        Array.Reverse(arr);
        return new string(arr);
    }
}

public class StringExtensionsBenchmark
{
    [Params(1000)]
    public int Iterations { get; set; }

    [Benchmark]
    public string ReverseArrays()
    {
        var largeString = new string('x', 1000000);
        for (int i = 0; i < Iterations; i++)
        {
            largeString.Reverse();
        }
        return largeString;
    }

    [Benchmark]
    public string ReverseSpans()
    {
        var largeString = new string('x', 1000000);
        for (int i = 0; i < Iterations; i++)
        {
            largeString.ReverseSpans();
        }
        return largeString;
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        var benchmark = new StringExtensionsBenchmark { Iterations = 100 };
        BenchmarkRunner.Run(typeof(StringExtensionsBenchmark));
    }
}