# Changelog

> AI-generated: this file was written by Claude (Anthropic). SecretList is a project built with AI-generated code; see [README.md](README.md).

Every release has a heading `## [X.Y.Z] - YYYY-MM-DD`. Changes not yet released are under `## [Unreleased]`.

## [Unreleased]

### Fixed
- **Saving no longer erases data.** `records.txt` keeps every value the current schema doesn't list: fields removed from a category, and all fields of records whose category isn't in the schema. They are written after the schema's fields, by name; empty ones are left out. Before, a schema change (or a typo in `schema.md`, which makes the app fall back to the default) followed by any save or delete removed them.
- **A field name containing a period is refused.** `schema.md`, or a file chosen for Import Schema, with a tag name containing `.` is refused like any invalid schema. Before, such a field's value was read back under the wrong name and lost on the next save.
- **Line breaks can't break `records.txt`.** A line break in a value or nickname is saved as a space. Before, it cut the record short, losing the rest of it.
- **Saving can't leave `records.txt` half-written.** It is written to `records.txt.tmp`, flushed to disk, then swapped in. Before, it was overwritten in place.
- **The printout includes everything in `records.txt`**: values the schema doesn't list (after the schema's fields) and records whose category isn't in the schema (after the others). A category name repeated in the schema no longer prints its records twice.
- README: printing prints only fields that have a value, and the category in gray. The README had said every field, including empty ones.

### Changed
- **The package is published by Edwin West** (`CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`, the name releases are signed as) instead of the template's `CN=AppPublisher`, and its version is 0.1.0.0. Windows treats it as a different package from earlier development builds, so records typed into those builds stay in their own data folder and aren't carried over.
- SecretList no longer asks for the `systemAIModels` permission (Windows AI), which it never used. It asks only for `runFullTrust`.
- SecretList is built for x64 only.
- The package includes the Windows App SDK runtime (`WindowsAppSDKSelfContained`), now version 2.3.1 (was 2.2.0). Release builds are no longer trimmed.
- Printing no longer writes `print-debug.log`, which recorded every record's category and nickname in the app's data folder.

### Documentation
- `README.md` is rewritten as the specification of the app as it is: what it stores and where, how to uninstall, every screen and key, printing, the schema rules and the exact default categories, building and testing, and the known limitations (including the data-loss bugs still to fix).
- `docs/file-format.md` specifies `schema.md` and `records.txt` (format version 1), locked by the new known-answer tests in `FileFormatTests`.
- `CLAUDE.md` is rewritten for AI assistants: ground rules, layout, build and test, how to evaluate the repository, a README claims map and the known gaps.

### Scripts
- `Scripts/Find-SecretListData.ps1` finds SecretList's data folders by its package name, and opening them in Explorer works (it no longer stores the results in PowerShell's automatic `$Matches` variable).
- `Scripts/Copy-SecretList.ps1`, a personal copy-the-repository tool with fixed paths, is removed.

### Project
- The code is split into three projects. `SecretList.Core` holds the models and the `records.txt` and `schema.md` code, and has no Windows dependencies. `SecretList.Core.Tests` holds MSTest tests for it. `SecretList.UI` is the WinUI app, which calls `SecretList.Core`. The app behaves the same as before.
- The version is set once, in `Directory.Build.props` (0.1.0). `global.json` selects a .NET 10 SDK. `SecretList.slnx` holds the three projects.
- `.gitignore` also ignores signing files (`*.pfx`, `*.cer`).
- Every pull request runs the tests, builds the app and packages an unsigned MSIX checked by `Scripts/Test-Msix.ps1` (`.github/workflows/build.yml`).
- `RepositoryRulesTests` and `ReleaseRulesTests` check the repository and release rules: Microsoft packages only, no network API, only `runFullTrust`, data only in the package data folder, the publisher, the version, the package name, pinned workflow actions, x64 only and the solution file.
