<?php

declare(strict_types=1);

/**
 * Tests of the PHP reference port (src/UFP.PHP/ufp.php).
 *
 * Run from the repository root:
 *
 *     php tests/UFP.PHP.Tests/ufp.test.php
 *
 * Like the Python and JavaScript suites of the other ports, this runner is dependency-free:
 * it needs PHP 8.0+ and nothing else (no PHPUnit, no composer).
 */

require_once __DIR__ . '/../../src/UFP.PHP/ufp.php';

use function UFP\find;
use function UFP\parse;
use function UFP\parseSingle;
use function UFP\rfindWithin;
use function UFP\tokenize;

const VECTORS_PATH = __DIR__ . '/../../spec/ufp-1.2.test-vectors.json';
const SPECIFICATION_VERSION = 'UFP-1.2';

/**
 * Counters of the tiny test runner: passed tests, failed tests, total tests and failures.
 *
 * @var array{passed: int, failed: int, total: int, failures: list<array{name: string, error: Throwable}>}
 */
$ufpTests = ['passed' => 0, 'failed' => 0, 'total' => 0, 'failures' => []];

/**
 * A failed assertion.
 */
final class TestFailure extends RuntimeException
{
}

/**
 * Runs one test and records its result.
 *
 * @param string $name What the test verifies.
 * @param callable(): void $body Assertions of the test.
 */
function test(string $name, callable $body): void
{
    global $ufpTests;

    $ufpTests['total']++;

    try {
        $body();
        $ufpTests['passed']++;
        printf("ok %d - %s\n", $ufpTests['total'], $name);
    } catch (Throwable $exception) {
        $ufpTests['failed']++;
        $ufpTests['failures'][] = ['name' => $name, 'error' => $exception];
        printf("not ok %d - %s\n", $ufpTests['total'], $name);
    }
}

/**
 * @param mixed $expected
 * @param mixed $actual
 */
function assertSame($expected, $actual, string $message = ''): void
{
    if ($expected !== $actual) {
        throw new TestFailure(sprintf(
            'Expected %s, got %s. %s',
            describe($expected),
            describe($actual),
            $message));
    }
}

/**
 * §11 Compares a result map with an expected map as a set of pairs, so the order of keys
 * does not matter.
 *
 * @param array<array-key, string> $expected
 * @param array<array-key, string> $actual
 */
function assertSameMap(array $expected, array $actual, string $message = ''): void
{
    if (count($expected) !== count($actual)) {
        throw new TestFailure(sprintf(
            'Expected %d variable(s) %s, got %d %s. %s',
            count($expected),
            describe($expected),
            count($actual),
            describe($actual),
            $message));
    }

    foreach ($expected as $key => $value) {
        if (!array_key_exists($key, $actual)) {
            throw new TestFailure(sprintf(
                "Variable '%s' was not found in %s. %s",
                (string) $key,
                describe($actual),
                $message));
        }

        if ($actual[$key] !== $value) {
            throw new TestFailure(sprintf(
                "Variable '%s' is %s, expected %s. %s",
                (string) $key,
                describe($actual[$key]),
                describe($value),
                $message));
        }
    }
}

function assertTrue(bool $condition, string $message = ''): void
{
    if (!$condition) {
        throw new TestFailure('Expected the condition to be true. ' . $message);
    }
}

/**
 * @param mixed $value
 */
function describe($value): string
{
    $json = json_encode($value, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);

    return $json === false ? var_export($value, true) : $json;
}

/**
 * @return array{specification: string, vectors: list<array{id: string, description: string,
 *     pattern: string, value: string, expected: array<array-key, string>}>}
 */
function loadVectors(): array
{
    $contents = file_get_contents(VECTORS_PATH);
    if ($contents === false) {
        throw new TestFailure('Cannot read the conformance suite: ' . VECTORS_PATH);
    }

    return json_decode($contents, true, 512, JSON_THROW_ON_ERROR);
}

// --- Conformance (§11) ---

test('suite belongs to the implemented format version', function (): void {
    assertSame(SPECIFICATION_VERSION, loadVectors()['specification']);
});

test('reproduces every vector', function (): void {
    foreach (loadVectors()['vectors'] as $vector) {
        assertSameMap($vector['expected'], parse($vector['value'], $vector['pattern']), $vector['id']);
    }
});

