<?php

declare(strict_types=1);

// UFP Format (User Friendly Pattern)
// Format:         UFP-1.2
// Implementation: PHP 8.0+ (no dependencies)
// Repository:     https://github.com/Jagailo/User-Friendly-Pattern-Format
// Author:         Alexey Yagelo
// Version:        1.2.0 (2026-11-04)
// License:        MIT (see LICENSE)

namespace UFP;

require_once __DIR__ . '/ufp.php';

/**
 * Runner of the machine-readable conformance suite for the PHP port.
 *
 * Usage:
 *     php src/UFP.PHP/conformance.php                 # spec/ufp-1.2.test-vectors.json
 *     php src/UFP.PHP/conformance.php <vectors.json>  # a specific suite file
 */

/**
 * The conformance suite that ships with the repository.
 */
const DEFAULT_VECTORS_PATH = __DIR__ . '/../../spec/ufp-1.2.test-vectors.json';

/**
 * Runs every vector of the suite against parse().
 *
 * @param string|null $vectorsPath Path of the suite file; the bundled suite by default.
 *
 * @return int 0 when every vector is reproduced, 1 otherwise.
 */
function runConformanceSuite(?string $vectorsPath = null): int
{
    $path = $vectorsPath ?? DEFAULT_VECTORS_PATH;
    $contents = file_get_contents($path);
    if ($contents === false) {
        fwrite(STDERR, sprintf("Cannot read the conformance suite: %s\n", $path));

        return 1;
    }

    $document = json_decode($contents, true, 512, JSON_THROW_ON_ERROR);
    $failures = 0;

    foreach ($document['vectors'] as $vector) {
        $actual = parse($vector['value'], $vector['pattern']);
        if (!sameMap($vector['expected'], $actual)) {
            $failures++;
            printf(
                "FAIL %s: expected %s, got %s\n",
                $vector['id'],
                describeMap($vector['expected']),
                describeMap($actual));
        }
    }

    printf("vectors: %d, failures: %d\n", count($document['vectors']), $failures);

    return $failures === 0 ? 0 : 1;
}

/**
 * §11 Compares a result map with an expected map as a set of pairs: equal size, every key
 * present with the same value. The order of keys is irrelevant.
 *
 * @param array<array-key, string> $expected
 * @param array<array-key, string> $actual
 */
function sameMap(array $expected, array $actual): bool
{
    if (count($expected) !== count($actual)) {
        return false;
    }

    foreach ($expected as $key => $value) {
        if (!array_key_exists($key, $actual) || $actual[$key] !== $value) {
            return false;
        }
    }

    return true;
}

/**
 * @param array<array-key, string> $map
 */
function describeMap(array $map): string
{
    $json = json_encode($map, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);

    return $json === false ? var_export($map, true) : $json;
}

if (PHP_SAPI === 'cli' && isset($_SERVER['SCRIPT_FILENAME']) && realpath($_SERVER['SCRIPT_FILENAME']) === __FILE__) {
    exit(runConformanceSuite($argv[1] ?? null));
}
