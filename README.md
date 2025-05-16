# UFP Format (User Friendly Pattern)

UFP - user-friendly pattern format that is used to find data in search strings. Patterns consist of 2 parts: static and dynamic.

## Pattern Components

### Dynamic Parts (Variables)
- Data that may differ in searched strings
- Must have a unique name wrapped in angle brackets (`<`, `>`)
  - Examples: `<Date>`, `<Patient.FirstName>`, `<UselessData1>`
- Cannot be placed next to each other without a separator
- Can be marked as greedy by adding `*` before the closing bracket (e.g., `<StudyUid*>`)

### Static Parts (Separators)
- Data that never changes in searched strings
- Can be any character except `<`, `>`, and `|`
- Can be of any length

## Greedy Variables
A greedy variable (marked with `*`) will match as much text as possible until reaching the next separator. This is useful when:
- The variable content may contain characters that would normally match subsequent separators
- You want to capture all remaining text until a specific pattern

Example:
`<Path*>/<Filename>.<Extension>`

For input `docs/reports/2024/annual_report.pdf`:
- Path: "docs/reports/2024"
- Filename: "annual_report"
- Extension: "pdf"

Without greedy marking, the pattern might incorrectly split on intermediate `/` characters.

## Example of Pattern and String

**String with data for parsing:**

![image](https://github.com/Jagailo/User-Friendly-Pattern-Format/assets/10468120/8719317d-fafe-4ab0-87ef-c1f226b37dab)

**User friendly pattern:**

![image](https://github.com/Jagailo/User-Friendly-Pattern-Format/assets/10468120/af59bcd8-c065-4a5a-973f-88eae2396edf)

**Parsing result:**
- Date: "22-04-2024"
- Phone: "1288724"
- Patient.FirstName: "Alexey"
- Patient.LastName: "Yagelo"
- Patient.Gender: "male"
- Reference: "KH2708530"
- UID: "3111196K001PB8"
- Extension: "doc"

## Mega Pattern
A mega pattern consists of several patterns separated by `|`. The parser will use the pattern that most closely matches the input string.

**Mega pattern example:**

`<Protocol>://<Domain>/<Page>?<FirstParameter.Key>=<FirstParameter.Value>|<Date>_<Name>.<Extension>|<StudyUid*>.<Type>.<Extension>`

**Values for parsing:**
- `04-05-2024_fh5_image.png`
- `https://youtu.be/KoFSQeOAYz4?t=10`
- `04-05-2024_scan.jpg`
- `1.2.840.52394.3.152.235.2.12.187636473.patient.csv`

**Parsing result:**
- Date: "04-05-2024"
- Name: "fh5_image"
- Extension: "png"
----
- Protocol: "https"
- Domain: "youtu.be"
- Page: "KoFSQeOAYz4"
- FirstParameter.Key: "t"
- FirstParameter.Value: "10"
----
- Date: "04-05-2024"
- Name: "scan"
- Extension: "jpg"
----
- StudyUid: "1.2.840.52394.3.152.235.2.12.187636473"
- Type: "patient"
- Extension: "csv"
