# UFP for end users - a five-minute guide

UFP is a small language that tells a computer which pieces of text you want. You do not need to know programming. You write your pattern as one real example, and the computer fills in the blanks.

## Your first pattern

Suppose you have a file name:

```text
04-05-2024_scan.jpg
```

You want the date, the name and the extension. Write this pattern:

```text
<Date>_<Name>.<Extension>
```

The result is:

```text
Date      = 04-05-2024
Name      = scan
Extension = jpg
```

A pattern has two kinds of text:

- **Fixed text** - everything outside the angle brackets. It must be exactly the same in your text. Here it is `_` and `.`.
- **A name in angle brackets** - a place where the text may change. You choose the name, and the computer puts the text it finds there under that name.

That is the whole idea.

## Choose good names

- Any words you like: `<Date>`, `<Patient.FirstName>`, `<InvoiceNumber>`.
- A name can contain letters, digits, dots and spaces. The signs `<`, `>` and `|` cannot be part of a name.
- Names are case-sensitive: `<Date>` and `<date>` are two different names.
- Spaces at the edges of a name are ignored: `< Date >` is the same as `<Date>`.
- The same name may appear twice; the last value replaces the first one.
- The result contains every value the pattern managed to find. There is no "ignore this piece" syntax: if you do not need a value, give it a name (for example `<Time>`) and ignore it in the program.

## Fixed text must be there

If a pattern starts with fixed text, your text must start with it too - and that part is not included in the result:

```text
pattern: IMG_<Number>.<Extension>
text:    IMG_1234.jpg          result: Number = 1234, Extension = jpg
text:    scan_99.jpg           result: (nothing)
```

Fixed text at the end works the same way: it must be present.

```text
pattern: <Name>.jpg
text:    holiday.jpg           result: Name = holiday
text:    holiday.png           result: (nothing)
```

This is how one pattern can pick the right files out of a folder full of different ones.

## Take as much as possible: the star

Put a `*` right before the closing `>` when the variable should take as much text as possible:

```text
pattern: <Path*>/<Filename>.<Extension>
text:    docs/reports/2024/annual_report.pdf
result:  Path = docs/reports/2024, Filename = annual_report, Extension = pdf
```

Without the star, `Path` would stop at the first `/` and the rest of the path would land in `Filename`. The star is only a "wildcard" mark. Anywhere else in a name it is just an ordinary character: `<A*B>` is a name `A*B`.

## Several patterns at once

Put several patterns in one line, separated by `|`:

```text
IMG_<Number>.jpg|scan_<Number>.jpg
```

This pattern accepts both `IMG_1234.jpg` and `scan_99.jpg`. If several patterns fit the text, the one that found the most names wins; when they are equal, the leftmost one wins.

For example, a clinic may have three devices that produce three different file names. One line with `|` handles all three:

```text
Scan_<Date>_<Patient>.dcm|MR_<Date>_<Patient>.dcm|CT_<Date>_<Patient>.dcm
```

No need to write three separate checks in the program.

## If nothing comes back

An empty result is not an error. It simply means "this text does not look like the pattern". Check these points:

1. Is the fixed text exactly the same, including upper/lower case?
2. Are all fixed pieces present, including the one at the end?
3. Two names cannot stand next to each other without fixed text: `<First><Last>` will not work - use `<First>_<Last>` or `<First> <Last>`.
4. In a pattern the signs `<`, `>` and `|` have a special meaning; `*` is special only right before `>`. They cannot be part of the fixed text. If your text contains `<`, `>` or `|` where you wanted fixed text, choose a different fixed fragment around that place.

If a pattern fits only partially, you may get some of the names but not all. That is normal: UFP returns everything it managed to find before the first problem.

## What UFP does not do

- It does not check that a date is a real date or that a number is a number. Everything is text: `"10"` stays `"10"`, not `10`.
- It does not change your text: spaces, letter case and punctuation stay exactly as they are.
- It does not search inside a longer text: matching always starts at the first character. If you need to skip unknown text at the beginning, make a variable for it.
- It does not create text from values; it only extracts values from text.

Text in any language works, including right-to-left languages such as Arabic and Hebrew.

## Examples for real work

| Task | Pattern | Example text |
| --- | --- | --- |
| Medical scan | `Scan_<Date>_<Patient.LastName>_<Patient.FirstName>.dcm` | `Scan_04-05-2024_Ivanov_Alexey.dcm` |
| Invoice | `<Year>-<Month>-<Number>_<Supplier>.pdf` | `2024-05-1287_Acme.pdf` |
| Car part | `<Manufacturer>-<PartNumber>_<Description>.jpg` | `Bosch-0986452041_OilFilter.jpg` |
| Photo | `IMG_<Number>.<Extension>` | `IMG_2043.JPG` |
| Log line | `<Date> <Time> <Level> <Message>` | `2024-05-04 12:00:01 WARN Disk space low` |
| Full path | `<Folder*>/<File>.<Extension>` | `work/2024/may/report.pdf` |

Dots inside a name are just part of the name: `Patient.LastName` is one name, not two.

## Quick reference

| You want to | Write |
| --- | --- |
| capture a changing piece of text | `<Name>` |
| capture everything up to the last fixed piece | `<Name*>` |
| require fixed text | type it as it is |
| allow several shapes of text | separate the patterns with `\|` |
| keep a literal `*` in a name | do not put it at the very end: `<A*B>` |
| use spaces in fixed text | type them: `<First> <Last>` |

The complete rules for programmers are in the [specification](SPECIFICATION.md). If you only write patterns, this page is enough.
