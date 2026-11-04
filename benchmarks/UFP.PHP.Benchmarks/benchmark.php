<?php

declare(strict_types=1);

/**
 * Benchmarks of the PHP port (src/UFP.PHP/ufp.php).
 *
 * Run from the repository root:
 *
 *     php benchmarks/UFP.PHP.Benchmarks/benchmark.php
 *     php benchmarks/UFP.PHP.Benchmarks/benchmark.php --iterations 200000 --repeat 7
 *     php benchmarks/UFP.PHP.Benchmarks/benchmark.php --no-memory
 *
 * Every scenario parses the same value many times, so the compiled pattern cache (see
 * compileAlternative in src/UFP.PHP/ufp.php) is warm. That is the steady state of an
 * application that parses many values with one pattern; a one-shot pattern pays a small
 * compilation cost. Use --iterations, --repeat and --samples to tune the run.
 *
 * The memory column is the memory retained by one Parse call: `memory_get_usage()` is read
 * around one call and around two calls, and the median difference between the two
 * measurements is the cost of a single result map. The compiled plans stay in the cache
 * and are not part of it, so the column shows the result array, the captured values and
 * the strings they keep alive. PHP frees everything else as soon as refcounts drop, so the
 * measurement needs no garbage collector control; `gc_collect_cycles()` is called anyway
 * so that a cycle left by the harness cannot leak into a sample.
 */

require_once __DIR__ . '/../../src/UFP.PHP/ufp.php';

use function UFP\parse;

const USAGE = <<<'TEXT'
Usage: php benchmarks/UFP.PHP.Benchmarks/benchmark.php [options]

Options:
  -i, --iterations <n>  number of Parse calls per measurement (default: 20000)
  -r, --repeat <n>      number of measurements per scenario (default: 7)
  -s, --samples <n>     number of memory samples per scenario (default: 100)
      --no-memory       skip the retained-memory measurement
  -h, --help            show this help
TEXT;

/**
 * The same three scenarios as the .NET, Python and JavaScript benchmarks.
 */
const CASES = [
    [
        'name' => 'Single',
        'pattern' => '<Date>_<Name>.<Extension>',
        'description' => '`<Date>_<Name>.<Extension>`',
        'value' => '04-05-2024_fh5_image.png',
    ],
    [
        'name' => 'MegaPattern',
        'pattern' => '<Protocol>://<Domain>/<Page>?<FirstParameter.Key>=<FirstParameter.Value>'
            . '|<Date>_<Name>.<Extension>'
            . '|<StudyUid*>.<Type>.<Extension>',
        'description' => 'three alternatives in one pattern',
        'value' => '1.2.840.52394.3.152.235.2.12.187636473.patient.csv',
    ],
    [
        'name' => 'GreedyVariables',
        'pattern' => '<Path*>/<Filename>.<Extension>',
        'description' => '`<Path*>/<Filename>.<Extension>`',
        'value' => 'docs/reports/2024/annual_report.pdf',
    ],
];

/**
 * Returns the best mean time of one Parse call in nanoseconds.
 *
 * @param string $value Text to parse.
 * @param string $pattern Pattern or pattern set.
 * @param int $iterations Parse calls per measurement.
 * @param int $repeat Number of measurements.
 */
function measure(string $value, string $pattern, int $iterations, int $repeat): float
{
    parse($value, $pattern); // warm up the compiled pattern cache

    $best = INF;

    for ($run = 0; $run < $repeat; $run++) {
        $last = [];
        $start = hrtime(true);

        for ($iteration = 0; $iteration < $iterations; $iteration++) {
            $last = parse($value, $pattern);
        }

        $elapsed = hrtime(true) - $start;

        // The last result is used after the loop, so the calls are not dead code, and a
        // scenario that stopped capturing variables fails loudly instead of measuring nothing.
        if ($last === []) {
            throw new RuntimeException('The benchmark loop did not capture any variable.');
        }

        $best = min($best, $elapsed / $iterations);
    }

    return $best;
}

/**
 * Returns the memory retained by `count` Parse calls whose results are all kept alive.
 *
 * @param string $value Text to parse.
 * @param string $pattern Pattern or pattern set.
 * @param int $count Number of calls: 1 or 2.
 */
function measureRetained(string $value, string $pattern, int $count): int
{
    gc_collect_cycles();
    $before = memory_get_usage();

    $first = parse($value, $pattern);
    $second = $count > 1 ? parse($value, $pattern) : [];

    $after = memory_get_usage();

    if ($first === [] || ($count > 1 && $second === [])) {
        throw new RuntimeException('The memory measurement did not capture any variable.');
    }

    return $after - $before;
}

