"""Benchmarks of the Python port (src/UFP.Python/ufp.py).

Run from the repository root:

    py benchmarks/UFP.Python.Benchmarks/benchmark.py
    py benchmarks/UFP.Python.Benchmarks/benchmark.py --iterations 20000 --repeat 7
    py benchmarks/UFP.Python.Benchmarks/benchmark.py --no-memory

Every scenario parses the same value many times, so the compiled pattern cache
(see ufp._compile) is warm. That is the steady state of an application that parses
many values with one pattern; a one-shot pattern pays a small compilation cost.
Use --iterations, --repeat and --samples to tune the run.

The memory column is the memory allocated by one Parse call and still alive after it:
tracemalloc is read around one call and around two calls whose results are kept, and
the median difference between the two measurements is the cost of a single result map.
The compiled plans stay in the cache and are not part of it, so the column shows the
result dict, the captured values and the small positions list. tracemalloc slows the
interpreter down, so it is started only for the memory phase - the timing phase runs
without it (use --no-memory to skip the memory column altogether).
"""

import argparse
import gc
import os
import platform
import sys
import timeit
import tracemalloc

REPOSITORY_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(REPOSITORY_ROOT, "src", "UFP.Python"))

import ufp  # noqa: E402 - the port is a single module, not a package

CASES = (
    (
        "Single",
        "<Date>_<Name>.<Extension>",
        "`<Date>_<Name>.<Extension>`",
        "04-05-2024_fh5_image.png",
    ),
    (
        "MegaPattern",
        "<Protocol>://<Domain>/<Page>?<FirstParameter.Key>=<FirstParameter.Value>"
        "|<Date>_<Name>.<Extension>|<StudyUid*>.<Type>.<Extension>",
        "three alternatives in one pattern",
        "1.2.840.52394.3.152.235.2.12.187636473.patient.csv",
    ),
    (
        "GreedyVariables",
        "<Path*>/<Filename>.<Extension>",
        "`<Path*>/<Filename>.<Extension>`",
        "docs/reports/2024/annual_report.pdf",
    ),
)


def measure(value, pattern, iterations, repeat):
    """Returns the best mean time of one Parse call in nanoseconds."""
    parse = ufp.parse
    parse(value, pattern)  # warm up the compiled pattern cache
    timer = timeit.Timer(lambda: parse(value, pattern))
    best = min(timer.repeat(repeat=repeat, number=iterations))
    return best / iterations * 1e9


def measure_retained(value, pattern, count):
    """Returns the memory allocated by `count` Parse calls whose results stay alive.

    `count` is 1 or 2; the results are kept in local variables until the second
    tracemalloc reading, so their memory is part of the delta.
    """
    gc.collect()
    before = tracemalloc.get_traced_memory()[0]

    first = ufp.parse(value, pattern)
    second = ufp.parse(value, pattern) if count > 1 else None

    after = tracemalloc.get_traced_memory()[0]

    if not first or (count > 1 and not second):
        raise RuntimeError("The memory measurement did not capture any variable.")

    return after - before


def measure_allocated(value, pattern, samples):
    """Returns the memory allocated by one Parse call in bytes.

    The difference between the memory of two calls and of one call removes the
    overhead of the measurement itself, so what is left is one result map.
    """
    for _ in range(20):
        ufp.parse(value, pattern)  # warm up the compiled pattern cache

    tracemalloc.start()
    try:
        one = [measure_retained(value, pattern, 1) for _ in range(samples)]
        two = [measure_retained(value, pattern, 2) for _ in range(samples)]
    finally:
        tracemalloc.stop()

    return median(two) - median(one)


def median(values):
    """Returns the median of `values` (the upper one for an even count)."""
    ordered = sorted(values)
    return ordered[len(ordered) // 2]


def format_bytes(size):
    """Formats a byte count the way the other benchmarks of the repository do."""
    return "%d B" % size if size < 1024 else "%.2f KB" % (size / 1024)


def main():
    parser = argparse.ArgumentParser(description="Benchmarks of the Python UFP port.")
    parser.add_argument("--iterations", type=int, default=20000,
                        help="number of Parse calls per measurement (default: 20000)")
    parser.add_argument("--repeat", type=int, default=7,
                        help="number of measurements per scenario (default: 7)")
    parser.add_argument("--samples", type=int, default=100,
                        help="number of memory samples per scenario (default: 100)")
    parser.add_argument("--no-memory", dest="memory", action="store_false",
                        help="skip the allocated-memory measurement")
    arguments = parser.parse_args()

    print("Python %s (%s)" % (platform.python_version(), platform.platform()))
    print()
    print("| Benchmark | Mean | Allocated |" if arguments.memory else "| Benchmark | Mean |")
    print("| --- | ---: | ---: |" if arguments.memory else "| --- | ---: |")

    for name, pattern, description, value in CASES:
        nanoseconds = measure(value, pattern, arguments.iterations, arguments.repeat)
        row = "| `%s` - %s | %.1f ns |" % (name, description, nanoseconds)

        if arguments.memory:
            allocated = measure_allocated(value, pattern, arguments.samples)
            row += " %s |" % format_bytes(allocated)

        print(row)


if __name__ == "__main__":
    main()
