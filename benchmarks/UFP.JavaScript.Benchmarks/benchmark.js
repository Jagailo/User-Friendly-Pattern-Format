/**
 * Benchmarks of the JavaScript port (src/UFP.JavaScript/ufp.js).
 *
 * Run from the repository root:
 *
 *     node benchmarks/UFP.JavaScript.Benchmarks/benchmark.js
 *     npm run benchmark -- --iterations 20000 --repeat 7
 *
 * Every scenario parses the same value many times, so the compiled pattern cache (see
 * compileAlternative in src/UFP.JavaScript/ufp.js) is warm. That is the steady state of
 * an application that parses many values with one pattern; a one-shot pattern pays a
 * small compilation cost. Use --iterations, --repeat and --samples to tune the run.
 *
 * The memory column is the heap allocated by one Parse call. It is measured from
 * process.memoryUsage() deltas around one and two calls after a forced garbage
 * collection, so the allocation of the measurement itself cancels out. This needs
 * global.gc, so the script re-runs itself with --expose-gc when memory is measured.
 */

import { spawnSync } from "node:child_process";
import { arch, cpus, platform } from "node:os";
import { fileURLToPath } from "node:url";
import { parseArgs } from "node:util";

import { parse } from "../../src/UFP.JavaScript/ufp.js";

const SCRIPT_PATH = fileURLToPath(import.meta.url);

const USAGE = `Usage: node benchmarks/UFP.JavaScript.Benchmarks/benchmark.js [options]

Options:
  -i, --iterations <n>  number of Parse calls per measurement (default: 20000)
  -r, --repeat <n>      number of measurements per scenario (default: 7)
  -s, --samples <n>     number of memory samples per scenario (default: 100)
      --no-memory       skip the allocated-memory measurement
  -h, --help            show this help`;

if (typeof globalThis.gc !== "function" && !process.argv.slice(2).includes("--no-memory")) {
    // The memory measurement needs a forced garbage collection; re-run with the flag.
    const child = spawnSync(process.execPath, ["--expose-gc", SCRIPT_PATH, ...process.argv.slice(2)], { stdio: "inherit" });
    if (child.error) {
        throw child.error;
    }

    process.exit(child.status ?? 1);
}

const CASES = [
    {
        name: "Single",
        pattern: "<Date>_<Name>.<Extension>",
        description: "`<Date>_<Name>.<Extension>`",
        value: "04-05-2024_fh5_image.png",
    },
    {
        name: "MegaPattern",
        pattern: "<Protocol>://<Domain>/<Page>?<FirstParameter.Key>=<FirstParameter.Value>"
            + "|<Date>_<Name>.<Extension>"
            + "|<StudyUid*>.<Type>.<Extension>",
        description: "three alternatives in one pattern",
        value: "1.2.840.52394.3.152.235.2.12.187636473.patient.csv",
    },
    {
        name: "GreedyVariables",
        pattern: "<Path*>/<Filename>.<Extension>",
        description: "`<Path*>/<Filename>.<Extension>`",
        value: "docs/reports/2024/annual_report.pdf",
    },
];

/**
 * Returns the best mean time of one Parse call in nanoseconds.
 *
 * @param {string} value
 * @param {string} pattern
 * @param {number} iterations Parse calls per measurement.
 * @param {number} repeat Number of measurements.
 * @returns {number}
 */
function measure(value, pattern, iterations, repeat) {
    parse(value, pattern); // warm up the compiled pattern cache

    let best = Number.POSITIVE_INFINITY;

    for (let run = 0; run < repeat; run++) {
        let last;

        const start = process.hrtime.bigint();
        for (let iteration = 0; iteration < iterations; iteration++) {
            last = parse(value, pattern);
        }

        const elapsed = Number(process.hrtime.bigint() - start);

        // The last result is used after the loop, so the calls are not dead code, and a
        // scenario that stopped capturing variables fails loudly instead of measuring nothing.
        if (last === undefined || Object.keys(last).length === 0) {
            throw new Error("The benchmark loop did not capture any variable.");
        }

        const nanoseconds = elapsed / iterations;
        if (nanoseconds < best) {
            best = nanoseconds;
        }
    }

    return best;
}

/**
 * The last result of a measured call, kept alive so the engine cannot drop the call.
 */
