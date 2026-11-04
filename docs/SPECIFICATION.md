# UFP-1.2 — User Friendly Pattern Format

**Specification, version 1.2**

| | |
| --- | --- |
| Format version | `UFP-1.2` |
| Status | Stable, published, open for implementation |
| Published | 2026-11-04 |
| Author | Alexey Yagelo |
| Reference implementations | [`src/UFP.NET/UfpParser.cs`](../src/UFP.NET/UfpParser.cs) (UFP.NET 1.2.0, `net10.0`), [`src/UFP.NET.Standard/UfpParser.cs`](../src/UFP.NET.Standard/UfpParser.cs) (`netstandard2.0`), [`src/UFP.Python/ufp.py`](../src/UFP.Python/ufp.py) (Python 3), [`src/UFP.JavaScript/ufp.js`](../src/UFP.JavaScript/ufp.js) (JavaScript, Node.js 20+) |
| Conformance suite | [`spec/ufp-1.2.test-vectors.json`](../spec/ufp-1.2.test-vectors.json) |
| License | MIT — see [LICENSE](../LICENSE) |

This document is a normative specification of the UFP pattern format and of the
`Parse` operation that extracts named values from a text. It contains everything
needed to write a conforming parser in any programming language: the grammar, the
mathematical model of the primitive text operations, the complete algorithm in a
language-neutral pseudocode, the failure semantics, an edge-case catalogue and a
machine-readable conformance suite.

A Russian translation is available in [SPECIFICATION.ru.md](SPECIFICATION.ru.md).

---

## Table of contents