test('never throws', function (): void {
    foreach (loadVectors()['vectors'] as $vector) {
        assertTrue(is_array(parse($vector['value'], $vector['pattern'])), $vector['id']);
    }
});

/**
 * One expected tokenizer part, in the key order the port uses.
 *
 * @return array{text: string, isSeparator: bool, isGreedy: bool}
 */
function part(string $text, bool $isSeparator, bool $isGreedy): array
{
    return ['text' => $text, 'isSeparator' => $isSeparator, 'isGreedy' => $isGreedy];
}

// --- Primitives (§2.2) ---

test('find returns the first occurrence at or after the start', function (): void {
    assertSame(1, find('a_b_c', '_', 0));
    assertSame(3, find('a_b_c', '_', 2));
    assertSame(-1, find('a_b_c', '.', 0));
});

test('find accepts out-of-range start indices without an error', function (): void {
    assertSame(1, find('a_b_c', '_', -5));
    assertSame(-1, find('a_b_c', '_', 99));
});

test('rfindWithin bounds the end of the match', function (): void {
    // "XX" occurs at 1 and at 2; the match must end at or before maxEnd.
    assertSame(2, rfindWithin('aXXXb', 'XX', 3));
    assertSame(1, rfindWithin('aXXXb', 'XX', 2));
    assertSame(-1, rfindWithin('aXXXb', 'XX', 1));
    assertSame(-1, rfindWithin('aXXXb', 'XX', -1));
});

test('rfindWithin is not a start index search', function (): void {
    // A match that starts exactly at maxEnd but extends past it is not allowed.
    assertSame(0, rfindWithin('abab', 'ab', 2));
    assertSame(2, rfindWithin('abab', 'ab', 3));
});

test('rfindWithin returns -1 when the needle does not fit', function (): void {
    assertSame(-1, rfindWithin('abc', 'abc', 1));
    assertSame(-1, rfindWithin('', 'a', 0));
});

test('rfindWithin accepts a bound past the end of the value', function (): void {
    // "XX" occurs at 1 and at 2, so the last occurrence is 2 whatever the bound is.
    assertSame(2, rfindWithin('aXXXb', 'XX', 99));
});

// --- Tokenizer (§4) ---

test('splits variables and separators', function (): void {
    assertSame([
        part('Date', false, false),
        part('_', true, false),
        part('Name', false, false),
        part('.', true, false),
        part('Extension', false, false),
    ], tokenize('<Date>_<Name>.<Extension>'));
});

test('removes the greedy mark from the name', function (): void {
    assertSame([
        part('Path', false, true),
        part('/', true, false),
        part('Filename', false, false),
    ], tokenize('<Path*>/<Filename>'));
});

test('treats an empty name as a variable', function (): void {
    assertSame([
        part('', false, false),
        part('_', true, false),
        part('B', false, false),
    ], tokenize('<>_<B>'));
});

test('keeps adjacent variables as two parts', function (): void {
    assertSame([
        part('A', false, false),
        part('B', false, false),
    ], tokenize('<A><B>'));
});

test('trims the name before the mark is removed', function (): void {
    assertSame([part('A ', false, true)], tokenize('<A *>'));
});

test('does not treat a mark inside the name as greedy', function (): void {
    assertSame([part('A*B', false, false)], tokenize('<A*B>'));
});

test('turns an unclosed variable into a separator', function (): void {
    assertSame([
        part('Date', false, false),
        part('_', true, false),
        part('Name', true, false),
    ], tokenize('<Date>_<Name'));
});

test('drops an end char outside a variable', function (): void {
    assertSame([
        part('ab', true, false),
        part('C', false, false),
    ], tokenize('a>b<C>'));
});

test('drops a start char inside a variable', function (): void {
    assertSame([part('AB', false, false)], tokenize('<A<B>>'));
});

test('does not share the tokenize result between calls', function (): void {
    $first = tokenize('<A>');
    $first[0]['text'] = 'changed';
    $first[] = part('X', true, false);

    assertSame([part('A', false, false)], tokenize('<A>'));
});

test('does not treat a pipe as a reserved character', function (): void {
    // The tokenizer works on one alternative: '|' is an ordinary static character.
    assertSame([
        part('A', false, false),
        part('|', true, false),
        part('B', false, false),
    ], tokenize('<A>|<B>'));
});

