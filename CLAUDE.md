# CLAUDE.md

> AI-generated: written by Claude (Anthropic) for AI assistants working in this repository. The owner's own instructions (Claude Project / Claude Code user instructions) also apply; where they differ on something specific to this repository, this file wins.

SecretList is a WinUI 3 / .NET 10 desktop app, packaged as MSIX (x64), for keeping an offline, printable list of who to call and where to look. **[README.md](README.md) is the specification.** Read it before changing anything.

## Ground rules

- **README.md, docs, tests and code move together.** A behavior change updates the README, `CHANGELOG.md` and, if it touches a file, `docs/file-format.md`, in the same pull request. A README statement with no code behind it is a bug.
- **Offline.** No network API anywhere in `SecretList.Core` or `SecretList.UI`: no `HttpClient`, no telemetry, no update checks (`RepositoryRulesTests`).
- **Microsoft packages only**: `Microsoft.WindowsAppSDK`, `Microsoft.Windows.SDK.BuildTools`, `Microsoft.Windows.SDK.BuildTools.WinApp`, `MSTest`. Adding or upgrading any package needs the owner's "yes" first.
- **The manifest asks only for `runFullTrust`.** The package name `C63ABE6F-95F6-41FC-A615-D59429F044E4` never changes (a new name means a new, empty data folder for every user). The publisher is exactly `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US` (`ReleaseRulesTests`).
- **Data lives only in `ApplicationData.Current.LocalFolder`**: `records.txt` and `schema.md`. Never build a path under AppData yourself. Changing what the app stores, or where, needs the owner's "yes".
- **The file formats are locked** by `docs/file-format.md` (format version 1) and `FileFormatTests`. A format change needs the owner's approval, a new spec version and a plan for files already on disk. Never change a known-answer test's expected values to make it pass.
- **Bugs are fixed test-first**: commit a test that reproduces the bug, let CI show it failing, then fix.
- **Never weaken, skip or delete a test** to make a change pass, including the rules tests.
- **x64 only.** Fluent look: `MainPage.xaml` inherits theme resources. Don't add hardcoded colors or fonts. The one existing exception is `Foreground="White"` on the "…" status button.
- **Mark AI-generated code**: a header naming Claude in each file Claude writes, an `AI-generated (Claude):` comment on lines Claude changes in files it didn't write, and `Co-Authored-By: Claude` on each commit.
- Never commit secrets, signing files (`*.pfx`) or real records.

## Layout

| Path | What it is |
|---|---|
| `SecretList.Core/SecretList.Core.csproj` | Library, `net10.0`, no Windows dependencies, warnings are errors |
| `SecretList.Core/Models/EntityDefinition.cs` | A category: `Category` (its name) and its ordered `Tags` |
| `SecretList.Core/Models/TagDefinition.cs` | A field: `Name`, `Order` (1-based), `IsRequired` (unused) |
| `SecretList.Core/Models/EntityRecord.cs` | A record: `Category`, `NickName`, `Values` (field name → value) |
| `SecretList.Core/Services/SchemaFileStore.cs` | Reads `schema.md` (lenient parser, 12-field limit), seeds it from the packaged default, Import (`TryImport`), Export (`ExportDefaultSchema`), `Save` |
| `SecretList.Core/Services/RecordFileStore.cs` | Reads and writes `records.txt` (keeps values outside the schema, line breaks → spaces, writes through `records.txt.tmp`) |
| `SecretList.Core/Services/RecordValues.cs` | `OutsideSchema`: a record's non-empty values whose tag the schema doesn't list, by name |
| `SecretList.Core/Services/SafeFile.cs` | Replaces a file through `<name>.tmp` + flush + `File.Replace`, so an interrupted write leaves the old file |
| `SecretList.Core/Services/BackupStore.cs` | Back Up (copy both files into a new dated folder) and Restore (check, then replace both files) |
| `SecretList.Core/Services/PrintContent.cs` | Which records are printed, in what order, with which fields (`PrintedRecord`, `PrintedField`) |
| `SecretList.Core.Tests/` | MSTest 4.0.1: `SchemaFileStoreTests`, `RecordFileStoreTests`, `FileFormatTests` (known answers), `DataLossTests`, `BackupTests`, `RepositoryRulesTests`, `ReleaseRulesTests`; `TestHelper.cs`, `MSTestSettings.cs` |
| `SecretList.UI/SecretList.UI.csproj` | The WinUI 3 app, `net10.0-windows10.0.26100.0`, MSIX with the Windows App SDK included (`WindowsAppSDKSelfContained`) |
| `SecretList.UI/Package.appxmanifest` | Package identity (name, publisher, version), `runFullTrust` |
| `SecretList.UI/App.xaml(.cs)`, `MainWindow.xaml(.cs)` | App start-up; the window (Mica backdrop, title bar) hosting a `Frame` |
| `SecretList.UI/MainPage.xaml(.cs)` | The only page: all UI logic (code-behind, no MVVM framework) |
| `SecretList.UI/ViewModels/FieldRow.cs`, `RecordListItem.cs` | Plain display objects for a field row and a Find result |
| `SecretList.UI/Services/PrintService.cs` | Printing: lays out `PrintContent`'s records, two per A4 sheet |
| `SecretList.UI/Services/DefaultSchemaLocation.cs` | Finds the packaged `Assets/DefaultSchema.md` (`Package.Current`) |
| `SecretList.UI/Assets/DefaultSchema.md` | The default categories, shipped read-only in the package |
| `SecretList.UI/Assets/*.png`, `AppIcon.ico` | Icons and tiles |
| `SecretList.UI/Properties/launchSettings.json` | `SecretList (Package)` and `SecretList (Unpackaged)` profiles |
| `docs/file-format.md` | The `schema.md` and `records.txt` formats (version 1) |
| `Scripts/Test-Msix.ps1` | Checks a built MSIX: publisher, version, self-contained, `runFullTrust` only, contents (used by CI) |
| `Scripts/Find-SecretListData.ps1` | Lists the user's SecretList data folders by package name; read-only |
| `.github/workflows/build.yml` | CI on every pull request and push to `main`: tests, Release build, unsigned MSIX + `Test-Msix.ps1` |
| `Directory.Build.props` | The version (`0.1.0`), shared by all projects |
| `global.json` | Selects a .NET 10 SDK (10.0.100+, latest feature band) |
| `SecretList.slnx` | The solution |
| `CHANGELOG.md`, `PRIVACY.md`, `LICENSE` (MIT), `.gitignore` | |

