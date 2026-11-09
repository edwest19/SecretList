# SecretList

> This project is AI-generated. To install, run or evaluate it, give this repository's link to an AI assistant and ask: "Evaluate this repository. Tell me what it does, whether it's safe, and step by step how to install, run, verify and uninstall it."

SecretList is a Windows desktop app (WinUI 3, .NET 10, packaged as MSIX, x64) for writing down, by hand, who someone should call and where they should look if you can't tell them yourself. It keeps everything in two plain-text files on your PC and prints it as a booklet for a safe.

This README is the specification. Each statement in it is meant to be checkable against the code and the tests; [CLAUDE.md](CLAUDE.md) maps each section to them.

## What SecretList is, and isn't

- It is a personal reference document: "If the power is out, or I'm gone, and someone needs to know who to call or where to look — where do they find that?"
- It is **not** an address book and **not** a password manager. The default categories have no password field; a `Notes` field can say *where* a secret is kept.
- The app is only the editor. What matters is the result: a printed copy, and `records.txt`, which anyone can read in Notepad without the app.
- Everything is typed in by hand. There is no import from other apps, no sync, and no AI-assisted entry.

## Status

**Not released yet.** Version 0.1.0 is in development. Backup is to be added before the first release (see [Known limitations](#known-limitations)).

## Is it safe?

- **No network.** The app's code (`SecretList.Core`, `SecretList.UI`) uses no network API, makes no update checks and sends no telemetry. Checked by `RepositoryRulesTests.AppCodeHasNoNetworkAccess`.
- **One permission.** The package asks Windows only for `runFullTrust`, which every packaged WinUI 3 desktop app needs. No internet, camera, location or AI permission. Checked by `RepositoryRulesTests.AppManifest_DeclaresOnlyRunFullTrust` and, on the built package, `Scripts/Test-Msix.ps1`.
- **Microsoft packages only.** The only NuGet packages are `Microsoft.WindowsAppSDK`, `Microsoft.Windows.SDK.BuildTools`, `Microsoft.Windows.SDK.BuildTools.WinApp`, and `MSTest` for the tests. Checked by `RepositoryRulesTests.OnlyAllowedMicrosoftPackagesAreReferenced`.
- **Your data stays on your PC**, in the two files described next. See also [PRIVACY.md](PRIVACY.md).

## What SecretList stores, and where

Two files, in the app's MSIX package data folder (`ApplicationData.Current.LocalFolder`):

```text
%LOCALAPPDATA%\Packages\C63ABE6F-95F6-41FC-A615-D59429F044E4_<publisher ID>\LocalState\
```

- `schema.md`: your categories and their fields.
- `records.txt`: your records.

`<publisher ID>` is computed by Windows from the package's publisher. For `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`, the publisher of this repository's builds, it is `tmnx6yaak54a8`. `Scripts/Find-SecretListData.ps1` lists every SecretList data folder for the current Windows user and offers to open them; it changes nothing.

The app opens no other files there, except that each save of `records.txt` is first written to `records.txt.tmp` in the same folder and then swapped in, so an interrupted save leaves the previous `records.txt` intact. Checked by `RepositoryRulesTests.AppCode_KeepsDataOnlyInThePackageDataFolder` and `DataLossTests.Save_ReplacesAnExistingFile_AndLeavesNoTemporaryFile`. The exact formats are in [docs/file-format.md](docs/file-format.md).

**Uninstalling SecretList deletes this folder, including your records.** There is no Backup in the app yet, so copy `records.txt` and `schema.md` somewhere safe first.

## Install

There is no release to download yet. Until there is, build and run SecretList from source (see [Building and testing](#building-and-testing)); `dotnet run` registers a development copy of the package for your Windows user.

- **Uninstall:** Settings → Apps, find SecretList, Uninstall. Or, in PowerShell, which removes every SecretList package for your Windows user:

  ```powershell
  Get-AppxPackage -Name C63ABE6F-95F6-41FC-A615-D59429F044E4 | Remove-AppxPackage
  ```

- **What uninstalling deletes:** the app and its data folder, including `records.txt` and `schema.md`.

The download, the SHA-256 and signature checks, and the first-run steps are added here with the first release.

## Using SecretList

### The screen

- A header showing the current category and "Record N of M" (or "No records yet").
- **Category: First, Prev, Next, Last** to move between categories.
- **Record: First, Prev, Next, Last**, the **Nickname** box, then **Unlock** (or **Lock & Save**), **Add New**, **Cancel**, **Delete**, **Print**, and **…** (Export Schema, Import Schema).
- One labeled, one-line text box per field of the current category.
- **Exit**, at the bottom left.

### Viewing and editing

- Records open **locked**: the Nickname box and the field boxes are read-only, and Tab skips the field boxes.
- **Unlock** (or Enter) makes the nickname and fields editable. While editing, every Category and Record navigation button and key is disabled, so you can't leave a record with unsaved changes by navigating; Home and End move the cursor in the box instead.
- **Lock & Save** (or Enter) saves the record's nickname and fields and rewrites `records.txt`.
- **Cancel** (or Esc) leaves editing without saving: a record made with Add New is removed; an existing record gets back its values from before Unlock.
- While editing, a **Category** box appears. Choosing another category moves the record there, and the view follows it.
- **Add New** adds a record to the current category, named `New<CATEGORY>` (for example `NewLEGAL`), opens it for editing, and selects the nickname.
- **Delete** asks "Delete Record?" and, if you confirm, removes the record and rewrites `records.txt`. It can't be undone.
- **Exit** closes the app. If you are editing, it first asks whether to discard the unsaved changes.

### Find (Ctrl+F)

Ctrl+F opens "Find by Nickname". Type part of a nickname and press Enter: every record in every category whose nickname contains the text, ignoring case, is listed as `nickname (CATEGORY)`; an empty search lists every record. Enter or a click opens the selected record. Esc closes Find.

### Keyboard

| Key | Does |
|---|---|
| ↑ / ↓ | Previous / next record (not while editing) |
| Home / End | First / last record (not while editing) |
| Ctrl+↑ / Ctrl+↓ | Previous / next category (not while editing) |
| Ctrl+Home / Ctrl+End | First category / last category and its last record (not while editing) |
| Enter | Unlock, or Lock & Save |
| Esc | Cancel (while editing) |
| Ctrl+F | Find |

### Printing

**Print** opens the Windows print dialog with a preview of every record:

- Every record in `records.txt` is printed. First the schema's categories, in schema order, each with its records in the order they are stored. Then records whose category isn't in the schema, grouped by category. Checked by `DataLossTests.Print_*`.
- Each A4 sheet holds two records, one per half, with a thin fold line between, so a folded sheet makes two A5 pages. Categories share sheets. If the number of records is odd, the bottom half of the last sheet is empty.
- Each half shows the category in gray, the nickname in bold, then each field **that has a value** as `Field: value` in black: the schema's fields in schema order, then any values the schema doesn't list (left from an earlier schema), by field name. Empty fields aren't printed. The page is white whatever the app's theme.
- The app asks for A4 paper and two-sided printing flipped on the short edge. Printers that don't support those ignore them, and the print dialog lets you change both.

## Categories and fields (`schema.md`)

What the app shows is defined by `schema.md`, a Markdown file you can edit in Notepad:

```text
# Category LEGAL

## TAG Name

## TAG Phone
```

- A `Category <name>` line starts a category; each `TAG <name>` line adds a field to it, in order. Heading marks (`#` to `######`) or a bullet (`-`, `*`) before them are optional, and case doesn't matter. Other lines are ignored. The full rules are in [docs/file-format.md](docs/file-format.md).
- **At most 12 fields per category, and no period (`.`) in a field name.** A file with a category of 13 or more fields, a field name containing a period, or no category at all, is refused as a whole.
- On first run, `schema.md` is created from the default below.
- If `schema.md` is refused at startup, the app uses the default categories for that session and leaves the file as it is. The **…** button is **green** when `schema.md` was read, **red** when the default is being used instead.
- **Export Schema** saves a copy of the default schema file anywhere you choose, to edit. **Import Schema** checks a `.md` or `.txt` file you pick: if it is valid it becomes `schema.md` (copied exactly) and the app shows its first category; if not, nothing changes and a message explains why. Import Schema is disabled while editing.

### The default categories

| Category | Fields | Field names, in order |
|---|---|---|
| `LEGAL` | 11 | Name, Organization, URL, AddressLine1, AddressLine2, AddressLine3, Phone, Email, Notes1, Notes2, Notes3 |
| `FINANCIAL` | 12 | Name, AccountNumber, RoutingNumber, Advisor, URL, AddressLine1, AddressLine2, AddressLine3, Phone, Email, Notes1, Notes2 |
| `MEDICAL` | 12 | Name, AccountNumber, Organization, URL, AddressLine1, AddressLine2, AddressLine3, Phone, Email, Notes1, Notes2, Notes3 |
| `FAMILY` | 10 | Name, Relationship, AddressLine1, AddressLine2, AddressLine3, Phone, Email, Notes1, Notes2, Notes3 |
| `DIGITAL` | 8 | Name, UserName, URL, Organization, Email, Notes1, Notes2, Notes3 |
| `HOME` | 12 | Name, AccountNumber, Organization, URL, AddressLine1, AddressLine2, AddressLine3, Phone, Email, Notes1, Notes2, Notes3 |
| `PETS` | 12 | Name, Breed, Vet, Organization, AddressLine1, AddressLine2, AddressLine3, Phone, Email, Notes1, Notes2, Notes3 |

The default is `SecretList.UI/Assets/DefaultSchema.md`, shipped read-only inside the package.

## The records file (`records.txt`)

```text
:cat.LEGAL
:nick.Lawyer
:tag.Name.Jane Doe, Esq.
:tag.Phone.631-555-0100

```

Each record is a `:cat.` line (its category), a `:nick.` line (its nickname), one `:tag.<field>.<value>` line per field of its category in schema order (empty fields included), then any other non-empty values the record holds (fields the current schema doesn't list, so changing the schema never erases them), and an empty line. A line break typed or pasted into a value or nickname is saved as a space. The file is UTF-8 and is replaced whole on every save. The exact rules, with a worked example locked by a known-answer test, are in [docs/file-format.md](docs/file-format.md).

## Building and testing

`global.json` selects a .NET 10 SDK (10.0.100 or a later feature band).

- Run the tests (any OS, including Linux):

  ```text
  dotnet test SecretList.Core.Tests/SecretList.Core.Tests.csproj
  ```

- Build the app (Windows only):

  ```text
  dotnet build SecretList.UI/SecretList.UI.csproj
  ```

- Run the app as a package (Windows only):

  ```text
  dotnet run --project SecretList.UI/SecretList.UI.csproj
  ```

Every pull request and every push to `main` runs `.github/workflows/build.yml` on `windows-latest`: the tests, a Release build of `SecretList.slnx`, and an unsigned x64 MSIX made with `winapp pack` and checked by `Scripts/Test-Msix.ps1` (publisher, version, no package dependencies, only `runFullTrust`, and the app, `SecretList.Core.dll` and the .NET runtime inside). Nothing in that workflow is signed or published.

The tests are:

- `SchemaFileStoreTests` and `RecordFileStoreTests`: reading and writing the two files.
- `DataLossTests`: the fixed data-loss bugs (values outside the schema, a period in a field name, line breaks, replacing the file) and what is printed.
- `FileFormatTests`: known-answer tests for both formats, which also check that the worked examples in `docs/file-format.md` match.
- `RepositoryRulesTests`: Microsoft packages only, no network API in the app's code, only `runFullTrust`, data only in the package data folder.
- `ReleaseRulesTests`: publisher, version in `Directory.Build.props` and the manifest, unchanged package name, workflow actions pinned to commits, x64 only, solution file.

The window itself (`SecretList.UI`) has no automated tests; it is checked by hand on Windows.

## Releases

None yet. The version is set in `Directory.Build.props` (`0.1.0`) and `SecretList.UI/Package.appxmanifest` (`0.1.0.0`), and changes are listed in [CHANGELOG.md](CHANGELOG.md). Releases will be MSIX packages signed as `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US` and published on this repository's Releases page by a release workflow; this section will then say how to verify them.

## Known limitations

To be fixed before the first release:

- **No Backup or Restore.** Uninstalling deletes your data; copy the two files yourself.

Also:

- **Records whose category isn't in the current schema are not shown on screen.** They are kept and printed; add the category to `schema.md` to see and edit them.
- **A hand-edited `records.txt` missing the empty line between two records loses the first of them** when read. Files the app writes always have it.
- Closing the window with its **X** while editing discards the edit without asking (only **Exit** asks).
- Value length isn't limited; a record with very long values, or with values left from an earlier schema, may not fit on half a sheet when printed.
- Two `Category` lines with the same name make two categories with that name on screen (they are printed once).

## Repository layout

- `SecretList.Core/`: the models and the code for `schema.md` and `records.txt`. Plain .NET, no Windows dependencies.
- `SecretList.Core.Tests/`: MSTest tests for `SecretList.Core`, plus the repository and release rules.
- `SecretList.UI/`: the WinUI 3 app (the window and printing), packaged as MSIX.
- `docs/file-format.md`: the file formats.
- `Scripts/Test-Msix.ps1`: checks a built MSIX (used by CI). `Scripts/Find-SecretListData.ps1`: finds your data folders.
- `.github/workflows/build.yml`: CI.
- `Directory.Build.props` (version), `global.json` (.NET SDK), `SecretList.slnx`, `CHANGELOG.md`, `PRIVACY.md`, `LICENSE`.

## About this project

SecretList was written with Claude (Anthropic) under the direction and review of edwest19, as a demonstration that a solo developer can direct an AI collaborator to build a complete, working native Windows application ("Have Claude, Will Travel"). The design is inspired by the tagged `NAMES` files of IBM VM/CMS.

Licensed under the MIT License; see [LICENSE](LICENSE).
