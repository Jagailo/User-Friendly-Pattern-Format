# UFP format (User Friendly Pattern)

UFP - user-friendly pattern format that is used to find the data in the search string. Pattern consists of 2 parts: static and dynamic.

Dynamic part (also called variable) is the data that may differ in the searched strings. In the pattern, they must have a unique name and be wrapped in angle brackets (`<`, `>`). For example, `<Date>`, `<Patient.FirstName>`, `<UselessData1>`. Variables cannot be placed next to each other without some kind of separating character.

Static part (also called separator) is the data that never changes in the searched strings. This can be any character except `<`, `>`, and `|`. Also, separators can be of any length.

### Example of a pattern and string for parsing

String with data for parsing:

![image](https://github.com/Jagailo/User-Friendly-Pattern-Format/assets/10468120/8719317d-fafe-4ab0-87ef-c1f226b37dab)

User friendly pattern:

![image](https://github.com/Jagailo/User-Friendly-Pattern-Format/assets/10468120/af59bcd8-c065-4a5a-973f-88eae2396edf)

Parsing result:

- Date: "22-04-2024"
- Phone: "1288724"
- Patient.FirstName: "Alexey"
- Patient.LastName: "Yagelo"
- Patient.Gender: "male"
- Reference: "KH2708530"
- UID: "3111196K001PB8"
- Extension: "doc"

## Mega pattern

Mega pattern consists of several other patterns. Patterns must be separated by `|` symbol. The search string will be parsed with the pattern that most closely matches the string.

Mega pattern example:
`<Protocol>://<Domain>/<Page>?<FirstParameter.Key>=<FirstParameter.Value>|<Date>_<Name>.<Extension>`

Values for parsing:
- `04-05-2024_fh5_image.png`
- `https://youtu.be/KoFSQeOAYz4?t=10`
- `04-05-2024_scan.jpg`

Parsing result:

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