## Build and test

```powershell
dotnet test SecretList.Core.Tests/SecretList.Core.Tests.csproj   # any OS, including Linux
dotnet build SecretList.UI/SecretList.UI.csproj                  # Windows only
dotnet run --project SecretList.UI/SecretList.UI.csproj          # Windows only: registers a development package and launches it
```

- `dotnet run` works because `Microsoft.Windows.SDK.BuildTools.WinApp` hooks the .NET CLI Run target to register a debug package identity. The app runs with package identity, so `Console.WriteLine` output isn't visible; use a debugger, or a temporary file removed before merging.
- CI builds `SecretList.slnx` in Release and packages with `winapp pack SecretList.UI/SecretList.UI.csproj -c Release --arch x64 --no-sign -p SelfContained=true`.

## How to evaluate this repository

1. Run the tests: `dotnet test SecretList.Core.Tests/SecretList.Core.Tests.csproj`. Report passed, failed and skipped. On Windows, also build the UI project.
2. Check every README statement against the code and a test, using the claims map below. Statements about the window have no automated test; check them in `MainPage.xaml(.cs)` and say they were checked by reading the code only.
3. Check `docs/file-format.md` against `RecordFileStore` and `SchemaFileStore`, and its worked examples against `FileFormatTests` (`Spec_ShowsTheseWorkedExamplesWordForWord` does the second automatically).
4. Check the rules: packages, network APIs, manifest capabilities, data folder (`RepositoryRulesTests`), and publisher, version, package name, pinned actions, x64 (`ReleaseRulesTests`).
5. Report each finding as **statement** (quote it), **evidence** (file and line, or test and result), and **verdict** (true / false / not tested).

## README claims map

