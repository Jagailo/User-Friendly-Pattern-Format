/**
 * Tests of the JavaScript reference port (src/UFP.JavaScript/ufp.js).
 *
 * Run from the repository root:
 *
 *     node --test tests/UFP.JavaScript.Tests
 *     npm test
 */

import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import { find, parse, parseSingle, rfindWithin, tokenize } from "../../src/UFP.JavaScript/ufp.js";

const VECTORS_PATH = new URL("../../spec/ufp-1.2.test-vectors.json", import.meta.url);
const SPECIFICATION_VERSION = "UFP-1.2";
const suite = JSON.parse(readFileSync(VECTORS_PATH, "utf8"));

/**
 * §11 Compare a result map with an expected map as a set of pairs: equal size, every key
 * present with the same value. The order of keys is irrelevant.
 *
 * @param {Record<string, string>} expected
 * @param {Record<string, string>} actual
 * @param {string} [message]
 */
function expectSameMap(expected, actual, message) {
    const expectedKeys = Object.keys(expected);
    assert.equal(Object.keys(actual).length, expectedKeys.length, message);

    for (const key of expectedKeys) {
        assert.ok(Object.hasOwn(actual, key), `Variable '${key}' was not found. ${message ?? ""}`);
        assert.equal(actual[key], expected[key], message);
    }
}

describe("Conformance", () => {
    it("suite belongs to the implemented format version", () => {
        assert.equal(suite.specification, SPECIFICATION_VERSION);
    });

    it("reproduces every vector", () => {
        for (const vector of suite.vectors) {
            expectSameMap(vector.expected, parse(vector.value, vector.pattern), vector.id);
        }
    });

    it("never throws", () => {
        for (const vector of suite.vectors) {
            assert.doesNotThrow(() => parse(vector.value, vector.pattern), vector.id);
        }
    });
});

describe("Primitives (§2.2)", () => {
    it("find returns the first occurrence at or after the start", () => {
        assert.equal(find("a_b_c", "_", 0), 1);
        assert.equal(find("a_b_c", "_", 2), 3);
        assert.equal(find("a_b_c", ".", 0), -1);
    });

    it("rfindWithin bounds the end of the match", () => {
        // "XX" occurs at 1 and at 2; the match must end at or before maxEnd.
        assert.equal(rfindWithin("aXXXb", "XX", 3), 2);
        assert.equal(rfindWithin("aXXXb", "XX", 2), 1);
        assert.equal(rfindWithin("aXXXb", "XX", 1), -1);
        assert.equal(rfindWithin("aXXXb", "XX", -1), -1);
    });

    it("rfindWithin is not a start index search", () => {
        // A match that starts exactly at maxEnd but extends past it is not allowed.
        assert.equal(rfindWithin("abab", "ab", 2), 0);
        assert.equal(rfindWithin("abab", "ab", 3), 2);
    });

    it("rfindWithin returns -1 when the needle does not fit", () => {
        assert.equal(rfindWithin("abc", "abc", 1), -1);
    });
});

