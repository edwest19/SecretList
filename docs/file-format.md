# SecretList file formats

> AI-generated: this file was written by Claude (Anthropic) from the code in `SecretList.Core` as of 2026-11-08, and updated the same day when the data-loss bugs were fixed (the line syntax didn't change). SecretList is a project built with AI-generated code; see [README.md](../README.md).

**Format version 1** (SecretList 0.1.0). SecretList keeps two text files in its MSIX package data folder (`ApplicationData.Current.LocalFolder`; see README, "What SecretList stores, and where"):

| File | What it is | Read by | Written by |
|---|---|---|---|
| `schema.md` | The categories and, for each, its tags (fields), in order | `SchemaFileStore.Load` at startup | `SchemaFileStore.Save` once, to create it from the packaged default; Import Schema replaces it with a byte-for-byte copy of the picked file (`SchemaFileStore.TryImport`) |
| `records.txt` | The records | `RecordFileStore.Load` at startup | `RecordFileStore.Save`, replacing the whole file on every Lock & Save and every Delete (through `records.txt.tmp`; see Writing) |

Both formats are locked by the known-answer tests in `SecretList.Core.Tests/FileFormatTests.cs`. **Changing either format needs the owner's approval**, a new version of this document, and a plan for files already on disk. Never change a known-answer test's expected values to make it pass.

## Both files

- **Encoding.** Written as UTF-8 with no byte-order mark. Read as UTF-8, or as UTF-16 or UTF-32 if the file starts with that byte-order mark (`File.ReadAllLines`).
- **Line endings.** Written with `Environment.NewLine`, which is CRLF (`\r\n`) on Windows, the only platform the app runs on. Read with CRLF, LF or CR.
- **Plain text.** Both files can be read and edited in Notepad without the app.

## `schema.md`

### Reading

Each line is trimmed, and blank lines are skipped. Then, in this order:

| A trimmed line that matches… | Means |
|---|---|
| `Category <name>`, optionally preceded by 1–6 `#` (and optional spaces) or by `-` or `*` and a space | Starts a new category named `<name>` (the rest of the line, trimmed). |
| `TAG <name>`, with the same optional prefixes | Adds a tag named `<name>` (trimmed) to the most recent category, after its existing tags. |
| anything else | Ignored. |

- The words `Category` and `TAG` are matched ignoring case, and at least one space must follow them. Because the prefix is optional, a plain line such as `Category HOME` or `tag Phone` counts.
- A tag line before the first category line is ignored.
- A tag whose name repeats an earlier tag in the same category (ignoring case) is ignored.
- Category names are not checked for repeats: two `Category LEGAL` lines make two categories named `LEGAL`.
- A category with no tags is kept.

The file is **refused** if it contains no category, if any category has more than **12** tags, or if any tag name contains a period (`.`), because `records.txt` splits a `:tag.` line at its first period. The whole file is refused; it is never cut down to 12 tags. When `schema.md` is refused at startup, the app uses the packaged default (`Assets/DefaultSchema.md`) for that session, leaves `schema.md` untouched, and colors the "…" button red. When a file picked for Import Schema is refused, nothing changes.

### Writing

`schema.md` is written only when it doesn't exist yet: at startup, the packaged default is read with the rules above and written back in this form, one line per category or tag, each followed by an empty line:

```text
# Category <category name>

## TAG <tag name>

## TAG <tag name>

```

Import Schema copies the picked file byte for byte, so `schema.md` keeps whatever style it was written in. Export Schema copies the packaged `Assets/DefaultSchema.md` byte for byte to the chosen file.

### Worked example (known-answer test)

This text (shown with LF; the test also reads it with CRLF):

```text
# My categories
Notes about this file are ignored.

### Category LEGAL
- TAG Name
* TAG Phone
## tag Notes1

category PETS
TAG Name
TAG Vet
TAG name
```

reads as two categories: `LEGAL` with tags `Name`, `Phone`, `Notes1`, and `PETS` with tags `Name`, `Vet` (the second `name` repeats `Name`). Written by `SchemaFileStore.Save`, those categories are exactly:

```text
# Category LEGAL

## TAG Name

## TAG Phone

## TAG Notes1

# Category PETS

## TAG Name

## TAG Vet

```

(every line ending in CRLF on Windows: 116 bytes in all).

## `records.txt`

### Writing

`RecordFileStore.Save` writes every record, in the app's order (the order records were read, then added), as:

```text
:cat.<category>
:nick.<nickname>
:tag.<tag name>.<value>
(one :tag line per tag)
(an empty line)
```

- First come the tags of the record's category **in the current schema**, in schema order. A tag with no value is written with an empty value (`:tag.Phone.`). The category is matched by exact name (case-sensitive).
- Then come the record's **other values**: tags the current schema doesn't list for that category (or, when the category isn't in the schema at all, every tag), ordered by tag name (ordinal), leaving out empty values. So a schema change never erases a value from the file.
- Each line break in a nickname or value (CRLF, CR or LF) is written as one space. Nothing else is changed or escaped.
- Every record, including the last, is followed by an empty line, so the file ends with an empty line.
- The whole text is written to `records.txt.tmp` beside `records.txt`, flushed to disk, and then swapped in for `records.txt` (`File.Replace`), or renamed to `records.txt` when there is none yet. A save interrupted before the swap leaves `records.txt` as it was; a leftover `records.txt.tmp` is overwritten by the next save.

### Reading

`RecordFileStore.Load` reads the file line by line. Lines are not trimmed, and the prefixes are case-sensitive:

| A line that… | Means |
|---|---|
| is empty or only whitespace | Ends the current record. |
| starts with `:cat.` | Starts a new record with the category after the prefix. |
| starts with `:nick.` | Sets the current record's nickname to the text after the prefix. |
| starts with `:tag.` | Splits the text after the prefix at its **first** `.`: before it is the tag name, after it the value (which may contain more periods). A `:tag.` line with no further `.` is ignored. |
| anything else | Ignored. |

- `:nick.` and `:tag.` lines before the first `:cat.` line are ignored.
- If a tag appears twice in a record, the later value wins.
- The last record doesn't need an empty line after it.
- A missing `records.txt` means no records.

### Worked example (known-answer test)

With this schema:

```text
# Category LEGAL
## TAG Name
## TAG Phone
## TAG Notes1
# Category PETS
## TAG Name
## TAG Vet
```

and these three records, in this order: a LEGAL record "Lawyer" (Name `Jane Doe, Esq.`, Phone `631-555-0100`, Notes1 `Will is in the safe. Copy at www.example.com/a.b`), a PETS record "Rex" (Name `Rex`, Vet `Dr. Zoë Müller`), and a LEGAL record "Accountant" with no values, `records.txt` is exactly:

```text
:cat.LEGAL
:nick.Lawyer
:tag.Name.Jane Doe, Esq.
:tag.Phone.631-555-0100
:tag.Notes1.Will is in the safe. Copy at www.example.com/a.b

:cat.PETS
:nick.Rex
:tag.Name.Rex
:tag.Vet.Dr. Zoë Müller

:cat.LEGAL
:nick.Accountant
:tag.Name.
:tag.Phone.
:tag.Notes1.

```

(every line ending in CRLF on Windows: 278 bytes in all, UTF-8, no byte-order mark). Reading that file gives back the same three records.

### Worked example: values outside the schema (known-answer test)

With a schema whose `LEGAL` category has the tags `Name` and `Phone`, a LEGAL record "Lawyer" holding the values Zip `11743`, Name `Jane`, Fax (empty) and Executor `Bob` is written as:

```text
:cat.LEGAL
:nick.Lawyer
:tag.Name.Jane
:tag.Phone.
:tag.Executor.Bob
:tag.Zip.11743

```

`Name` and `Phone` come from the schema, in schema order; `Executor` and `Zip` are kept after them in name order; the empty `Fax` is left out.

## Known limitations of format version 1

- **A missing empty line drops a record.** If a `:cat.` line follows a record without an empty line between them, the earlier record is discarded when the file is read. Files the app writes always have the empty line; this affects hand-edited files only.
- **Records whose category isn't in the schema aren't shown on screen.** They are kept in `records.txt` with all their values and are printed after the others, but the app has no category to show them under until one is added to `schema.md`.