1. [Introduction](#1-introduction)
2. [Data model and primitive operations](#2-data-model-and-primitive-operations)
3. [Syntax](#3-syntax)
4. [Tokenization](#4-tokenization)
5. [Matching a single alternative](#5-matching-a-single-alternative)
6. [Pattern sets (mega patterns)](#6-pattern-sets-mega-patterns)
7. [Result model](#7-result-model)
8. [Conformance requirements](#8-conformance-requirements)
9. [Edge-case catalogue](#9-edge-case-catalogue)
10. [Implementation notes](#10-implementation-notes)
11. [Conformance suite](#11-conformance-suite)
12. [Versioning](#12-versioning)
13. [Open publication, licensing and patent statement](#13-open-publication-licensing-and-patent-statement)
- [Appendix A. Worked examples](#appendix-a-worked-examples)
- [Appendix B. Mapping to the reference implementation](#appendix-b-mapping-to-the-reference-implementation)
- [Appendix C. Changes in reference implementation 1.2.0](#appendix-c-changes-in-reference-implementation-120)

---

## 1. Introduction

### 1.1. What UFP is

UFP is a *reverse template*: a pattern describes the **shape of the text that is
being searched** — a sequence of static literals and named variable slots — and the
parser returns every variable slot as a key/value pair.

```text
value:   04-05-2024_scan.jpg
pattern: <Date>_<Name>.<Extension>
result:  Date = "04-05-2024", Name = "scan", Extension = "jpg"
```

A pattern consists of two kinds of parts:

* **static parts (separators)** — characters that never change and are used to find
  the boundaries between values;
* **dynamic parts (variables)** — named slots written in angle brackets; a variable
  may be marked as *greedy* with a trailing `*`, which makes it capture as much text
  as possible (`<Path*>/<Filename>.<Extension>`).

A **pattern set** ("mega pattern") lists several alternative patterns separated by
`|`; the parser picks the alternative that captured the most variables.

### 1.2. Why another pattern language

| | Regular expressions | Glob | Format strings / named templates | UFP |
| --- | --- | --- | --- | --- |
| Written by end users | Hard: quoting, escaping, groups | Easy, but no names | Easy, but no parsing rules | Easy, no escaping |
| Named extraction | Named groups with syntax noise | No | Yes | Yes — the name *is* the slot |
| Greedy / non-greedy | Explicit quantifiers, backtracking | Only `*` | Usually unspecified | One `*` on the variable |
| Multiple shapes per pattern | Alternation + anchors | No | No | Alternatives with `|` |
| Deterministic result | Depends on engine/backtracking | — | Usually unspecified | Yes — fully specified below |
| Failure mode | Can be catastrophic (ReDoS) | — | — | Total, linear-time scans |

Design principles:

1. **No escaping.** Any character except `<`, `>`, `|` is a literal.
2. **Names instead of groups.** The pattern is also the schema.
3. **Reverse matching, not validation.** The parser extracts, it does not validate
   the content of the variables.
4. **Total function.** Every (value, pattern) pair yields a result map; a conforming
   implementation never throws.
5. **No backtracking.** The algorithm locates boundaries with ordinary substring
   searches, so the cost is predictable.
6. **Deterministic.** For equal inputs every conforming implementation returns the
   same map.

### 1.3. Scope and non-goals

In scope: the syntax of patterns, the parsing algorithm, the result map, failure
semantics, alternatives, greedy variables, conformance testing.

Out of scope: type conversion of the extracted strings, validation of their content
(dates, numbers, e-mail addresses), Unicode normalisation, localisation, formatting
(values *into* text), streaming/incremental parsing, and the design of any
serialisation of the result map.

### 1.4. Conventions and terminology

The key words **MUST**, **MUST NOT**, **SHOULD**, **SHOULD NOT**, **MAY** are to be
interpreted as described in RFC 2119.

| Term | Meaning |
| --- | --- |
| **value** | The text that is searched, e.g. `04-05-2024_scan.jpg` |
| **pattern** | One alternative, e.g. `<Date>_<Name>.<Extension>` |
| **pattern set** | A pattern list separated by `|`, e.g. `<A>_<B>\|<A>-<B>` |
| **part** | One token of a pattern: a separator or a variable |
| **separator** (static part) | A literal that must appear in the value; it is consumed |
| **variable** (dynamic part) | A named slot `⟨name⟩`, optionally greedy |
| **greedy variable** | A variable whose name ends with `*`; it captures up to the *last* possible occurrence of the following separator |
| **alternative** | One pattern of a pattern set |
| **result map** | The extracted `name → text` pairs |

## 2. Data model and primitive operations

### 2.1. Strings and characters

A **value** and a **pattern** are finite sequences of characters. A *character* is
one element of the host language's string type: a Unicode code point in languages
with code-point indexing (Python, Go, Rust `char`, Swift `Character`), a UTF-16 code
unit in languages with UTF-16 indexing (.NET, Java, JavaScript). See §10.1 — the
choice does not change the result.

All comparisons are **ordinal and case-sensitive**: two characters are equal only if
they are the same character, without case folding, without Unicode normalisation and
without trimming. A value is never modified by `Parse`.

### 2.2. Primitive operations

The algorithm in §4 and §5 is expressed with the following primitives. They are
defined mathematically so that the specification does not depend on any standard
library. `LENGTH(s)` is the number of characters of `s`, indices are zero-based.

| Primitive | Result |
| --- | --- |
| `LENGTH(s)` | Number of characters in `s`. |
| `SUBSTRING(s, from, to)` | The characters at indices `from … to − 1`. `SUBSTRING(s, 5, 5)` is the empty string. Requires `0 ≤ from ≤ to ≤ LENGTH(s)`. |
| `CONCAT(a, b)` | The characters of `a` followed by the characters of `b`. |
| `FIND(s, needle, start)` | The smallest index `p ≥ start` such that `SUBSTRING(s, p, p + LENGTH(needle)) = needle`; `−1` if there is none. |
| `RFIND_WITHIN(s, needle, maxEnd)` | The largest index `p ≥ 0` such that `SUBSTRING(s, p, p + LENGTH(needle)) = needle` **and** `p + LENGTH(needle) − 1 ≤ maxEnd`; `−1` if there is none or if `maxEnd < 0`. |
| `TRIM(s)` | `s` without leading and trailing whitespace characters (Unicode `White_Space`). |
| `STARTS_WITH(s, prefix)` | `TRUE` if the first `LENGTH(prefix)` characters of `s` are `prefix`. |
| `ENDS_WITH(s, suffix)` | `TRUE` if `s` ends with `suffix` (in particular `TRUE` for `s = "*"`). |
| `DROP_LAST(s)` | `s` without its last character; requires `LENGTH(s) > 0`. |
| `SPLIT(s, ch)` | The list of maximal substrings of `s` that do not contain `ch` (empty substrings included). |

> **Note on `RFIND_WITHIN`.** This is the only non-obvious primitive. The upper bound
> is the index of the last character that may be part of the match, not the index
> where the match may start: a match that starts at `maxEnd` is *not* allowed unless
> it is one character long. In many languages this is
> `RFIND_WITHIN(s, needle, maxEnd) = s.rfind(needle, 0, maxEnd + 1)` (Python),
> `s.LastIndexOf(needle, maxEnd)` (.NET), `s.lastIndexOf(needle, maxEnd)` (Java,
> JavaScript). Implementations that only have an unbounded reverse search MUST verify
> the bound explicitly, for example by searching in `SUBSTRING(s, 0, maxEnd + 1)`.

## 3. Syntax

### 3.1. Reserved characters

Exactly four characters have a special meaning anywhere in a pattern:

| Character | Meaning |
| --- | --- |
| `<` | Starts a variable. Inside a variable it is ignored (dropped). |
| `>` | Ends a variable. Outside a variable it is ignored (dropped). |
| `|` | Separates the alternatives of a pattern set. Cannot occur inside a pattern. |
| `*` | Immediately before the closing `>` it marks the variable as greedy. Anywhere else it is an ordinary character of the name. |

Every other character — letters, digits, spaces, punctuation, emoji, any Unicode
character — is an ordinary literal and needs no escaping.

### 3.2. Well-formed patterns

```ebnf
pattern-set = alternative, { "|", alternative } ;
alternative = { element } ;
element     = variable | static ;
variable    = "<", { name-char }, [ "*" ], ">" ;
name-char   = ? any character except ">" ? ;          (* "<" is dropped, see §4 *)
static      = static-char, { static-char } ;
static-char = ? any character except "<", ">", "|" ? ;
```

Additional rules:

1. `name-char` excludes `>`, because `>` terminates the variable.
2. A `*` is a greedy mark **only** if it is the last character of the variable
   content *after* `TRIM`. `<A*>` is greedy and named `A`; `<A*B>` is not greedy and
   is named `A*B`; `<A *>` is greedy and is named `A ` — the space is kept, because
   trimming happens before the `*` is removed.
3. A variable name MAY be empty (`<>`). An empty name is stored in the result map
   under the key `""` and takes part in matching like any other name.
4. A variable name is `TRIM`med: `<Date >` and `< Name>` are the same as `<Date>` and
   `<Name>`.
5. `TRIM` affects only the name; the extracted text is never trimmed.
6. Static parts are taken literally, including spaces.

### 3.3. Ill-formed patterns

Tokenization (§4) is a **total function**: it accepts every character sequence and
never fails. Therefore the format has no "invalid pattern" error. The following
constructs are ill-formed according to §3.2 but still have a defined meaning:

| Construct | Meaning |
| --- | --- |
| `abc` (no variables at all) | The result is always the empty map. |
| `<Date>_<Name` (unclosed `<`) | Everything after the unclosed `<` becomes a static part; the `<` itself is dropped. |
| `a>b<C>` (`>` outside a variable) | The `>` is dropped; the pattern behaves like `ab<C>`. |
| `<A<B>>` (`<` inside a variable) | The inner `<` is dropped; the variable is named `AB`. |
| `|<A>\|\|` (empty alternatives) | Empty alternatives are ignored (§6). |
| `<A><B>` (two adjacent variables) | The first variable is ignored; the last one captures the rest of the value. |

Implementations MUST handle these constructs exactly as described; they MUST NOT
report an error. Authors SHOULD NOT write them.

## 4. Tokenization

Tokenization turns one alternative (a pattern without `|`) into an ordered list of
parts. A part is a record with three fields:

| Field | Type | Meaning |
| --- | --- | --- |
| `text` | string | For a separator — the literal to search for. For a variable — its name (may be empty). |
| `kind` | `Separator` / `Variable` | Which kind of part this is. |
| `isGreedy` | boolean | `TRUE` only for variables whose name was followed by `*`. Always `FALSE` for separators. |

### 4.1. Algorithm

```text
FUNCTION Tokenize(pattern) RETURNS List<Part>
    parts   ← empty list
    inVar   ← FALSE
    buffer  ← ""

    FOR EACH ch IN pattern DO
        IF ch = "<" THEN
            IF NOT inVar THEN
                inVar ← TRUE
                IF LENGTH(buffer) > 0 THEN
                    AppendSeparator(parts, buffer)
                    buffer ← ""
                END IF
            END IF
        ELSE IF ch = ">" THEN
            IF inVar THEN
                inVar ← FALSE
                name   ← TRIM(buffer)
                greedy ← FALSE
                IF ENDS_WITH(name, "*") THEN
                    name   ← DROP_LAST(name)
                    greedy ← TRUE
                END IF
                AppendVariable(parts, name, greedy)
                buffer ← ""
            END IF
        ELSE
            buffer ← CONCAT(buffer, ch)
        END IF
    END FOR

    IF LENGTH(buffer) > 0 THEN
        AppendSeparator(parts, buffer)
    END IF

    RETURN parts
END FUNCTION
```

### 4.2. Properties

* Every part is non-empty as text **except a variable with an empty name**; separators
  are never empty, because a buffer is emitted only when it is non-empty.
* Two separators never follow each other unless the pattern contains something the
  tokenizer drops between them, and two variables never follow each other unless the
  pattern contains them adjacently (see §3.3).
* The tokenizer never looks at the value; it is a pure syntax function.
* Tokenization is **not** part of the hot loop of a typical application: a
  pattern set can be tokenized once and reused for any number of values.

### 4.3. Examples

| Pattern | Parts (`kind: text`) |
| --- | --- |
| `<Date>_<Name>.<Extension>` | `V: Date`, `S: _`, `V: Name`, `S: .`, `V: Extension` |
| `<Path*>/<Filename>` | `V: Path (greedy)`, `S: /`, `V: Filename` |
| `pre/<A>` | `S: pre/`, `V: A` |
| `<A>_` | `V: A`, `S: _` |
| `<>_<B>` | `V: ""`, `S: _`, `V: B` |
| `<A><B>` | `V: A`, `V: B` |
| `<A *>` | `V: "A " (greedy)` |
| `<A*B>` | `V: "A*B"` |
| `<Date>_<Name` | `V: Date`, `S: _`, `S: Name` |
| `a>b<C>` | `S: ab`, `V: C` |
| `<A<B>>` | `V: AB` |

## 5. Matching a single alternative

### 5.1. Model

Let `P = p₀ … pₙ₋₁` be the parts of one alternative and `S = s₀ … sₘ₋₁` the
separators among them, in pattern order. Let `V` be the value.

An **occurrence** of a separator `x` is an index `p` with
`SUBSTRING(V, p, p + LENGTH(x)) = x`.

A variable `pᵢ` is **terminated by** the part `pᵢ₊₁` if that part is a separator, or is
the **final** variable if no part follows it. A variable that is followed by another
variable is **ignored** (§5.3).

If the pattern starts with a static part, that part is a **prefix requirement**: the value
MUST start with it, and the part is consumed before the first variable is cut (§5.3). A
value that does not start with it does not match the alternative at all — without this
rule the leading literal would silently leak into the first captured value
(`IMG_<Number>.jpg` would capture `Number = "IMG_1234"` for `IMG_1234.jpg` and would even
"match" `scan_99.jpg`). Every other static part is the terminator of the variable in front
of it, so it is required and consumed by that variable.

Matching is a two-step algorithm:

1. **Locate the separators from right to left.** The separators are placed as far to
   the right as possible, each one strictly to the left of the occurrence chosen for
   the separator to its right. This makes greedy variables well-defined: their end
   position is already known before the value is cut.
2. **Cut the value from left to right.** Every variable takes the text between the
   cursor and its terminator: the *first* occurrence of the terminator at or after the
   cursor for a non-greedy variable, or the occurrence found in step 1 for a greedy
   variable. The terminator itself is consumed.

### 5.2. Step 1 — locate the separators (right to left)

```text
FUNCTION LocateSeparators(value, separators) RETURNS Array<Integer>
    positions ← array of LENGTH(separators) items, all −1
    bound     ← LENGTH(value) − 1

    FOR k ← LENGTH(separators) − 1 DOWN TO 0 DO
        positions[k] ← RFIND_WITHIN(value, separators[k], bound)
        IF positions[k] ≥ 0 THEN
            bound ← positions[k] − 1
        END IF
    END FOR

    RETURN positions
END FUNCTION
```

Consequences:

* `positions[k] ≥ 0` means "the k-th separator occurs in the value, fully at or before
  one character before the (k+1)-th separator" — the chosen occurrences never overlap.
* If a separator does not occur within its bound, its position is `−1` and the bound
  stays unchanged, so the separators to its left are searched in the same prefix and
  may still be found.
* The bound is the index of the **last character** that may be part of the match, which
  is why `RFIND_WITHIN` is defined with `maxEnd` instead of a start index (§2.2).
* A leading static part is not searched for with `RFIND_WITHIN`: it is verified as a
  prefix of the value and consumed (§5.1), and its entry in `positions` is unused.
* When `bound` becomes negative (a separator occupies the beginning of the value and
  there are separators to its left), no occurrence can be found: the position is `−1`.
  This case MUST NOT raise an error.

### 5.3. Step 2 — cut the value (left to right)

```text
FUNCTION ParseSingle(value, pattern) RETURNS Map<String, String>
    results    ← empty map
    parts      ← Tokenize(pattern)
    separators ← [ part.text FOR part IN parts WHERE part.kind = Separator ]   (in order)
    positions  ← LocateSeparators(value, separators)

    segmentStart ← 0

    IF LENGTH(parts) > 0 AND parts[0].kind = Separator THEN
        IF NOT STARTS_WITH(value, parts[0].text) THEN
            RETURN results                                   # the leading static part does not match
        END IF
        segmentStart ← LENGTH(parts[0].text)                  # the leading static part is consumed
    END IF

    separatorsSeen ← 0

    FOR i ← 0 TO LENGTH(parts) − 1 DO
        part ← parts[i]

        IF part.kind = Separator THEN
            separatorsSeen ← separatorsSeen + 1
            CONTINUE
        END IF

        IF i = LENGTH(parts) − 1 THEN                      # final variable
            results[part.text] ← SUBSTRING(value, segmentStart, LENGTH(value))
            CONTINUE
        END IF

        IF parts[i + 1].kind = Variable THEN               # adjacent variables
            CONTINUE                                       # the first one is ignored
        END IF

        terminator ← parts[i + 1].text

        IF positions[separatorsSeen] = −1 THEN
            RETURN results                                 # failure, see 5.4
        END IF

        IF part.isGreedy THEN
            cut ← positions[separatorsSeen]
        ELSE
            cut ← FIND(value, terminator, segmentStart)
            IF cut = −1 THEN
                RETURN results                             # failure, see 5.4
            END IF
        END IF

        IF cut < segmentStart THEN
            RETURN results                                 # safety net, see 5.4
        END IF

        results[part.text] ← SUBSTRING(value, segmentStart, cut)
        segmentStart      ← cut + LENGTH(terminator)
    END FOR

    RETURN results
END FUNCTION
```

Notes:

* The leading static part is verified and consumed before the loop starts. The loop still
  counts it in `separatorsSeen` when it reaches it, so a variable that follows the leading
  part is terminated by the *next* separator (`positions[1]`), not by the leading one. The
  position of the leading part itself (`positions[0]`) is never used.
* `separatorsSeen` is the number of separators that appear **before** the current
  variable, which is exactly the index in `positions` of the separator that
  **terminates** the current variable. It MUST be derived from the parts, not from the
  number of already extracted variables (see Appendix C).
* A variable that is neither final nor terminated by a separator is ignored: it
  produces no entry and does not move the cursor.
* The terminator is consumed: `segmentStart` jumps behind it.
* The final variable always captures the rest of the value, including text that looks
  like a static part of the pattern. Only variables terminated by a separator have
  their terminator removed.

### 5.4. Failure semantics

Parsing does **not** have a single success/failure result. Every variable extracted
before the first failure is kept, so the result of a partial match is always a prefix
of the variables of the pattern:

| Situation | Result |
| --- | --- |
| The value does not start with the leading static part (§5.1) | The empty map: nothing is extracted. |
| The separator terminating variable `v` was not located in step 1 | Stop. Return the variables extracted *before* `v`. |
| The terminator of a non-greedy variable does not occur at or after the cursor | Stop. Return the variables extracted *before* `v`. |
| The located cut position is before the cursor (defensive rule; cannot happen for a value that is consistent with step 1) | Stop. Return the variables extracted *before* `v`. |
| No variable was extracted yet | The empty map. |

Because a pattern set (§6) compares alternatives by the number of captured variables,
partial results are meaningful: a less specific alternative wins only when the more
specific one really captured fewer variables.

An empty result map therefore means "the value does not have the shape described by the
pattern". A non-empty result map does not guarantee that the whole pattern matched.

## 6. Pattern sets (mega patterns)

### 6.1. Splitting

A pattern set is split on every `|` character. **Empty pieces are dropped**, so
`<A>|<B>`, `|<A>|`, `||<A>||` all produce the same list of alternatives, and a set that
consists only of `|` characters produces no alternatives at all.

```text
FUNCTION SplitPatternSet(patternSet) RETURNS List<String>
    pieces       ← SPLIT(patternSet, "|")
    alternatives ← empty list
    FOR EACH piece IN pieces DO
        IF LENGTH(piece) > 0 THEN
            APPEND(alternatives, piece)
        END IF
    END FOR
    RETURN alternatives
END FUNCTION
```

### 6.2. Selection

Every alternative is matched independently; the result of the alternative that
captured the **greatest number of variables** is returned. The number of captured
variables is the size of the result map, so a repeated variable name counts once.

```text
FUNCTION Parse(value, patternSet) RETURNS Map<String, String>
    IF value = NULL OR patternSet = NULL THEN
        RETURN empty map
    END IF

    alternatives ← SplitPatternSet(patternSet)
    IF LENGTH(alternatives) = 0 THEN
        RETURN empty map
    END IF
    IF LENGTH(alternatives) = 1 THEN
        RETURN ParseSingle(value, alternatives[0])
    END IF

    best ← empty map
    FOR EACH alternative IN alternatives DO        # strictly left to right
        current ← ParseSingle(value, alternative)
        IF SIZE(current) > SIZE(best) THEN         # strictly greater: ties keep the first
            best ← current
        END IF
    END FOR

    RETURN best
END FUNCTION
```

Notes:

* **Ties keep the first alternative**, so the order of alternatives is significant and
  a pattern set is deterministic even when several alternatives match equally well.
* `SIZE` is the number of entries of the result map (the number of distinct captured
  names), not the number of variables in the pattern.
* An alternative that captures nothing yields the empty map, which loses against every
  alternative that captured at least one variable.
* The remaining checks (`NULL` arguments, empty pattern set) are part of the specified
  behaviour, not implementation details.

## 7. Result model

* The result is a **map from variable name to extracted text**. Both are strings.
* Keys are the variable names exactly as defined in §3.2 (trimmed, without the greedy
  mark). A name may be the empty string.
* If the same name occurs more than once in the pattern, **the last assignment wins**
  (`results[name] ← text` is an assignment, not an insert).
* Only variables are returned. Static parts and the number of alternatives are never
  reflected in the result.
* The order of the map entries is not part of this specification; consumers MUST NOT
  rely on it.
* No conversion is performed: `"10"` stays `"10"`, not `10`; `""` is a captured empty
  string, not "absent". A missing key is the only way to express "not captured".
* The empty map is the result of "nothing captured" and is used both for a value that
  does not match and for a pattern without variables.

## 8. Conformance requirements

An implementation is **UFP-1.2 conformant** if it satisfies all of the following.

### 8.1. MUST

1. `Parse(value, patternSet)` MUST return a map for **every** pair of inputs; it MUST
   NOT throw, abort, warn or return `null`/`nil` for any value or pattern, including
   ill-formed patterns, empty strings and separators at the very beginning or end of
   the value.
2. The result MUST equal the result of the pseudocode in §4–§6 for all inputs.
3. Comparisons MUST be ordinal and case-sensitive, with no Unicode normalisation and no
   case folding (§2.1).
4. The value MUST NOT be modified, trimmed or normalised.
5. The result MUST be deterministic: the same inputs always yield the same map.
6. A repeated variable name MUST overwrite the previous value (§7).
7. Alternatives MUST be evaluated in source order and ties MUST keep the first
   alternative (§6.2).
8. `null`/`nil` arguments MUST produce the empty map (§6.2). Languages without null
   values may omit this rule.
9. Partial results MUST be returned as described in §5.4.
10. A pattern that starts with a static part MUST only match a value that starts with that
    part, and the part MUST NOT appear in any captured value (§5.1).

### 8.2. SHOULD

1. Tokenize the pattern once and reuse the parts for many values (§4.2).
2. Keep the total cost at `O(|value| · |pattern|)` in the worst case (§10.2).
3. Return an ordered map only as a convenience; do not document the order as stable.
4. Provide a way to detect a partial match if the host language allows it (for example a
   second method that reports the number of unmatched parts) — but `Parse` itself MUST
   keep the specified signature/semantics.

### 8.3. MAY

1. Add typed accessors (`ParseInt`, `ParseDate`, …) built on top of `Parse`.
2. Cache tokenized patterns keyed by pattern string.
3. Add an option that treats unterminated variables as errors — as long as the default
   behaviour remains as specified.
4. Reject non-conformant patterns only through an opt-in strict API.

### 8.4. Security considerations

There is no backtracking, no recursion and no evaluation of the pattern as code, so the
format cannot be exploited for code execution. The cost is bounded by the number of
`FIND`/`RFIND_WITHIN` calls: `O(number of separators)` reverse scans plus one forward
scan per non-greedy variable, each linear in the value length. A pattern set multiplies
this by the number of alternatives, so a hostile pattern with thousands of alternatives
still costs linearly more work — the same as any other input-proportional parsing. There
is no analogue of catastrophic backtracking (ReDoS).

## 9. Edge-case catalogue

Every row is also a part of the conformance suite (§11), with the same `id`.

### 9.1. Variables, names and greedy marks

| Pattern | Value | Result | Why |
| --- | --- | --- | --- |
| `<Value>` | `any text here` | `Value = "any text here"` | A final variable takes the rest of the value. |
| `<Value>` | `` (empty) | `Value = ""` | A final variable may capture an empty string. |
| `<>` | `abc` | `"" = "abc"` | An empty name is legal. |
| `<>_<>` | `a_b` | `"" = "b"` | A repeated name keeps the last value. |
| `<Date >_< Name>` | `04-05-2024_scan` | `Date = "04-05-2024"`, `Name = "scan"` | Names are trimmed. |
| `<A><B>` | `ab` | `B = "ab"` | An adjacent variable is ignored. |
| `<A*B>_<C>` | `1_2` | `A*B = "1"`, `C = "2"` | Only a trailing `*` marks greediness. |
| `<**>_<B>` | `1_2` | `* = "1"`, `B = "2"` | One trailing `*` is removed. |
| `<A *>_<B>` | `1_2` | `A  = "1"`, `B = "2"` | The name is trimmed before `*` is removed, so the space stays. |

### 9.2. Greedy and non-greedy cuts

| Pattern | Value | Result | Why |
| --- | --- | --- | --- |
| `<A*>.<B>.<C>` | `1.2.3.4` | `A = "1.2"`, `B = "3"`, `C = "4"` | Greedy takes the last possible occurrence. |
| `<A>.<B>.<C>` | `1.2.3.4` | `A = "1"`, `B = "2"`, `C = "3.4"` | Non-greedy takes the first occurrence. |
| `<A*>_<B>` | `a_b_c` | `A = "a_b"`, `B = "c"` | Greedy keeps the inner separator. |
| `<A>_<B*>` | `a_b_c` | `A = "a"`, `B = "b_c"` | Greedy at the end. |
| `<A*>_<B*>_<C>` | `a_b_c_d` | `A = "a_b"`, `B = "c"`, `C = "d"` | Both cuts are bounded by the next separator. |
| `<A*>.<B>.<C>` | `1..2..3` | `A = "1..2"`, `B = ""`, `C = "3"` | Empty segments are allowed. |
| `<A*>ab<B>` | `xabab` | `A = "xab"`, `B = ""` | The occurrence used by `B` stays to the left of `A`'s bound. |
| `<A>ab<B>` | `xabab` | `A = "x"`, `B = "ab"` | Forward scan from the cursor. |
| `<A*>aa<B>` | `xaaa` | `A = "xa"`, `B = ""` | Overlapping occurrences of a two-character separator. |
| `_<A*>_<B>` | `_a_b_c` | `A = "a_b"`, `B = "c"` | A leading static part is consumed and does not shift the terminator of a greedy variable. |

### 9.3. Static parts: prefix requirement and anchors

| Pattern | Value | Result | Why |
| --- | --- | --- | --- |
| `pre_<A>` | `pre_abc` | `A = "abc"` | The leading static part is required and consumed. |
| `pre_<A>` | `zzz_abc` | *(empty)* | The leading static part must be a prefix of the value. |
| `IMG_<Number>.jpg` | `IMG_1234.jpg` | `Number = "1234"` | The leading part does not leak into the captured value. |
| `IMG_<Number>.jpg` | `scan_99.jpg` | *(empty)* | A value with a different prefix is not captured at all — which is what makes the pattern usable as a filter, also inside a pattern set. |
| `<A>_<B>_` | `1_2` | *(empty)* | The trailing static part must be findable, and here it is not. |
| `<A>.<B>.jpg` | `x.png.jpg` | `A = "x"`, `B = "png"` | A trailing static part anchors `B` and is consumed by it. |
| `<A>.<B>.jpg` | `x.png` | `A = "x"` | Partial result: `.jpg` is missing. |
| `<A>.jpg` | `x.png` | *(empty)* | The first variable already fails. |
| ` <A> _ <B> ` | ` x _ y ` | `A = "x"`, `B = "y"` | Spaces are ordinary static characters; the captured text is not trimmed. |

### 9.4. Ordering, empty segments and boundaries

| Pattern | Value | Result | Why |
| --- | --- | --- | --- |
| `<A>_<B>` | `_` | `A = ""`, `B = ""` | Both segments may be empty. |
| `<A>_<B>` | `_x` | `A = ""`, `B = "x"` | The value starts with the separator. |
| `<A>_<B>` | `x_` | `A = "x"`, `B = ""` | The value ends with the separator. |
| `<A>_<B>` | `no separators at all` | *(empty)* | The terminator does not exist. |
| `<A>_<B>.<C>` | `a.b_c` | *(empty)* | Separators must appear in the value in pattern order. |
| `<A>_<B>_<C>` | `_x` | *(empty)* | A separator at index 0 leaves no room for the separators to its left. |
| `<A>_<B>_<C>_<D>` | `_x_y` | *(empty)* | Same rule, one level deeper. |
| `<A>.<B>.<C>` | `.x.y` | `A = ""`, `B = "x"`, `C = "y"` | The chain still fits, so the empty first segment is fine. |
| `<A>_` | `x_` | `A = "x"` | A trailing separator that terminates a variable is consumed. |
| `<A>_` | `x` | *(empty)* | ... and must exist. |

### 9.5. Pattern sets

| Pattern set | Value | Result | Why |
| --- | --- | --- | --- |
| `<A>\|<B>` | `x` | `A = "x"` | Tie: the first alternative wins. |
| `<A>_<B>\|<A>` | `1_2` | `A = "1"`, `B = "2"` | More captured variables win. |
| `<A>_<B>\|<A>-<B>` | `1-2` | `A = "1"`, `B = "2"` | The first alternative captures nothing, the second captures two. |
| `<A>_<A>\|<A>` | `1_2` | `A = "2"` | A repeated name counts once, so both capture one variable; the first wins. |
| `\|<A>\|` | `x` | `A = "x"` | Empty alternatives are dropped. |
| `\|\|` | `x` | *(empty)* | No alternatives at all. |
| `` (empty) | `x` | *(empty)* | No alternatives at all. |

### 9.6. Unicode and case

| Pattern | Value | Result | Why |
| --- | --- | --- | --- |
| `<a>_<B>` | `Abc_def` | `a = "Abc"`, `B = "def"` | Names are case-sensitive. |
| `<A>_<B>` | `Abc_def` | `A = "Abc"`, `B = "def"` | No case-insensitive matching of separators. |
| `<A>→<B>` | `x→y` | `A = "x"`, `B = "y"` | Any character may be a separator. |
| `<A>_<B>` | `привет_мир` | `A = "привет"`, `B = "мир"` | Cyrillic is ordinary text. |
| `<A>_<B>` | `🙂_🙃` | `A = "🙂"`, `B = "🙃"` | Characters outside the BMP are kept intact. |
| `<A>…<B>` | `x…y` | `A = "x"`, `B = "y"` | Multi-byte separators match exactly. |

## 10. Implementation notes

### 10.1. Code units vs. code points

`LENGTH`, `SUBSTRING`, `FIND` and `RFIND_WITHIN` may be implemented over UTF-16 code
units (.NET, Java, JavaScript), over UTF-8 bytes (Go, Rust) or over code points
(Python). This does **not** change the result, because every index used by the
algorithm is a separator boundary:

* all indices produced by `RFIND_WITHIN` are the start of an occurrence of a
  separator, and every separator is a whole sequence of characters;
* `segmentStart` is always `cut + LENGTH(terminator)`, i.e. also a character boundary.

Consequently a cut can never split a surrogate pair or a multi-byte sequence. The only
requirement is that the *same* unit is used consistently inside one implementation.

### 10.2. Complexity

Let `n = LENGTH(value)`, `m` be the number of characters of the pattern, `s` the
number of separators, and `a` the number of alternatives.

| Phase | Cost |
| --- | --- |
| Tokenization of one alternative | `O(m)` |
| Locating separators | up to `s` reverse scans, each `O(n·L)` for a terminator of length `L` |
| Cutting the value | at most one forward scan per variable, each `O(n·L)` |
| Pattern set | the sum over `a` alternatives |

The worst case is `O(n · m)` per pattern set (with `s ≤ m` and `L ≤ m`), which is the
same order as a naive `Split` per separator, and there is no combinatorial
explosion. In practice the reverse scans of step 1 are what make greedy variables
cheap: the end position of every greedy variable is known before any text is copied.

Typical optimisations that keep conformance:

* tokenize pattern sets once and cache the parts;
* search the *remaining prefix* only (`maxEnd`), which is what the specification
  already requires — do not scan the whole value repeatedly when the bound shrinks;
* avoid substring copies during scanning (use indices and copy only at the end);
* use `Ordinal`/byte-wise comparison; never use culture-aware comparison.

### 10.3. Common pitfalls

1. **Using a start index instead of a maximum end index** for the reverse search. A
   terminator of length `L` found at `maxEnd` would then overlap the next separator.
   This is the single most likely source of incompatibility.
2. **Advancing the separator cursor per extracted variable** instead of per separator
   part. This breaks greedy variables in patterns that start with a static part
   (Appendix C).
3. **Trimming the value** or the extracted text, or comparing case-insensitively.
4. **Inserting into the result map** (`map.Add`) instead of assigning
   (`map[name] = text`); a repeated name is legal and overwrites.
5. **Returning `null`/`nil`** or throwing for "no match"; return an empty map.
6. **Requiring a full match.** Trailing static parts are anchors that are not consumed,
   and a prefix of variables may be returned (§5.4).
7. **Sorting the alternatives or de-duplicating them.** Order decides ties.
8. **Splitting the value** instead of locating separators, which cannot implement
   greedy variables correctly (the split count is unknown in advance).
9. **Ignoring the leading static part.** It has to be verified as a prefix of the value
   and consumed before the first variable is cut; otherwise the literal silently becomes
   part of the first captured value and a foreign value is reported as a match (§5.1).
   A parser that passes the conformance suite cannot make this mistake — the vectors
   `leading-static-part-*` catch it.

## 11. Conformance suite

[`spec/ufp-1.2.test-vectors.json`](../spec/ufp-1.2.test-vectors.json) contains the
machine-readable suite: every entry is a `value`, a `pattern` (a pattern set) and the
exact map that `Parse` must return.

```json
{
  "specification": "UFP-1.2",
  "vectors": [
    {
      "id": "greedy-path",
      "description": "A greedy variable keeps intermediate separators",
      "pattern": "<Path*>/<Filename>.<Extension>",
      "value": "docs/reports/2024/annual_report.pdf",
      "expected": { "Path": "docs/reports/2024", "Filename": "annual_report", "Extension": "pdf" }
    }
  ]
}
```

How to use it:

1. Read the file, iterate over `vectors`.
2. For each vector call your `Parse(value, pattern)`.
3. Compare the returned map with `expected` **as a set of pairs** — the order of keys
   is irrelevant, but the sizes must be equal.
4. The implementation is conformant when every vector passes and no call throws.

The .NET test suite does exactly this for both reference implementations
([`tests/UFP.NET.Tests/UfpConformanceTests.cs`](../tests/UFP.NET.Tests/UfpConformanceTests.cs)):

```bash
dotnet test UFP.slnx --configuration Release --filter "FullyQualifiedName~UfpConformanceTests"
```

Adding a vector is the recommended way to propose a change of the format: a vector is
a precise, executable statement about behaviour.

A second, dependency-free runner is included for Python
([`src/UFP.Python/ufp.py`](../src/UFP.Python/ufp.py)):

```bash
py src/UFP.Python/ufp.py
# vectors: 77, failures: 0
```

## 12. Versioning

The format version (`UFP-1.2`) numbers the syntax and the parsing behaviour. It is
independent of the version of any library, but the first publication was released together
with the reference implementations: format `UFP-1.2`, libraries `1.2.0` (see Appendix C for
the behaviour fixes included in that release).

The format version follows semantic versioning:

* **patch** — editorial fixes, new conformance vectors that already pass;
* **minor** — new syntax or semantics that do not change the result of an existing
  conformant pattern (for example a new optional marker);
* **major** — any change that alters the result of an existing pattern.

## 13. Open publication, licensing and patent statement

**This document is a public, dated disclosure.** It is published so that the format,
the algorithm and every example in it become **prior art** that no party can later
claim as an exclusive invention. The author does not seek and will not seek patents,
utility models or any other exclusive rights covering the UFP format, the algorithm
described here, or any obvious variant of them, and grants an irrevocable,
royalty-free, worldwide permission to implement, use, sell and distribute the format.

* The specification text and the reference implementations are licensed under the
  [MIT License](../LICENSE).
* Implementations in any language may be released under any licence, including
  proprietary ones.
* The name **UFP** and the version tag `UFP-1.2` may be used to describe an
  implementation without a fee; an implementation is encouraged to state
  "conforms to UFP-1.2" only if it passes the conformance suite of §11.
* If you publish a port, please reference this document and the repository
  <https://github.com/Jagailo/User-Friendly-Pattern-Format> so that the disclosure date
  and the test vectors stay discoverable.

This statement is not legal advice; it is the author's declared intent to keep the
format free and open.

---

## Appendix A. Worked examples

### A.1. Greedy variable with two separators

```text
value:   docs/reports/2024/annual_report.pdf     (35 characters, indices 0…34)
pattern: <Path*>/<Filename>.<Extension>
```

Tokenization (§4): `V: Path (greedy)`, `S: /`, `V: Filename`, `S: .`, `V: Extension`,
therefore `separators = ["/", "."]`.

Step 1 (§5.2), right to left:

| k | Separator | bound before | `RFIND_WITHIN` | bound after |
| --- | --- | --- | --- | --- |
| 1 | `.` | 34 | 31 | 30 |
| 0 | `/` | 30 | 17 | 16 |

Step 2 (§5.3):

| Part | `separatorsSeen` | Terminator | `positions[…]` | `cut` | Captured | `segmentStart` after |
| --- | --- | --- | --- | --- | --- | --- |
| `V: Path` | 0 | `/` | 17 | 17 (greedy) | `docs/reports/2024` | 18 |
| `S: /` | 1 | — | — | — | — | 18 |
| `V: Filename` | 1 | `.` | 31 | 31 (forward) | `annual_report` | 32 |
| `S: .` | 2 | — | — | — | — | 32 |
| `V: Extension` | 2 | — (final) | — | 35 | `pdf` | — |

Result: `Path = "docs/reports/2024"`, `Filename = "annual_report"`, `Extension = "pdf"`.

### A.2. Partial match

```text
value:   x.png
pattern: <A>.<B>.jpg
```

`separators = [".", ".jpg"]`. Step 1: `positions[1] = −1` (no `.jpg`), so the bound
stays `4`; `positions[0] = 1`. Step 2: `A` is terminated by `.`, `positions[0] = 1`, the
forward scan finds `.` at `1`, so `A = "x"` and `segmentStart = 2`. `B` is terminated by
`.jpg`, `positions[1] = −1` → stop with `A = "x"` (§5.4).

### A.3. Pattern set

```text
value:       1-2
pattern set: <A>_<B>|<A>-<B>
```

* `<A>_<B>`: separator `_` is not found → empty map (0 variables).
* `<A>-<B>`: `A = "1"`, `B = "2"` (2 variables) → wins.

Result: `A = "1"`, `B = "2"`.

### A.4. Why the bound must be a maximum end index

```text
value:   xabab
pattern: <A*>ab<B>
```

With `RFIND` bounded by the *start* index, a match could start at the last character
and overlap the following separator; the terminator of `A` is `ab` and the last
non-overlapping occurrence is at index `3`, so `A = "xab"` and `B = ""`. Any
implementation that returns `A = "x"` for this value has used start-index semantics and
is not conformant.

## Appendix B. Mapping to the reference implementation

| Specification | `UfpParser.cs` (both `UFP.NET` and `UFP.NET.Standard`) |
| --- | --- |
| §3, §4 — tokenization | `SplitPattern`, `PatternPart` (record / class with `Value`, `IsSeparator`, `IsGreedy`) |
| §5.2 — locate separators | the first loop of `ParseBySinglePattern` (`LastIndexOf` bounded by `searchEnd - 1`) |
| §5.3 — cut the value | the second loop of `ParseBySinglePattern` |
| §5.4 — failure semantics | `return results;` inside the loop |
| §6.1 — split the pattern set | `patterns.Split(SeparatorChar, StringSplitOptions.RemoveEmptyEntries)` |
| §6.2 — selection | `OrderByDescending(x => x.Count).First()` (LINQ is a stable sort, so ties keep the first alternative) |
| §2.2 — `RFIND_WITHIN` | `string.LastIndexOf(string, int)` — the two-argument overload has exactly the `maxEnd` semantics |
| §2.2 — `FIND` | `string.IndexOf(string, int, StringComparison.Ordinal)` |
| §2.2 — `TRIM` | `string.Trim()` |

The reference implementation is a single file with no dependencies, so it can be
copied into a project instead of being referenced as a package. The two files must be
kept in sync (`net10.0` and `netstandard2.0`).

## Appendix C. Changes in reference implementation 1.2.0

The last published version, `1.1.0`, does not compile and contained three parsing defects.
Version `1.2.0` is the first version that implements this specification; it includes the
compile fix that was prepared as `1.1.1` (never released) and these three behaviour fixes:

1. **Exception on a separator at index 0.** The reverse scan called
   `LastIndexOf(separator, searchEnd - 1)` unconditionally. When a separator had been
   found at index `0` and separators to its left still had to be located, `searchStart`
   was `-1` and `ArgumentOutOfRangeException` was thrown, e.g. `<A>_<B>_<C>` with `_x`.
   The specification requires the separator to be treated as not found (§5.2), which
   yields an empty or partial result.
2. **Separator cursor advanced per variable.** The cursor that indexes the located
   positions was incremented after every extracted variable instead of at every separator
   part. For a pattern that starts with a static part the cursor therefore pointed at the
   wrong separator: `<A*>_<B>` with `a_b_c` was correct, but `_<A*>_<B>` with `_a_b_c`
   returned `A = ""`, `B = "a_b_c"` instead of `A = "_a_b"`, `B = "c"`, and some patterns
   of this shape threw `ArgumentOutOfRangeException` from `Substring`. §5.3 defines the
   cursor as the number of separators seen, which is the intended behaviour and matches
   the equivalent pattern without the leading static part.
3. **Leading static part was neither verified nor consumed.** In `1.1.0` a static part in
   front of the first variable was ignored, so the literal leaked into the captured value
   and foreign values matched: `IMG_<Number>.jpg` returned `Number = "IMG_1234"` for
   `IMG_1234.jpg` and `Number = "scan_99"` for `scan_99.jpg`, which also made alternatives
   of a pattern set indistinguishable (`IMG_<Number>.jpg|scan_<Number>.jpg`). §5.1 now
   requires the value to start with the leading part and consumes it, so the same pattern
   returns `Number = "1234"` and returns nothing for `scan_99.jpg`.

Everything else was verified to be identical to the published behaviour: the conformance
suite (§11) passes on both implementations, and 14 000 randomly generated
(pattern, value) pairs — including malformed patterns, empty values, overlapping
separators, greedy marks and multiple alternatives — produce identical results in the
reference implementations and in the Python port
[`src/UFP.Python/ufp.py`](../src/UFP.Python/ufp.py), which was written from §4–§6 of this
document and is used to generate the conformance suite (Python indexes strings by code
point, .NET by UTF-16 code unit, so the two ports also confirm §10.1).

No other behaviour differs, and no pattern that is well-formed and starts with a
variable has changed.
