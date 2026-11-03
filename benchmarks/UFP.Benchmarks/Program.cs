using System;

using BenchmarkDotNet.Running;

namespace UFP.Benchmarks
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            if (Array.IndexOf(args, "--filter") < 0)
            {
                args = [.. args, "--filter", "*"];
            }

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