/**
 * Returns the memory retained by one Parse call in bytes.
 *
 * The difference between the retained memory of two calls and of one call removes the
 * overhead of the measurement itself, so what is left is one result map.
 *
 * @param string $value Text to parse.
 * @param string $pattern Pattern or pattern set.
 * @param int $samples Number of memory samples.
 */
function measureAllocated(string $value, string $pattern, int $samples): int
{
    for ($index = 0; $index < 20; $index++) {
        parse($value, $pattern); // warm up the compiled pattern cache
    }

    $one = [];
    $two = [];
    for ($sample = 0; $sample < $samples; $sample++) {
        $one[] = measureRetained($value, $pattern, 1);
        $two[] = measureRetained($value, $pattern, 2);
    }

    return median($two) - median($one);
}

/**
 * @param int[] $values
 */
function median(array $values): int
{
    sort($values);

    return $values[intdiv(count($values), 2)];
}

function formatBytes(int $bytes): string
{
    return $bytes < 1024 ? $bytes . ' B' : sprintf('%.2f KB', $bytes / 1024);
}

/**
 * Describes the runtime and the processor of the machine that runs the benchmark.
 */
function describeEnvironment(): string
{
    $cpu = getenv('PROCESSOR_IDENTIFIER');
    $cpu = $cpu === false ? '' : trim($cpu);

    if ($cpu === '' && is_readable('/proc/cpuinfo')) {
        $info = file_get_contents('/proc/cpuinfo');
        if ($info !== false && preg_match('/^model name\s*:\s*(.+)$/m', $info, $matches) === 1) {
            $cpu = trim($matches[1]);
        }
    }

    return sprintf(
        'PHP %s (%s %s)%s',
        PHP_VERSION,
        php_uname('s'),
        php_uname('m'),
        $cpu === '' ? '' : ', ' . $cpu);
}

/**
 * @param string[] $arguments Command line arguments without the script name.
 *
 * @return array{iterations: int, repeat: int, samples: int, memory: bool}
 */
function parseOptions(array $arguments): array
{
    $options = ['iterations' => 20000, 'repeat' => 7, 'samples' => 100, 'memory' => true];

    for ($index = 0; $index < count($arguments); $index++) {
        $argument = $arguments[$index];

        switch ($argument) {
            case '-h':
            case '--help':
                echo USAGE, "\n";
                exit(0);

            case '--no-memory':
                $options['memory'] = false;
                break;

            case '-i':
            case '--iterations':
                $options['iterations'] = toPositiveInteger($arguments[++$index] ?? '', $argument);
                break;

            case '-r':
            case '--repeat':
                $options['repeat'] = toPositiveInteger($arguments[++$index] ?? '', $argument);
                break;

            case '-s':
            case '--samples':
                $options['samples'] = toPositiveInteger($arguments[++$index] ?? '', $argument);
                break;

            default:
                throw new InvalidArgumentException(sprintf("Unknown option '%s'.", $argument));
        }
    }

    return $options;
}

function toPositiveInteger(string $value, string $name): int
{
    if (preg_match('/\A[0-9]+\z/', $value) !== 1 || (int) $value <= 0) {
        throw new InvalidArgumentException(sprintf('%s must be a positive integer, got \'%s\'.', $name, $value));
    }

    return (int) $value;
}

function main(array $arguments): int
{
    try {
        $options = parseOptions($arguments);
    } catch (InvalidArgumentException $exception) {
        fwrite(STDERR, $exception->getMessage() . "\n\n" . USAGE . "\n");

        return 1;
    }

    echo describeEnvironment(), "\n\n";
    echo $options['memory'] ? "| Benchmark | Mean | Retained |\n" : "| Benchmark | Mean |\n";
    echo $options['memory'] ? "| --- | ---: | ---: |\n" : "| --- | ---: |\n";

    foreach (CASES as $scenario) {
        $nanoseconds = measure(
            $scenario['value'],
            $scenario['pattern'],
            $options['iterations'],
            $options['repeat']);
        $row = sprintf(
            '| `%s` - %s | %.1f ns |',
            $scenario['name'],
            $scenario['description'],
            $nanoseconds);

        if ($options['memory']) {
            $bytes = measureAllocated($scenario['value'], $scenario['pattern'], $options['samples']);
            $row .= sprintf(' %s |', formatBytes($bytes));
        }

        echo $row, "\n";
    }

    return 0;
}

exit(main(array_slice($argv, 1)));