describe("Tokenizer (§4)", () => {
    it("splits variables and separators", () => {
        assert.deepStrictEqual(tokenize("<Date>_<Name>.<Extension>"), [
            { text: "Date", isSeparator: false, isGreedy: false },
            { text: "_", isSeparator: true, isGreedy: false },
            { text: "Name", isSeparator: false, isGreedy: false },
            { text: ".", isSeparator: true, isGreedy: false },
            { text: "Extension", isSeparator: false, isGreedy: false },
        ]);
    });

    it("removes the greedy mark from the name", () => {
        assert.deepStrictEqual(tokenize("<Path*>/<Filename>"), [
            { text: "Path", isSeparator: false, isGreedy: true },
            { text: "/", isSeparator: true, isGreedy: false },
            { text: "Filename", isSeparator: false, isGreedy: false },
        ]);
    });

    it("treats an empty name as a variable", () => {
        assert.deepStrictEqual(tokenize("<>_<B>"), [
            { text: "", isSeparator: false, isGreedy: false },
            { text: "_", isSeparator: true, isGreedy: false },
            { text: "B", isSeparator: false, isGreedy: false },
        ]);
    });

    it("keeps adjacent variables as two parts", () => {
        assert.deepStrictEqual(tokenize("<A><B>"), [
            { text: "A", isSeparator: false, isGreedy: false },
            { text: "B", isSeparator: false, isGreedy: false },
        ]);
    });

    it("trims the name before the mark is removed", () => {
        assert.deepStrictEqual(tokenize("<A *>"), [
            { text: "A ", isSeparator: false, isGreedy: true },
        ]);
    });

    it("does not treat a mark inside the name as greedy", () => {
        assert.deepStrictEqual(tokenize("<A*B>"), [
            { text: "A*B", isSeparator: false, isGreedy: false },
        ]);
    });

    it("turns an unclosed variable into a separator", () => {
        assert.deepStrictEqual(tokenize("<Date>_<Name"), [
            { text: "Date", isSeparator: false, isGreedy: false },
            { text: "_", isSeparator: true, isGreedy: false },
            { text: "Name", isSeparator: true, isGreedy: false },
        ]);
    });

    it("drops an end char outside a variable", () => {
        assert.deepStrictEqual(tokenize("a>b<C>"), [
            { text: "ab", isSeparator: true, isGreedy: false },
            { text: "C", isSeparator: false, isGreedy: false },
        ]);
    });

    it("drops a start char inside a variable", () => {
        assert.deepStrictEqual(tokenize("<A<B>>"), [
            { text: "AB", isSeparator: false, isGreedy: false },
        ]);
    });

    it("does not share the tokenize result between calls", () => {
        const first = tokenize("<A>");
        first.push({ text: "X", isSeparator: true, isGreedy: false });
        first[0].text = "changed";

        assert.deepStrictEqual(tokenize("<A>"), [
            { text: "A", isSeparator: false, isGreedy: false },
        ]);
    });

    it("does not treat a pipe as a reserved character", () => {
        // The tokenizer works on one alternative: '|' is an ordinary static character.
        assert.deepStrictEqual(tokenize("<A>|<B>"), [
            { text: "A", isSeparator: false, isGreedy: false },
            { text: "|", isSeparator: true, isGreedy: false },
            { text: "B", isSeparator: false, isGreedy: false },
        ]);
    });
});

describe("Matching (§5)", () => {
    it("gives the rest of the value to a final variable", () => {
        assert.deepStrictEqual(parse("any text here", "<Value>"), { Value: "any text here" });
        assert.deepStrictEqual(parse("", "<Value>"), { Value: "" });
    });

    it("returns an independent map each time", () => {
        const first = parse("x", "<A>");
        first.A = "changed";
        assert.deepStrictEqual(parse("x", "<A>"), { A: "x" });
    });

    it("stops a non-greedy variable at the first occurrence", () => {
        assert.deepStrictEqual(parse("1.2.3.4", "<A>.<B>.<C>"), { A: "1", B: "2", C: "3.4" });
    });

    it("stops a greedy variable at the last occurrence", () => {
        assert.deepStrictEqual(parse("1.2.3.4", "<A*>.<B>.<C>"), { A: "1.2", B: "3", C: "4" });
    });

    it("keeps intermediate separators in a greedy variable", () => {
        assert.deepStrictEqual(parse("a_b_c", "<A*>_<B>"), { A: "a_b", B: "c" });
        assert.deepStrictEqual(parse("a_b_c", "<A>_<B*>"), { A: "a", B: "b_c" });
    });

    it("consumes a leading static part", () => {
        assert.deepStrictEqual(parse("IMG_1234.jpg", "IMG_<Number>.jpg"), { Number: "1234" });
    });

    it("requires a leading static part", () => {
        assert.deepStrictEqual(parse("scan_99.jpg", "IMG_<Number>.jpg"), {});
    });

    it("requires the leading static part to be a prefix", () => {
        assert.deepStrictEqual(parse("x_IMG_1.jpg", "IMG_<Number>.jpg"), {});
    });

    it("does not shift a greedy variable by a leading static part", () => {
        assert.deepStrictEqual(parse("_a_b_c", "_<A*>_<B>"), { A: "a_b", B: "c" });
    });

    it("consumes a trailing static part with the variable before it", () => {
        assert.deepStrictEqual(parse("x.png.jpg", "<A>.<B>.jpg"), { A: "x", B: "png" });
    });

    it("returns the partial result when a trailing static part is missing", () => {
        assert.deepStrictEqual(parse("x.png", "<A>.<B>.jpg"), { A: "x" });
    });

    it("allows empty segments", () => {
        assert.deepStrictEqual(parse("_", "<A>_<B>"), { A: "", B: "" });
        assert.deepStrictEqual(parse("_x", "<A>_<B>"), { A: "", B: "x" });
        assert.deepStrictEqual(parse("x_", "<A>_<B>"), { A: "x", B: "" });
    });

    it("leaves no room for the chain when a separator starts the value", () => {
        assert.deepStrictEqual(parse("_x", "<A>_<B>_<C>"), {});
        assert.deepStrictEqual(parse("_x_y", "<A>_<B>_<C>_<D>"), {});
    });

    it("keeps the last value of a repeated name", () => {
        assert.deepStrictEqual(parse("1_2", "<A>_<A>"), { A: "2" });
    });

    it("is case-sensitive", () => {
        assert.deepStrictEqual(parse("Abc_def", "<a>_<B>"), { a: "Abc", B: "def" });
    });

    it("does not split Unicode characters", () => {
        assert.deepStrictEqual(parse("\u{1F642}_\u{1F643}", "<A>_<B>"), { A: "\u{1F642}", B: "\u{1F643}" });
        assert.deepStrictEqual(parse("привет_мир", "<A>_<B>"), { A: "привет", B: "мир" });
    });

    it("returns the empty map for null or undefined arguments", () => {
        assert.deepStrictEqual(parse(null, "<A>"), {});
        assert.deepStrictEqual(parse("x", null), {});
        assert.deepStrictEqual(parse(undefined, "<A>"), {});
        assert.deepStrictEqual(parse("x", undefined), {});
    });

    it("returns the empty map for an empty pattern", () => {
        assert.deepStrictEqual(parse("x", ""), {});
    });

    it("returns the empty map for a pattern without variables", () => {
        assert.deepStrictEqual(parse("abc", "abc"), {});
    });

    it("does not split a pattern set in parseSingle", () => {
        // A single alternative treats '|' as an ordinary static character.
        assert.deepStrictEqual(parseSingle("x|y", "<A>|<B>"), { A: "x", B: "y" });
    });

    it("stores a variable named __proto__ as an own property", () => {
        const result = parse("x", "<__proto__>");

        assert.deepStrictEqual(Object.keys(result), ["__proto__"]);
        assert.equal(Object.getOwnPropertyDescriptor(result, "__proto__").value, "x");
        assert.equal(Object.getPrototypeOf(result), Object.prototype);
    });

    it("keeps the compiled-pattern cache bounded without losing correctness", () => {
        for (let index = 0; index < 300; index++) {
            assert.deepStrictEqual(parse(`x_${index}`, `<A>_${index}`), { A: "x" });
        }
    });
});

