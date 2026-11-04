// UFP Format (User Friendly Pattern)
// Format:         UFP-1.2
// Implementation: JavaScript (Node.js 20+, ES modules, no dependencies)
// Repository:     https://github.com/Jagailo/User-Friendly-Pattern-Format
// Author:         Alexey Yagelo
// Version:        1.2.0 (2026-11-04)
// License:        MIT (see LICENSE)

/**
 * Runner of the machine-readable conformance suite for the JavaScript port.
 *
 * Usage:
 *     node src/UFP.JavaScript/conformance.js                 # spec/ufp-1.2.test-vectors.json
 *     node src/UFP.JavaScript/conformance.js <vectors.json>  # a specific suite file
 */

import { readFileSync } from "node:fs";
import { fileURLToPath, pathToFileURL } from "node:url";

import { parse } from "./ufp.js";

/**
 * The conformance suite that ships with the repository.
 */
export const DEFAULT_VECTORS_PATH = fileURLToPath(
    new URL("../../spec/ufp-1.2.test-vectors.json", import.meta.url));

/**
 * Runs every vector of the suite against `parse`.
 *
 * @param {string} [vectorsPath] Path of the suite file.
 * @returns {number} `0` when every vector is reproduced, `1` otherwise.
 */
export function runConformanceSuite(vectorsPath = DEFAULT_VECTORS_PATH) {
    const document = JSON.parse(readFileSync(vectorsPath, "utf8"));
    let failures = 0;

    for (const vector of document.vectors) {
        const actual = parse(vector.value, vector.pattern);
        if (!sameMap(vector.expected, actual)) {
            failures++;
            console.log(`FAIL ${vector.id}: expected ${JSON.stringify(vector.expected)}, got ${JSON.stringify(actual)}`);
        }
    }

    console.log(`vectors: ${document.vectors.length}, failures: ${failures}`);
    return failures === 0 ? 0 : 1;
}

/**
 * Compares a result map with an expected map as a set of pairs (§11).
 *
 * @param {Record<string, string>} expected
 * @param {Record<string, string>} actual
 * @returns {boolean}
 */
function sameMap(expected, actual) {
    const expectedKeys = Object.keys(expected);
    if (expectedKeys.length !== Object.keys(actual).length) {
        return false;
    }

    for (const key of expectedKeys) {
        if (!Object.hasOwn(actual, key) || actual[key] !== expected[key]) {
            return false;
        }
    }

    return true;
}

const isMain = typeof process !== "undefined"
    && process.argv[1] !== undefined
    && import.meta.url === pathToFileURL(process.argv[1]).href;

if (isMain) {
    process.exitCode = runConformanceSuite(process.argv[2]);
}
