# Deep dive

> Українською: [Docs/uk/DeepDive.md](https://github.com/UkrUtils/Katottg/blob/main/Docs/uk/DeepDive.md)

How the library works inside and why it is built this way: what the official file looks like, which of
its quirks the code has to handle, the snapshot format, and how search finds and ranks results. For
the public API see [API](https://ukrutils.github.io/Katottg/api); for what is not built yet see the
[roadmap](https://ukrutils.github.io/Katottg/roadmap).

## 1. The official file

### 1.1 Where the data comes from

- The codifier is maintained by the **Ministry for Development of Communities and Territories of
  Ukraine**. Its page currently lives on `mininfra.gov.ua`:
  `https://mininfra.gov.ua/diialnist/rozvytok-mistsevoho-samovriaduvannia/kodyfikator-administratyvno-terytorialnykh-odynyts-ta-terytorii-terytorialnykh-hromad`.
  Editions are published there as xlsx files.
- Orders that change the codifier are published on `zakon.rada.gov.ua` (for example `v0042945-25`).
- A description and the current edition are also on the State Statistics Service site:
  `https://ukrstat.gov.ua/klasf/nac_kls/tab_kato.htm`.
- There is no machine-readable dataset (JSON or API) on data.gov.ua.

The ministry's domain has already changed several times (minregion → mtu → mininfra), and at one point
its page returned HTTP 522. That is why the library never touches the network at run time: the data is
embedded, and every source xlsx is archived in `OfficialData/` as `katottg-YY-MM-DD.xlsx`.

### 1.2 Physical layout (edition of 03.11.2026)

| What | Value |
|---|---|
| Sheets | one, `Кодифікатор` |
| Row 1 | the approval stamp "ЗАТВЕРДЖЕНО наказом … від 26.11.2020 № 290 (в редакції наказу … від 12.01.2021 № 3)", in column F, with `\n` inside |
| Row 2 | the title "Кодифікатор адміністративно-територіальних одиниць…" |
| Row 3 | the column header (A–G): `Перший рівень`, `Другий рівень`, `Третій рівень`, `Четвертий рівень`, `Додатковий рівень`, `Категорія об’єкта`, `Назва об’єкта` |
| Rows 4–31754 | data: **31,751 records** |
| Row 31755 | the note "Примітка. Коди Автономної Республіки Крим … набирають чинності з моменту повернення …" |
| Size | 1.5 MB xlsx; 10 MB `sheet1.xml`; 1.8 MB `sharedStrings.xml` |

Each row is the **full path** to a unit: the level columns 1…N hold the codes of all ancestors and of
the unit itself, the rest are empty. The unit's own code is the last filled level. There are no gaps
between levels.

### 1.3 Categories and levels

| Category | Meaning | Level | Count |
|---|---|---|---|
| `O` | region / AR Crimea | 1 | 25 |
| `K` | city with special status (Kyiv, Sevastopol) | 1 | 2 |
| `P` | district | 2 | 136 |
| `H` | territorial community | 3 | 1,772 |
| `M` | city | 4 | 459 |
| `T` | urban-type settlement | 4 | 881 |
| `X` | settlement | 4 | 1,164 |
| `C` | village | 4 | 27,204 |
| `B` | city district | 5 (additional) | 108 |

The category letters in the file are **Latin** (U+0043 and so on), but they look exactly like Cyrillic
letters (С, Н, Х, Т, М, Р, В, О, К), so `KatottgCategoryExtensions.FromLetter` accepts both. The law
that abolishes urban-type settlements is gradually removing category `T`, and new letters may appear,
so the set of categories is not closed: unknown letters become `KatottgCategory.Unknown`.

### 1.4 Code format

`UA` + 17 digits, for example `UA01020030010043419`:

```
UA 01 02 003 001 00 43419
   │  │  │   │   │  └─ 5 digits: service tail (not derived from the hierarchy)
   │  │  │   │   └──── city district (01..)
   │  │  │   └──────── settlement within the community (001..)
   │  │  └──────────── community within the district (001..)
   │  └─────────────── district within the region (01..)
   └────────────────── region (01..85)
```

Verified on all 31,751 records (and kept verified by tests on the full file):

- every code matches `^UA\d{17}$`;
- unit codes are unique;
- the **12 significant digits** (without the 5-digit tail) are unique too, so a unit can be found by a
  code whose tail is wrong or lost (`FindBySignificantPart`);
- a child's code always starts with its parent's prefix. The parent's 12-digit prefix is the child's
  code with the lowest non-zero segment zeroed (`ParentPrefix`). The parent's full code, tail included,
  cannot be computed from the child's code.

### 1.5 Atypical places in the hierarchy (16 rows)

1. **Kyiv and Sevastopol (`K`)**: levels 1–4 repeat the city's code, and the city districts (`B`) sit
   at level 5. They have no district or community.
2. **Prypiat and Chornobyl (`M`)**: sit directly under Kyiv region, levels 2–3 repeat the region's
   code. This is the exclusion zone; there is no district or community.
3. **City districts (`B`)** exist not only in Kyiv and Sevastopol. In 22 more cities they are nested
   under `M`: Kharkiv, Dnipro, Donetsk, Kryvyi Rih, Zaporizhzhia, Lviv and others.

Conclusion: the hierarchy has to be built from the **distinct codes** in a row. The column number does
not tell the level: "level 3" is not always a community.

### 1.6 Data quality issues

| Issue | Count | What the library does |
|---|---|---|
| Trailing space in a name | 319 (313 of them in `H`) | trims on import and reports it as an info issue |
| Latin `i` instead of `і` | 1 (`Вiйтовецька`) | keeps the original name; search treats the Latin look-alikes as Cyrillic |
| Apostrophe `’` (U+2019) | 768 names | search folds all apostrophe variants (removes them by default) |
| Hyphens | 624 names | search treats a hyphen as a space |
| Repeated names | 17,574 distinct names for 30,708 settlements; "Іванівка" occurs 120 times | the name is never a key |
| Repeated names within **one community** | 122 cases (for example `Липовець`, both M and X) | name + community is not a key either; `DisplayName` always shows the category |
| Service rows (stamp, title, header, note) | 4 | the header is found by its text, not by its row number |

## 2. Design principles

1. **Works right after installation.** The package embeds a snapshot of the data (~360 KB gzip). No
   network is needed to start.
2. **Immutable data.** `KatottgDataset` and `KatottgUnit` never change after they are built, and
   collections are exposed as `ReadOnlyCollection`. Readers need no locks. A future update mechanism
   will build a new instance and swap it atomically.
3. **The code is the primary key.** `KatottgCode` is a `readonly struct` over `ulong` (17 digits fit
   below 2^64): no allocations, cheap comparison.
4. **A core without dependencies.** The xlsx parser is `System.IO.Compression.ZipArchive` + `XmlReader`
   (OOXML is a zip of XML files) and lives in the separate `Import` package. No ClosedXML or EPPlus.
5. **Search without external engines.** For 31 thousand records in-memory indexes are enough; Lucene
   would be overkill.
6. **AOT- and trimming-friendly.** No reflection and no `System.Text.Json` in the core
   (`IsAotCompatible=true`).
7. **Determinism.** Search and the snapshot give the same result on every run: every sort ends with a
   unique criterion (ultimately the code).

## 3. In-memory model

`KatottgDataset.Create(edition, records)` takes records in file order and builds:

- `KatottgUnit[]` in the order of the records. Each unit holds its index, code, category, name and a
  reference to its parent. The parent must come earlier, so one pass is enough; the children arrays
  are filled in a second pass, in file order.
- `Dictionary<ulong, int>` from the code value to the index, for `Find`.
- `Dictionary<ulong, int>` from the 12 significant digits to the index, for `FindBySignificantPart`.
- read-only lists by category (`GetByCategory`) and the list of roots (`Regions`).

`Create` rejects empty names, duplicate codes, duplicate 12-digit prefixes and parents that do not
appear earlier. Shortcuts such as `Region`, `District`, `Community`, `PopulatedPlace` and `CityDistrict`
are resolved once, from the categories of the ancestors, so they are `null` exactly when the level does
not exist.

`KatottgDataset.Embedded` is a `Lazy<T>` with `ExecutionAndPublication`: the first access reads the
embedded resource, and every later access returns the same instance. The name index for search is built
separately, on the first search (~26 ms, ~1.4 MB).

## 4. Import (`KatottgSheetParser`)

1. Open the zip, find the first sheet through `xl/workbook.xml` (the sheet name is not required to be
   `Кодифікатор`), read `sharedStrings.xml`.
2. Stream the sheet with `XmlReader`. Shared strings (`t="s"`), inline strings, formula strings and
   numbers are supported.
3. Find the header: the row that has `Перший рівень`, a `Категорія…` column and a `Назва…` column.
   The columns are taken from the header, so a different column order still works.
4. A data row is any row where at least one level column looks like a code. Because of this a row with
   a damaged first level becomes an error instead of silently disappearing as a service row. All other
   rows are skipped and listed in the report; the "Примітка" row is kept in `ImportReport.Notes`.
5. The unit's code is the last filled level; its parent is the last code to the left that **differs**
   from it. This is what handles the repeated codes of Kyiv, Sevastopol and Prypiat.
6. Checks, each with its own `ImportIssueKind`: invalid code, duplicate code, duplicate 12 significant
   digits, missing parent, prefix mismatch with the parent, unknown category, a category unexpected
   under its parent, a gap between levels, an empty name, Latin letters in a Cyrillic name, and
   surrounding whitespace.
7. The edition date comes from the file name (`katottg-YY-MM-DD`, also `YYYY`), then from
   `docProps/core.xml` (`dcterms:modified`). The SHA-256 of the source file is stored in the edition.

Errors mean the row was skipped; `build-snapshot` refuses to write a snapshot if there are any.

## 5. The snapshot `katottg.bin.gz`

A simple binary format that is fast to read and needs no reflection. Little-endian, usually inside
gzip (detected by the `1F 8B` signature, without seeking):

```
magic "KATG" | version u16 | edition-date i32 (yyyyMMdd) | sha256[32] (zeros = unknown)
source: string (empty = unknown)
count u32
string-table: u32 count, followed by the strings
records: count × { code u64, parentIndex i32 (-1 = root), category u8, nameIndex u32 }
```

- A string is a 7-bit-encoded byte length (as in `BinaryWriter`) followed by UTF-8.
- The string table holds the unique names (~17.6 thousand) in order of first appearance, so the output
  is deterministic: the same dataset gives the same bytes before compression.
- Reading validates everything it can: the magic and version, the date, that each code has at most 17
  digits, that each parent index points to an earlier record, that each name index is in range, and
  then all the checks of `Create`. A broken snapshot gives `InvalidDataException`.
- Gzip bytes depend on the zlib version of the runtime, so the freshness test compares the
  decompressed data with what the newest xlsx produces, not the compressed bytes.
- The format is defined in `Storage/SnapshotFormat.cs`; any change to it bumps
  `SnapshotFormat.Version`.

**Why not JSON or SQLite.** JSON is two to three times larger and needs a System.Text.Json source
generator to parse fast, which the core avoids. SQLite adds a native dependency. For 31 thousand rows
that are read once, neither pays off.

## 6. Search

### 6.1 Normalization

`KatottgNormalizer.Normalize` is applied identically to names in the index and to the query:

1. Unicode composition (NFC), lower case; combining marks are dropped.
2. In words with **mixed** alphabets (`Вiйтовецька` with a Latin i), Latin look-alikes become Cyrillic.
   A word written entirely in Latin is left as is: that is a transliteration, not a typo.
3. Apostrophes (`'`, `’`, `ʼ`, `` ` ``, `‘` and a few more) are removed by default ("Кам’янське" =
   "Камянське") or unified to `ʼ` with `ApostropheHandling.Unify`. Apostrophes at the edges of a word
   are treated as quotes and dropped.
4. Anything that is not a letter, digit or apostrophe (hyphens, periods, commas) separates words; words
   are joined with a single space.
5. `ґ→г`, `ё→е`, `ы→и`, `э→е`, `ъ→ʼ`: tolerance for the Russian keyboard layout and common slips.

### 6.2 The name index

`NameIndex` is a sorted array of entries "a normalized name, starting at the beginning of one of its
words". A name with three words gives three entries. A lookup is a binary search for the first entry
that starts with the query, followed by a linear scan while entries still start with it. This is more
compact than a trie and just as fast for ~30 thousand names.

| Entry | Match kind | Base score |
|---|---|---|
| the whole name equals the query | `Exact` | 1.0 |
| the whole name starts with the query | `Prefix` | 0.7 + 0.2 × query length / name length |
| a later word starts with the query | `WordPrefix` | 0.5 + 0.2 × query length / suffix length |

A code query (`UA…`) bypasses the index: it goes to `Find`, then `FindBySignificantPart`, and gets
`MatchKind.Code` with score 2.0.

### 6.3 Parsing the query

The free-form string is split by `,` and `;`. Each part is normalized and checked for a **category
marker**:

- before the name: `с.`, `село`, `м.`, `місто`, `смт`, `пгт`, `с-ще`, `селище`, `селище міського типу`,
  `сел.`;
- after the name: `р-н`, `район`, `обл.`, `область`;
- for communities: `громада`, `ТГ`, `ОТГ`, optionally preceded by `територіальна` / `об’єднана` and by
  `міська` / `сільська` / `селищна`.

A marker becomes a category filter, and its first category is the "primary" one (`с-ще` means
settlement first, but also allows urban-type settlement). A part that consists only of a marker
("Селище" while the user is still typing) is treated as a name. A few aliases are expanded: `АР Крим`
and `АРК` mean "Автономна Республіка Крим".

### 6.4 Resolving context

Each part is tried as the **target**; all other parts are **context**. For each context part the
search finds the units it names (exact and prefix matches, no word-prefix matches):

- a context **with a marker** ("Бердянський р-н") is mandatory: if it matches nothing, the combination
  gives no results; the target must lie strictly inside one of the matched units;
- a context **without a marker** (a street, a postal code, a house number, or just a name) is applied
  only if it leaves at least one candidate. That is how "01001, м. Київ, вул. Хрещатик, 1" ends up as
  Kyiv: the street and the numbers narrow nothing and are ignored.

If the target's own marker filters out everything, the search repeats without the marker's category
(the fallback), because addresses often give the category inaccurately.

Among all "target + contexts" combinations the winner is the one that, in this order: did not need the
fallback, applied more contexts, found a deeper unit, had a higher top score. Ties go to the earlier
part.

### 6.5 Ranking

The final score of a hit is the base score + a small **category weight** (special-status city 0.09,
region 0.08, city 0.07, district 0.06, community 0.05, urban-type settlement 0.04, settlement 0.03,
village 0.02, city district 0.01) + 0.015 if the unit has the marker's primary category. The weights
are smaller than the gaps between match kinds, so they only order units that matched equally well: a
city ranks above a village with the same name.

Ties are broken by category weight, then shorter name, then the name ordinally, then the code. The
order is therefore fully determined.

### 6.6 Speed

Measured with BenchmarkDotNet (`Tests/UkrUtils.Katottg.Benchmarks`), compared with the original
targets:

| Operation | Target | Measured |
|---|---|---|
| cold start of `Embedded` | < 30 ms | ~12 ms |
| `Find(code)` | < 50 ns, no allocations | ~4 ns |
| exact name search | < 1 µs | ~1 µs |
| prefix, 20 results | < 20 µs | ~4 µs |
| free-form address with 3 parts | < 1 ms | ~16 µs |

Memory: ~6.5 MB of managed memory for the dataset, ~1.4 MB more for the name index.