describe("Pattern sets (§6)", () => {
    it("prefers the alternative that captured more variables", () => {
        assert.deepStrictEqual(parse("1_2", "<A>_<B>|<A>"), { A: "1", B: "2" });
        assert.deepStrictEqual(parse("1-2", "<A>_<B>|<A>-<B>"), { A: "1", B: "2" });
    });

    it("keeps the first alternative on a tie", () => {
        assert.deepStrictEqual(parse("x", "<A>|<B>"), { A: "x" });
        assert.deepStrictEqual(parse("1_2", "<A>_<A>|<A>"), { A: "2" });
    });

    it("ignores empty alternatives", () => {
        assert.deepStrictEqual(parse("x", "|<A>|"), { A: "x" });
        assert.deepStrictEqual(parse("x", "||"), {});
    });

    it("selects an alternative by its leading static part", () => {
        assert.deepStrictEqual(
            parse("scan_99.jpg", "IMG_<Number>.jpg|scan_<Number>.jpg"),
            { Number: "99" });
        assert.deepStrictEqual(parse("other_5.jpg", "IMG_<Number>.jpg|scan_<Number>.jpg"), {});
    });

    it("reproduces the mega pattern of the README", () => {
        const megaPattern = "<Protocol>://<Domain>/<Page>?<FirstParameter.Key>=<FirstParameter.Value>"
            + "|<Date>_<Name>.<Extension>"
            + "|<StudyUid*>.<Type>.<Extension>";

        assert.deepStrictEqual(parse("https://youtu.be/KoFSQeOAYz4?t=10", megaPattern), {
            Protocol: "https",
            Domain: "youtu.be",
            Page: "KoFSQeOAYz4",
            "FirstParameter.Key": "t",
            "FirstParameter.Value": "10",
        });
        assert.deepStrictEqual(parse("04-05-2024_fh5_image.png", megaPattern), {
            Date: "04-05-2024",
            Name: "fh5_image",
            Extension: "png",
        });
        assert.deepStrictEqual(parse("1.2.840.52394.3.152.235.2.12.187636473.patient.csv", megaPattern), {
            StudyUid: "1.2.840.52394.3.152.235.2.12.187636473",
            Type: "patient",
            Extension: "csv",
        });
    });
});
