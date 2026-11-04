# UFP conformance suite

[`ufp-1.2.test-vectors.json`](ufp-1.2.test-vectors.json) is the machine-readable part of
the [UFP-1.2 specification](../docs/SPECIFICATION.md): every vector is a call
`Parse(value, pattern)` and the exact map it must return.

Schema of the file:

| Field | Description |
| --- | --- |
| `specification` | The format version the vectors belong to. |
| `vectors[].id` | Stable identifier, also used in the edge-case catalogue of the specification. |
| `vectors[].description` | What the vector demonstrates. |
| `vectors[].pattern` | The pattern (or pattern set) to pass to `Parse`. |
| `vectors[].value` | The text to parse. |
| `vectors[].expected` | The map of captured variables; key order is irrelevant. |

## Running the suite against your implementation

1. Load the file and iterate over `vectors`.
2. Call your parser with `value` and `pattern`.
3. Compare the returned map with `expected` as a set of pairs (equal size, every key
   present with the same value).
4. A conformant implementation returns the expected map for **all** vectors and never
   throws.

Running the suite against the .NET reference implementations:

```bash
dotnet test UFP.slnx --configuration Release --filter "FullyQualifiedName~UfpConformanceTests"
```

## Reference ports

[`src/UFP.Python/ufp.py`](../src/UFP.Python/ufp.py) is a small implementation of the
algorithm of the specification in a language with code-point string indexing. It doubles
as a runner for the suite:

```bash
py src/UFP.Python/ufp.py              # or: python3 src/UFP.Python/ufp.py
# vectors: 77, failures: 0
```

It is a porting example, not a package: copy the functions you need and keep the
behaviour of `parse`. Further ports (for example PHP or Go) live next to it in `src/`.

[`src/UFP.JavaScript/ufp.js`](../src/UFP.JavaScript/ufp.js) is the same algorithm in a
language with UTF-16 string indexing. Its runner lives next to it:

```bash
node src/UFP.JavaScript/conformance.js
# vectors: 77, failures: 0
```

## Adding a vector

Adding a vector is the recommended way to propose a behaviour change: it is an exact,
executable statement. Keep the `id` in `kebab-case`, describe the rule that the vector
demonstrates in `description`, and make sure both reference implementations and the
specification text agree with it.
