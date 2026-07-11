# SecretList

## An offline, schema-driven personal reference manager for WinUI 3 / .NET

Copyright (c) 2026 edwest19

---

## 🤖 AI Disclaimer

This entire application — architecture, code, and this README — was generated with the assistance of **Claude** (Anthropic AI), under the direction and review of edwest19. Every design decision, class, and line of code was discussed, explained, and reviewed step by step in collaboration with Claude before being written. Nothing here was scanned, auto-generated without oversight, or produced without a human directing the process.

This project doubles as a portfolio demonstration: **"Have Claude, Will Travel"** — proof that a solo developer can direct an AI collaborator to build a complete, working, well-architected native Windows application.

---

## What This Is

SecretList is **not** an address book, and it's **not** a password manager. It's a personal, offline reference document meant to answer one question when it matters most:

> *"If the power is out, or I'm gone, and someone needs to know who to call or where to look — where do they find that?"*

The app itself is just the tool used to **build and maintain** that information. The real deliverable is:

1. A plain, human-readable `.txt` export file
2. A clean, printed paper copy

Both are meant to be stored somewhere safe (like a home safe), alongside a copy of this app on a flash drive if desired — but the app does **not** need to run for the information to be useful. Anyone with the printout, or a text editor and the exported file, can read it.

## Design Philosophy

- **Offline only.** No cloud, no sync, no telemetry, no AI-assisted data entry. Everything is typed in by hand, deliberately, by one person.
- **No third-party packages.** Built entirely on the .NET Base Class Library and the Windows App SDK (WinUI 3). No NuGet dependencies beyond what the official templates provide.
- **Human-readable storage.** Both the data and the schema are stored as simple, tagged plain text — readable and editable in Notepad even without the app installed.
- **Schema-driven, not hardcoded.** The information isn't a fixed "Contact" record — it's built from a small meta-model (categories and their tags), hand-editable in a plain-text schema file, inspired conceptually by IBM VM/CMS's `NAMES` file format.
- **Modern, inviting look.** The UI uses standard Fluent design (Mica backdrop, system light/dark theming, native Windows controls) rather than a retro terminal skin — the goal is an app people actually want to open and use, not one that looks like a chore.
- **Keyboard-first, mouse-optional.** Every field, button, and navigation action is reachable with Tab and Enter alone — useful for fast data entry, and for anyone who simply prefers not to use a mouse.

## Architecture

### Data Model (`Models/`)

- **`TagDefinition`** — describes a single field: its name, display order, and whether it renders as a multi-line text box (see "Multi-line fields" below)
- **`EntityDefinition`** — a named category (e.g. `LEGAL`, `FINANCIAL`) and its ordered list of tags
- **`EntityRecord`** — one actual entry: a nickname (`NickName`), its category (`Category`), and a dictionary of tag → value pairs

### Services (`Services/`)

- **`RecordFileStore`** — reads and writes the entire record collection to/from a custom tagged-text format (see below), using only `System.IO`
- **`SchemaFileStore`** — reads and writes the schema itself from a hand-editable markdown file, seeding a sensible default the first time the app runs (see "The Schema" below)
- **`DefaultSchema`** — the starting/fallback set of categories and tags, used only to seed the schema file on first run or recover from a broken one
- **`PrintService`** — builds and shows a native Windows print preview/dialog for the full record collection, laid out for an A5 booklet (see "Printing" below)

### ViewModels (`ViewModels/`)

- **`FieldRow`** / **`RecordListItem`** — small display-only wrapper classes that keep UI-specific logic (like read-only state, or formatted list labels) out of the core data model
- **`FieldRowTemplateSelector`** — picks between a single-line and a multi-line field layout per row, based on the tag

### UI (`MainPage.xaml` / `MainWindow.xaml`)

- A single-page screen showing **one record at a time**, styled with standard Fluent controls and a Mica window backdrop
- **Locked (browse) mode** by default — fields are read-only, and full keyboard/button navigation is always available
- **Unlocked (edit) mode** — toggle with Enter or the Lock/Unlock button — fields become editable; locking again saves changes. Navigation is disabled while unlocked, so you can't accidentally wander off and lose an unsaved edit
- **Cancel** (button or `Esc`) backs out of an edit or a not-yet-saved new record without saving anything

## The Schema

The set of categories and their fields isn't hardcoded — it lives in a plain-text file, `schema.md`, stored alongside the record data. You can hand-edit it in any text editor, or use the **Import Schema** button in the app to load one from anywhere on disk (Desktop, Documents, a flash drive) without needing to find the app's data folder yourself.

The Import button doubles as a status indicator:

- 🟢 **Green** — the active schema loaded successfully.
- 🔴 **Red** — the schema file was missing or couldn't be read, so the app fell back to its built-in default categories.

Format:

```markdown
# Category LEGAL

## TAG Name

## TAG Organization
```

