"""Benchmarks of the Python port (src/UFP.Python/ufp.py).

Run from the repository root:

    py benchmarks/UFP.Python.Benchmarks/benchmark.py

Every scenario parses the same value many times, so the compiled pattern cache
(see ufp._compile) is warm. That is the steady state of an application that parses
many values with one pattern; a one-shot pattern pays a small compilation cost.
Use --iterations and --repeat to tune the run.
"""

import argparse
import os
import platform
import sys
import timeit

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


def main():
    parser = argparse.ArgumentParser(description="Benchmarks of the Python UFP port.")
    parser.add_argument("--iterations", type=int, default=20000,
                        help="number of Parse calls per measurement (default: 20000)")
    parser.add_argument("--repeat", type=int, default=7,
                        help="number of measurements per scenario (default: 7)")
    arguments = parser.parse_args()

    print("Python %s (%s)" % (platform.python_version(), platform.platform()))
    print()
    print("| Benchmark | Mean |")
    print("| --- | ---: |")

    for name, pattern, description, value in CASES:
        nanoseconds = measure(value, pattern, arguments.iterations, arguments.repeat)
        print("| `%s` - %s | %.1f ns |" % (name, description, nanoseconds))


if __name__ == "__main__":
    main()