test('trims Unicode whitespace around a name', function (): void {
    // §2.2 TRIM uses the Unicode White_Space set, not only ASCII whitespace.
    assertSame([part('A', false, false)], tokenize("<\u{00A0}A\u{2003}>"));
});

test('tolerates a name that is not valid UTF-8', function (): void {
    assertSame([part("\xFF", false, false)], tokenize("<\xFF>"));
});

// --- Matching (§5) ---

test('gives the rest of the value to a final variable', function (): void {
    assertSameMap(['Value' => 'any text here'], parse('any text here', '<Value>'));
    assertSameMap(['Value' => ''], parse('', '<Value>'));
});

test('returns an independent map each time', function (): void {
    $first = parse('x', '<A>');
    $first['A'] = 'changed';

    assertSameMap(['A' => 'x'], parse('x', '<A>'));
});

test('stops a non-greedy variable at the first occurrence', function (): void {
    assertSameMap(['A' => '1', 'B' => '2', 'C' => '3.4'], parse('1.2.3.4', '<A>.<B>.<C>'));
});

test('stops a greedy variable at the last occurrence', function (): void {
    assertSameMap(['A' => '1.2', 'B' => '3', 'C' => '4'], parse('1.2.3.4', '<A*>.<B>.<C>'));
});

test('keeps intermediate separators in a greedy variable', function (): void {
    assertSameMap(['A' => 'a_b', 'B' => 'c'], parse('a_b_c', '<A*>_<B>'));
    assertSameMap(['A' => 'a', 'B' => 'b_c'], parse('a_b_c', '<A>_<B*>'));
});

test('consumes a leading static part', function (): void {
    assertSameMap(['Number' => '1234'], parse('IMG_1234.jpg', 'IMG_<Number>.jpg'));
});

test('requires a leading static part', function (): void {
    assertSameMap([], parse('scan_99.jpg', 'IMG_<Number>.jpg'));
});

test('requires the leading static part to be a prefix', function (): void {
    assertSameMap([], parse('x_IMG_1.jpg', 'IMG_<Number>.jpg'));
});

test('does not shift a greedy variable by a leading static part', function (): void {
    assertSameMap(['A' => 'a_b', 'B' => 'c'], parse('_a_b_c', '_<A*>_<B>'));
});

test('consumes a trailing static part with the variable before it', function (): void {
    assertSameMap(['A' => 'x', 'B' => 'png'], parse('x.png.jpg', '<A>.<B>.jpg'));
});

test('returns the partial result when a trailing static part is missing', function (): void {
    assertSameMap(['A' => 'x'], parse('x.png', '<A>.<B>.jpg'));
});

test('allows empty segments', function (): void {
    assertSameMap(['A' => '', 'B' => ''], parse('_', '<A>_<B>'));
    assertSameMap(['A' => '', 'B' => 'x'], parse('_x', '<A>_<B>'));
    assertSameMap(['A' => 'x', 'B' => ''], parse('x_', '<A>_<B>'));
});

test('leaves no room for the chain when a separator starts the value', function (): void {
    assertSameMap([], parse('_x', '<A>_<B>_<C>'));
    assertSameMap([], parse('_x_y', '<A>_<B>_<C>_<D>'));
});

test('keeps the last value of a repeated name', function (): void {
    assertSameMap(['A' => '2'], parse('1_2', '<A>_<A>'));
});

test('is case-sensitive', function (): void {
    assertSameMap(['a' => 'Abc', 'B' => 'def'], parse('Abc_def', '<a>_<B>'));
});

test('does not split Unicode characters', function (): void {
    assertSameMap(
        ['A' => "\u{1F642}", 'B' => "\u{1F643}"],
        parse("\u{1F642}_\u{1F643}", '<A>_<B>'));
    assertSameMap(
        ['A' => 'привет', 'B' => 'мир'],
        parse('привет_мир', '<A>_<B>'));
    assertSameMap(['A' => 'x', 'B' => 'y'], parse("x\u{2026}y", "<A>\u{2026}<B>"));
});