let lastResult;

/**
 * Returns the heap allocated by `count` Parse calls.
 *
 * @param {string} value
 * @param {string} pattern
 * @param {number} count
 * @returns {number}
 */
function measureCalls(value, pattern, count) {
    globalThis.gc();
    const before = process.memoryUsage().heapUsed;

    for (let index = 0; index < count; index++) {
        lastResult = parse(value, pattern);
    }

    return process.memoryUsage().heapUsed - before;
}

/**
 * Returns the heap allocated by one Parse call in bytes.
 *
 * The difference between the allocation of two calls and one call removes the overhead
 * of the measurement itself (the object returned by process.memoryUsage).
 *
 * @param {string} value
 * @param {string} pattern
 * @param {number} samples Number of memory samples.
 * @returns {number}
 */
function measureAllocated(value, pattern, samples) {
    for (let index = 0; index < 20; index++) {
        parse(value, pattern); // warm up the JIT and the compiled pattern cache
    }

    const one = [];
    const two = [];
    for (let index = 0; index < samples; index++) {
        one.push(measureCalls(value, pattern, 1));
        two.push(measureCalls(value, pattern, 2));
    }

    return median(two) - median(one);
}

/**
 * @param {number[]} values
 * @returns {number}
 */
function median(values) {
    const sorted = [...values].sort((left, right) => left - right);
    return sorted[Math.floor(sorted.length / 2)];
}

/**
 * @param {number} bytes
 * @returns {string}
 */
function formatBytes(bytes) {
    return bytes < 1024 ? `${Math.round(bytes)} B` : `${(bytes / 1024).toFixed(2)} KB`;
}

/**
 * @param {string[]} argv
 * @returns {{ iterations: number, repeat: number, samples: number, memory: boolean }}
 */
function parseOptions(argv) {
    const { values } = parseArgs({
        args: argv,
        options: {
            iterations: { type: "string", short: "i", default: "20000" },
            repeat: { type: "string", short: "r", default: "7" },
            samples: { type: "string", short: "s", default: "100" },
            "no-memory": { type: "boolean", default: false },
            help: { type: "boolean", short: "h", default: false },
        },
        allowPositionals: false,
    });

    if (values.help) {
        console.log(USAGE);
        process.exit(0);
    }

    return {
        iterations: toPositiveInteger(values.iterations, "--iterations"),
        repeat: toPositiveInteger(values.repeat, "--repeat"),
        samples: toPositiveInteger(values.samples, "--samples"),
        memory: !values["no-memory"],
    };
}

/**
 * @param {string} value
 * @param {string} name
 * @returns {number}
 */
function toPositiveInteger(value, name) {
    const parsed = Number(value);
    if (!Number.isInteger(parsed) || parsed <= 0) {
        throw new Error(`${name} must be a positive integer, got '${value}'.`);
    }

    return parsed;
}

function main() {
    let options;
    try {
        options = parseOptions(process.argv.slice(2));
    } catch (error) {
        console.error(error.message);
        console.error();
        console.error(USAGE);
        return 1;
    }

    const processor = cpus()[0];
    const cpu = processor === undefined ? "" : `, ${processor.model.trim()}`;
    console.log(`Node.js ${process.version} (${platform()} ${arch()})${cpu}`);
    console.log();

    console.log(options.memory ? "| Benchmark | Mean | Allocated |" : "| Benchmark | Mean |");
    console.log(options.memory ? "| --- | ---: | ---: |" : "| --- | ---: |");

    for (const scenario of CASES) {
        const nanoseconds = measure(scenario.value, scenario.pattern, options.iterations, options.repeat);
        if (!options.memory) {
            console.log(`| \`${scenario.name}\` - ${scenario.description} | ${nanoseconds.toFixed(1)} ns |`);
            continue;
        }

        const bytes = measureAllocated(scenario.value, scenario.pattern, options.samples);
        console.log(`| \`${scenario.name}\` - ${scenario.description} | ${nanoseconds.toFixed(1)} ns | ${formatBytes(bytes)} |`);
    }

    if (options.memory && (lastResult === undefined || Object.keys(lastResult).length === 0)) {
        throw new Error("The memory measurement did not capture any variable.");
    }

    return 0;
}

process.exitCode = main();