| README section | Code | Tests |
|---|---|---|
| What SecretList is, and isn't | `Assets/DefaultSchema.md` (no password field) | `SchemaFileStoreTests.ShippedDefaultSchema_HasSevenCategories_EachWithinTheTwelveTagLimit_AndNoPasswordField` |
| Is it safe? (network, permission, packages) | all of `SecretList.Core`, `SecretList.UI`; `Package.appxmanifest`; `*.csproj` | `RepositoryRulesTests` (all four); `Scripts/Test-Msix.ps1` in CI |
| What SecretList stores, and where | `MainPage.xaml.cs` `DataFilePath`, `SchemaFilePath`; `SafeFile.WriteAllBytes` | `RepositoryRulesTests.AppCode_KeepsDataOnlyInThePackageDataFolder`; `DataLossTests.Save_ReplacesAnExistingFile_AndLeavesNoTemporaryFile`; `BackupTests.Restore_ReplacesBothFilesByteForByte_AndLeavesNoTemporaryFile`; publisher ID: not tested (computed from the publisher; matches the folder Windows created for the earlier `CN=AppPublisher` builds) |
| Install (uninstall) | Windows behavior for MSIX packages | not tested |
| Using SecretList: the screen, viewing and editing, Find, keyboard | `MainPage.xaml`, `MainPage.xaml.cs` (`ShowCurrentRecord`, `ToggleLock`, `CancelEdit`, `AddButton_Click`, `DeleteButton_Click`, `CategoryComboBox_SelectionChanged`, `RunSearch`, `NavigateToRecord`, `Page_KeyDown`, `ExitButton_Click`) | none (UI); checked by hand on Windows |
| Printing | `PrintContent.cs` (what and in what order); `PrintService.cs` (`BuildAllPages`, `BuildA4Sheet`, `BuildRecordHalf`, `OnPrintTaskRequested`: layout, paper, duplex) | `DataLossTests.Print_*`; the layout is checked by hand on Windows |
| Backup and Restore | `BackupStore.cs`, `SafeFile.cs`, `SchemaFileStore.TryRead`; `MainPage.xaml.cs` (`BackupMenuItem_Click`, `RestoreMenuItem_Click`, `PickFolderAsync`) | `BackupTests`; the pickers and dialogs are checked by hand on Windows |
| Categories and fields (`schema.md`) | `SchemaFileStore.cs` (`Load`, `ParseLines`, `TryImport`, `ExportDefaultSchema`, `Save`); `MainPage.xaml.cs` (`UpdateImportButtonStatus`, `ImportSchemaMenuItem_Click`, `ExportSchemaMenuItem_Click`) | `SchemaFileStoreTests`; `FileFormatTests.SchemaFile_*`; `DataLossTests.Load_SchemaWithATagNameContainingAPeriod_*`, `TryImport_SchemaWithATagNameContainingAPeriod_*` |
| The default categories (table) | `Assets/DefaultSchema.md` | `SchemaFileStoreTests.ShippedDefaultSchema_HasExactlyTheCategoriesAndTagsTheReadmeLists` (also checks the README rows) |
| The records file | `RecordFileStore.cs`, `RecordValues.cs` | `RecordFileStoreTests`; `FileFormatTests.RecordsFile_*`, `Spec_ShowsTheseWorkedExamplesWordForWord`; `DataLossTests.Save_*` |
| Building and testing | `global.json`, `*.csproj`, `.github/workflows/build.yml`, `Scripts/Test-Msix.ps1` | CI |
| Releases | `Directory.Build.props`, `Package.appxmanifest`, `CHANGELOG.md` | `ReleaseRulesTests.ManifestVersion_IsTheProjectVersionPlusZero`, `ManifestPublisher_IsTheNameReleasesAreSignedAs` |
| Known limitations | `MainPage.xaml.cs` (`CurrentTypeRecords` shows schema categories only; no window-close handler); `RecordFileStore.Load` (missing empty line); `PrintService.BuildRecordHalf` (no overflow handling); `SchemaFileStore.ParseLines` (repeated category names) | none: each fix comes with a test that reproduces it first |

## Known gaps

- The limitations listed in the README's "Known limitations" are not fixed.
- No release workflow, signing or GitHub release yet; `ReleaseRulesTests` doesn't yet check `release.yml`, the signing subject or a dated `CHANGELOG.md` entry.
- UI logic lives in `MainPage.xaml.cs`, not in `SecretList.Core`, so the window's behavior has no automated tests.
- `Scripts/Find-SecretListData.ps1` is not run by CI.
- The `SecretList (Unpackaged)` launch profile is untested; the app's storage is written for package identity.
- `TagDefinition.IsRequired` is never set or used.
- The tooltip on a red "…" button says `schema.md` "is missing or couldn't be read", but a missing `schema.md` is created from the default and shows green.

## Code notes

- **UI state.** `MainPage` keeps `_currentTypeIndex` (category), `_currentRecordIndex` (index into `CurrentTypeRecords`, the current category's records in stored order) and `_isLocked`. Every handler changes state, then calls `ShowCurrentRecord()`, which rebuilds the field rows (`FieldsControl.ItemsSource`) and the buttons' enabled state.
- **Editing.** Unlocking takes a snapshot of the record (`_editSnapshotNickName`, `_editSnapshotValues`) for Cancel. Add New sets `_pendingNewRecord`, which Cancel removes. Locking calls `SaveCurrentRecordEdits()`, which copies the boxes into the record and saves all records.
- **Keys.** `Page_KeyDown` is attached both in `MainPage.xaml` and, in `MainPage_Loaded`, to the window's root with `handledEventsToo: true`. On the owner's PC (2026-11-08), ↓ moved one record and Enter on a focused Print button did not also unlock; change this only with a reproduced problem.
- **The 12-field limit** lets the screen and the printed half-sheet assume a category's fields fit; there is no scrolling or print overflow handling. Revisit both if the limit changes.
- **Printing.** `PrintContent.Build` (Core, tested) decides what is printed; `PrintService` only lays it out, so don't put selection logic back in the UI.
- **Printing threads.** `PrintDocument` is created once in `Initialize()` on the UI thread. The `PrintTaskRequested` callback's source delegate runs on a worker thread, so `SetSource` is marshaled back with the UI `DispatcherQueue` and a deferral. Creating or touching a `PrintDocument` on the worker thread throws `RPC_E_WRONG_THREAD` (`0x8001010E`).
- **Build output.** Everything under `bin/` and `obj/` is generated; never edit it.