test('returns the empty map for null arguments', function (): void {
    assertSameMap([], parse(null, '<A>'));
    assertSameMap([], parse('x', null));
    assertSameMap([], parse(null, null));
});

test('returns the empty map for an empty pattern', function (): void {
    assertSameMap([], parse('x', ''));
});

test('returns the empty map for a pattern without variables', function (): void {
    assertSameMap([], parse('abc', 'abc'));
});

test('does not split a pattern set in parseSingle', function (): void {
    // A single alternative treats '|' as an ordinary static character.
    assertSameMap(['A' => 'x', 'B' => 'y'], parseSingle('x|y', '<A>|<B>'));
});

test('keeps the compiled-pattern cache bounded without losing correctness', function (): void {
    for ($index = 0; $index < 300; $index++) {
        assertSameMap(['A' => 'x'], parse('x_' . $index, '<A>_' . $index));
    }
});

test('uses integer array keys for names that are canonical integers', function (): void {
    // PHP casts '0' and '1' to integer keys; the set of captured pairs is unchanged.
    assertSameMap([0 => 'a', 1 => 'b'], parse('a_b', '<0>_<1>'));
    assertSameMap(['01' => 'a'], parse('a', '<01>'));
});

test('never throws for ill-formed patterns', function (): void {
    $patterns = ['<', '>', '<>', '<A', 'A>', '<<A>>', '<A*', '<|>', '|<A>|', '||', '<A><B>', '<A*>_<B>_<C>'];

    foreach ($patterns as $pattern) {
        foreach (['', 'x', '_', "\xFF", 'x_y'] as $value) {
            assertTrue(is_array(parse($value, $pattern)), json_encode($pattern) . ' / ' . json_encode($value));
        }
    }
});

// --- Pattern sets (§6) ---

test('prefers the alternative that captured more variables', function (): void {
    assertSameMap(['A' => '1', 'B' => '2'], parse('1_2', '<A>_<B>|<A>'));
    assertSameMap(['A' => '1', 'B' => '2'], parse('1-2', '<A>_<B>|<A>-<B>'));
});

test('keeps the first alternative on a tie', function (): void {
    assertSameMap(['A' => 'x'], parse('x', '<A>|<B>'));
    assertSameMap(['A' => '2'], parse('1_2', '<A>_<A>|<A>'));
});

test('ignores empty alternatives', function (): void {
    assertSameMap(['A' => 'x'], parse('x', '|<A>|'));
    assertSameMap([], parse('x', '||'));
});

test('selects an alternative by its leading static part', function (): void {
    assertSameMap(
        ['Number' => '99'],
        parse('scan_99.jpg', 'IMG_<Number>.jpg|scan_<Number>.jpg'));
    assertSameMap([], parse('other_5.jpg', 'IMG_<Number>.jpg|scan_<Number>.jpg'));
});

test('reproduces the mega pattern of the README', function (): void {
    $megaPattern = '<Protocol>://<Domain>/<Page>?<FirstParameter.Key>=<FirstParameter.Value>'
        . '|<Date>_<Name>.<Extension>'
        . '|<StudyUid*>.<Type>.<Extension>';

    assertSameMap([
        'Protocol' => 'https',
        'Domain' => 'youtu.be',
        'Page' => 'KoFSQeOAYz4',
        'FirstParameter.Key' => 't',
        'FirstParameter.Value' => '10',
    ], parse('https://youtu.be/KoFSQeOAYz4?t=10', $megaPattern));

    assertSameMap([
        'Date' => '04-05-2024',
        'Name' => 'fh5_image',
        'Extension' => 'png',
    ], parse('04-05-2024_fh5_image.png', $megaPattern));

    assertSameMap([
        'StudyUid' => '1.2.840.52394.3.152.235.2.12.187636473',
        'Type' => 'patient',
        'Extension' => 'csv',
    ], parse('1.2.840.52394.3.152.235.2.12.187636473.patient.csv', $megaPattern));
});

// --- Summary ---

printf("\ntests: %d, passed: %d, failed: %d\n", $ufpTests['total'], $ufpTests['passed'], $ufpTests['failed']);

foreach ($ufpTests['failures'] as $failure) {
    printf("\nFAILED: %s\n%s\n", $failure['name'], (string) $failure['error']);
}

exit($ufpTests['failed'] === 0 ? 0 : 1);