Parsing is deliberately lenient about style — heading levels (`#`, `##`, `###`) and bullet-list tags (`- TAG Name`) both work, so a file written with a document title and proper markdown structure imports just as well as the simple form above. If the file is missing or fails to parse into anything usable, the app quietly falls back to its built-in default rather than crashing or showing an empty schema.

**12-tag limit per category.** No category may have more than 12 tags. This isn't a soft guideline — a schema (whether it's the on-disk `schema.md` or a file picked via Import Schema) with any category exceeding 12 tags is rejected outright, exactly like an unparseable file: the app falls back to its built-in default, and (for an import) nothing is written to disk. The status button turns red, and a failure dialog explains why an import didn't take. The point of the cap is that every category's fields fit on screen with no scrolling, and every record's fields fit on a single printed A5 half (see Printing, below) — there's no per-record overflow/pagination handling, so this limit is a hard invariant the rest of the app relies on, not just a display preference.

**Default starting categories:**

| Category | Tags |
| --- | --- |
| **LEGAL** | Name, Organization, URL, Address, Phone, Email, Notes, UserName |
| **FINANCIAL** | Name, AccountNumber, Organization, URL, Address, Phone, Email, Notes |
| **MEDICAL** | Name, AccountNumber, Organization, URL, Address, Phone, Email, Notes |
| **FAMILY** | Name, Relationship, Address, Phone, Email, Notes |
| **DIGITAL** | Name, UserName, URL, Organization, Email, Notes |
| **HOME** | Name, AccountNumber, Organization, URL, Address, Phone, Email, Notes |

Note: **no password fields exist anywhere**, by design. The `Notes` field is meant for pointers ("see password manager, master key held by X") rather than actual credentials — this file is built to be found, so it should never contain secrets that matter.

### Multi-line fields

`Address` and `Notes` fields (matched by name, in any category) render as taller, wrapping text boxes that accept multiple lines, instead of a single-line field like everything else.

## File Format

Records are stored in a simple tagged-text format, one record per block, separated by blank lines:

```text
:cat.LEGAL
:nick.MyLawyer
:tag.Name.Jane Doe, Esq.
:tag.Organization.
:tag.Phone.631-555-0000
:tag.Email.
:tag.Address.
:tag.Notes.Handles the estate

:cat.FINANCIAL
:nick.MyBank
:tag.Name.First National Bank
:tag.Organization.
:tag.AccountNumber.AC-4471
:tag.Phone.
:tag.Address.
:tag.Notes.
```

Every tag is always written, even if empty — so the file's shape always matches the current schema, and nothing is silently missing. Multi-line `Address`/`Notes` values still occupy a single line in the file; real line breaks are encoded as a literal `\n` and decoded back automatically when the app reads the file.

## Printing

Printing is designed to produce a small, foldable booklet rather than a stack of full-size pages:

- Pages are laid out as **A4, portrait**, with **two records per sheet** — one in the top half, one in the bottom half, separated by a thin fold line. Folding a printed sheet in half horizontally turns each half into an A5-sized page.
- Records are placed two-per-sheet straight through the full record list, in category order — a sheet is simply "the next two records," so categories freely share a sheet rather than each starting its own page. If the total record count is odd, the very last sheet's bottom half is left blank.
- **Duplex printing is requested by default** (both sides of the sheet, flipped on the short edge, matching the horizontal fold), along with A4 paper size. Whether these are actually honored depends on the printer/driver — the print dialog still lets you override paper size or duplex yourself if needed.
- This layout only works because of the **12-tag-per-category limit** described above — a single record's fields are guaranteed to fit within one A5 half, so there's no per-record overflow or continuation-page logic.

## Current Status

**Working:**

- Full CRUD (Create, Read, Update, Delete) for records
- Persistent storage in the app's local AppData folder
- Hand-editable, importable schema (`schema.md`) with a default fallback
- Multi-line Address/Notes fields
- Find by nickname (`Ctrl+F`)
- Print (A4 sheets, two records per sheet for an A5-fold booklet, duplex requested by default, via the native Windows print dialog)
- Full keyboard-first navigation, with matching on-screen buttons for everything (no mouse required)
- Cancel/undo for in-progress edits and new, not-yet-saved records

**Planned:**

- Export to a user-chosen `.txt` file (today's persistence writes to the app's own AppData folder; a separate export-to-anywhere flow with a file picker isn't built yet)
- In-app visual schema editor (today, schema changes happen via hand-editing or importing `schema.md`, not through app UI controls)

## Requirements

- Windows 10/11
- .NET 10 SDK
- Windows App SDK / WinUI 3

## Building

```text
dotnet build
dotnet run
```

## License

Licensed under the [MIT License](LICENSE). Free software — use, modify, and
distribute it for any purpose.

See [PRIVACY.md](PRIVACY.md) for the privacy policy — the short version is
that the app collects no information.
