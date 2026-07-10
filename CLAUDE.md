# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

SecretList is a WinUI 3 / Windows App SDK desktop app (packaged MSIX) targeting `net10.0-windows10.0.26100.0`. It's a personal, offline reference document answering "if I'm gone, who does someone call, and where do they look" — not an address book or password manager. The app itself is just the editor; the real deliverable is a human-readable `.txt` export (and a printed copy) meant to sit in a safe, readable with nothing but a text editor even if the app never runs again. See [README.md](README.md) for the full design philosophy and user-facing feature list.

Constraints that shape design decisions here, per the README:

- **Offline only** — no cloud, sync, telemetry, or AI-assisted data entry; everything is typed in by hand.
- **No third-party NuGet packages** beyond what the official WinUI template provides.
- **Human-readable storage** — both `records.txt` and `schema.md` (see below) must stay editable in Notepad with no app installed, and must keep working if hand-edited.
- **Visual style is modern Fluent/Mica**, not the earlier green-screen terminal look — see "UI look" below. Don't reintroduce hardcoded black/green/Consolas styling in new XAML.
- Passwords are never stored (see the `DIGITAL` category in [Services/DefaultSchema.cs](Services/DefaultSchema.cs)); `Notes` fields point to where a secret is kept instead.

## Build & run

```powershell
dotnet build                 # RID auto-resolves to win-<arch> via the csproj
dotnet run                   # runs the packaged app; the WinApp build tools register a
                             # debug package identity (AUMID) and launch with it
```

- Platforms: `x86;x64;ARM64`. There is no test project — do not attempt to run tests.
- `dotnet run` works on this packaged WinUI app because of the `Microsoft.Windows.SDK.BuildTools.WinApp` package, which hooks the .NET CLI Run target. In Visual Studio, the two launch profiles are `SecretList (Package)` (MSIX) and `SecretList (Unpackaged)`.
- **Important for debugging:** because the app launches via package identity (AUMID) rather than as a direct child process, `Console.WriteLine` output is invisible in the terminal you ran `dotnet run` from. Anything you need to see at runtime should go to a file instead (see `PrintService`'s `Log()` for the existing pattern) or be inspected with a debugger/DebugView.

## Architecture

The app is a single-page WinUI app. `App` → `MainWindow` (hosts a `Frame`) → navigates to `MainPage`, which contains essentially all logic.

**UI look.** `MainWindow.xaml` uses `MicaBackdrop` and a standard `TitleBar`; `App.xaml` merges the default `XamlControlsResources` (Fluent). `MainPage.xaml` has no hardcoded colors/fonts — everything inherits theme resources (`{StaticResource TitleTextBlockStyle}`, `{ThemeResource CardBackgroundFillColorDefaultBrush}`, etc.), so the app follows system light/dark mode automatically. There is no bottom "function key" hint bar anymore; keyboard shortcuts are discoverable via the on-screen nav/action buttons instead (see "Global key handling" below).

**Schema-driven records.** Three model concepts in [Models/](Models/):

- `EntityDefinition` — a category (e.g. `LEGAL`, `FINANCIAL`), stored in its `Category` property, and its ordered list of `TagDefinition` fields.
- `TagDefinition` — one field within a category: `Name`, `Order`, `IsRequired`, and a computed `IsMultiline` (true only when `Name` is `"Notes"` or `"Address"`, case-insensitive — see "Multi-line fields" below).
- `EntityRecord` — one saved entry: a nickname (`NickName`), a category (`Category`), and a `Dictionary<string,string>` of tag → value.

**Schema is loaded from `schema.md`, not hardcoded.** [Services/SchemaFileStore.cs](Services/SchemaFileStore.cs) reads `ApplicationData.Current.LocalFolder\schema.md` on startup:

```text
# Category LEGAL

## TAG Name

## TAG Organization
...
```

`# Category NAME` starts a category; `## TAG TagName` adds a field to whichever category came before it, in the order written (that order becomes display order). If `schema.md` doesn't exist yet, it's seeded from the packaged, read-only `Assets/DefaultSchema.md` (resolved via `Package.Current.InstalledLocation`, with an `AppContext.BaseDirectory` fallback for unpackaged debug runs) and written out. If it exists but fails to parse into at least one category (e.g. a bad hand-edit), the same default is used in-memory without touching the file — a typo can't brick the app or blank out the schema. Day-to-day schema changes happen by hand-editing `schema.md` or via Import Schema, not by touching the packaged default. A record's `Values` may contain keys not in the current schema, and the schema drives which fields render — so editing the schema changes what an existing record displays without altering its stored data.

**12-tag-per-category cap.** `SchemaFileStore.ParseLines` enforces a hard `MaxTagsPerCategory = 12` after building the parsed list: if *any* category ends up with more than 12 tags, the whole parse is discarded (`ParseLines` returns an empty list), which is exactly the same signal `Load()`/`TryImport()` already treat as "unparseable" — so a schema violating the cap gets the identical fallback-to-default-and-flag-red treatment as a genuinely broken file, with no partial acceptance (it doesn't truncate to the first 12 tags). This is a deliberate invariant the UI and print layout both rely on: `MainPage` never has to handle a category whose fields don't fit on screen, and `PrintService` never has to handle a record whose fields don't fit in a printed A5 half (see Print, below) — there's no scrolling logic and no per-record print overflow/continuation-page logic anywhere in the codebase, so if this cap is ever changed, check both of those assumptions.

**Persistence.** [Services/RecordFileStore.cs](Services/RecordFileStore.cs) reads/writes a single plain-text file at `ApplicationData.Current.LocalFolder\records.txt` (per-user app-data, only meaningful when running with package identity). The custom line format is:

```text
:cat.<Category>
:nick.<NickName>
:tag.<TagName>.<value>      # blank line separates records
```

`Save` only writes tags present in the schema (in `Order`); `Load` splits `:tag.` on the *first* dot, so tag names must not contain `.` but values may. Note `Load` is written with inconsistent indentation vs. `Save` — that's cosmetic, not two implementations.

Multi-line values (see below) still get one line per tag in the file: real newlines are encoded as a literal `\n` two-character sequence on save and decoded back on load. Known tradeoff: a value containing a literal backslash-`n` (e.g. some Windows paths) would be misread as a line break — acceptable for Address/Notes content, but worth remembering if this encoding is ever reused elsewhere.

**Multi-line fields (Address/Notes).** `TagDefinition.IsMultiline` and its mirror `FieldRow.IsMultiline` ([ViewModels/FieldRow.cs](ViewModels/FieldRow.cs)) drive `FieldRowTemplateSelector` ([ViewModels/FieldRowTemplateSelector.cs](ViewModels/FieldRowTemplateSelector.cs)), which picks between two `DataTemplate`s defined in `MainPage.xaml`'s `Page.Resources`: `SingleLineFieldTemplate` (normal one-line `TextBox`) and `MultilineFieldTemplate` (`AcceptsReturn`, `TextWrapping="Wrap"`, `MinHeight="80"`, scrollable). The match is purely by tag name, not a schema.md setting — no new markdown syntax was needed for it.

**UI state machine ([MainPage.xaml.cs](MainPage.xaml.cs)).** All interaction is keyboard-first (Tab-reachable throughout — no mouse required) with a modern Fluent look. Three integer/bool fields drive everything: `_currentTypeIndex`, `_currentRecordIndex`, `_isLocked`. `ShowCurrentRecord()` is the single render function — every navigation/edit handler mutates state then calls it. Records are filtered per-category on the fly via `CurrentTypeRecords` (a LINQ `.Where().ToList()`), so `_currentRecordIndex` indexes into that filtered list, not `_records`.

**Locked vs. edit mode.** `_isLocked` toggles all TextBoxes between read-only and editable, and also toggles the Category picker and every nav button. `FieldRow` binds `IsReadOnly` and a mirrored `IsTabStop` (`!IsReadOnly`) so locked fields are skipped by Tab. The Lock button (labeled "Unlock" / "Lock & Save") and Enter both call `ToggleLock()`, which:
- **Locking** (was unlocked): calls `SaveCurrentRecordEdits()`, then clears `_pendingNewRecord` and the edit snapshot (see Cancel, below).
- **Unlocking** (was locked, about to edit an existing record): snapshots that record's `NickName`/`Values` into `_editSnapshotNickName`/`_editSnapshotValues` so Cancel can revert to them.

**Cancel (button + Esc).** Backs out of whatever's currently unlocked without saving:
- If `_pendingNewRecord` is set (i.e. you're mid **Add New**), it's removed from `_records` outright.
- Otherwise, if there's an edit snapshot, the current record's `NickName`/`Values` are reverted to it.
- Either way, ends by relocking and re-rendering. Guarded to no-op if already locked.

**Reassigning Category while editing.** The Category `ComboBox` is only enabled while unlocked. `CategoryComboBox_SelectionChanged` updates the current record's `Category` and follows it to the new category's tab (`_currentTypeIndex`/`_currentRecordIndex` recomputed) so the record doesn't just vanish from the list you were viewing.

**Global key handling & nav buttons.** `MainPage_Loaded` attaches a `KeyDown` handler to the window root with `handledEventsToo: true`. Bindings: `↑/↓` = prev/next record, `Home/End` = first/last record, `Ctrl+↑/↓` = prev/next category, `Ctrl+Home/End` = first/last category, `Ctrl+F` = Find, `Esc` = Cancel (only while unlocked). All of record/category navigation — keyboard *and* the on-screen First/Prev/Next/Last buttons for both Category and Record — is **disabled while `_isLocked` is false** (mid-edit), so you can't silently navigate away and lose unsaved changes; this also means Home/End behave as normal text-cursor keys while a field is focused for editing, instead of being hijacked for record navigation. While the Find overlay is open (`_searchOverlayVisible`), normal navigation keys are suppressed entirely.

**On-screen layout.** Two toolbar rows above the field list: a Category row (label, `ComboBox`, First/Prev/Next/Last) and a Record row (First/Prev/Next/Last, Nickname, then Unlock/Add New/Cancel/Delete/Print). Tab order runs top-to-bottom through both rows before reaching the field list.

**Find (Ctrl+F).** A modal-style overlay (`SearchOverlay` in [MainPage.xaml](MainPage.xaml), hidden by default) searches every record's `NickName` (case-insensitive substring match) regardless of the current category. Results populate a `ListView` bound to `RecordListItem` ([ViewModels/RecordListItem.cs](ViewModels/RecordListItem.cs)); selecting one via Enter or click calls `NavigateToRecord`, which finds the matching category index and record index and jumps there, closing the overlay.

**Print.** [Services/PrintService.cs](Services/PrintService.cs) uses the WinUI `PrintManager`/`PrintDocument` APIs to print (or preview) the full record collection, plain black-on-white text regardless of the app's own theme. `MainPage` initializes it with the window handle (`hwnd`) on load and triggers it from the Print button.

Pagination is built for an **A5-fold booklet**, not one page per category:
- Page size is **A4 portrait** (`PageWidth`/`PageHeight` at 96 DPI), split into two equal halves (`HalfHeight`) separated by a thin fold-line `Rectangle`, with a small reserved `FoldGap` between them. Folding a printed sheet in half horizontally turns each half into an A5-sized page.
- `BuildAllPages` flattens every category's records into one list, in schema order, then slices it two at a time (`i += 2`) into A4 sheets via `BuildA4Sheet` — a sheet is just "the next two records" in the flattened sequence, so **categories freely share a sheet**; there's no per-category page break. If the total record count is odd, the last sheet's bottom half is simply omitted (no blank placeholder record).
- Each half's content comes from `BuildRecordHalf`, sized to `HalfHeight`. This is only safe because `SchemaFileStore` rejects any schema with more than 12 tags in a category (see above) — a single record's fields are guaranteed to fit in one half, so there's **no per-record overflow or continuation-page logic anywhere in `PrintService`**. If the tag cap is ever raised, this assumption needs to be revisited.
- `OnPrintTaskRequested` requests `PrintMediaSize.IsoA4` and `PrintDuplex.TwoSidedShortEdge` on `printTask.Options` (short-edge flip matches a sheet meant to be folded horizontally). This is wrapped in try/catch and logged — not every printer driver exposes every option, and the print dialog still lets the user override paper size/duplex themselves regardless of what's requested.

Two threading details worth knowing if this code is touched again:
- `PrintDocument` is created **once**, in `Initialize()`, on the UI thread — not inside the `PrintTaskRequested` callback. That callback's `sourceRequested` delegate runs on a worker thread with no `DispatcherQueue`; constructing or touching a `PrintDocument` there throws `RPC_E_WRONG_THREAD` (`0x8001010E`).
- The actual `sourceRequested.SetSource(...)` call is marshaled back onto the UI thread's `DispatcherQueue` (captured in `Initialize()`) via `TryEnqueue`, using a deferral (`GetDeferral()`/`Complete()`) so the print system waits for it.
- `PrintService` currently has verbose file-based logging (`Log()`, writing to `ApplicationData.Current.LocalFolder\print-debug.log`, truncated on every `Initialize()`) left in from debugging the threading issue above. It's harmless to leave, but could be stripped once printing has stayed stable for a while.

## Storage locations

All at `ApplicationData.Current.LocalFolder` (i.e. `%LOCALAPPDATA%\Packages\<PackageFamilyName>\LocalState\` — find the exact folder with `Get-ChildItem "$env:LOCALAPPDATA\Packages" -Recurse -Filter records.txt`, since the debug package identity's folder name isn't a fixed, guessable string):

- `records.txt` — the actual data (see Persistence, above).
- `schema.md` — the schema (see Schema-driven records, above). Hand-editable; regenerated from `DefaultSchema.Create()` if missing or unparseable.
- `print-debug.log` — temporary print-pipeline diagnostics (see Print, above).

## Conventions

- Source files carry a header comment crediting AI assistance ("AI Disclaimer: ... under the direction and review of edwest19"). Preserve this style on files that already have it.
- `ImplicitUsings` and nullable reference types are enabled.
- UI logic lives in `MainPage.xaml.cs` code-behind, not an MVVM framework — the `ViewModels/` classes are plain display/state DTOs (plus one `DataTemplateSelector`), not `INotifyPropertyChanged` view models. `ShowCurrentRecord()` rebuilds `FieldsControl.ItemsSource` wholesale rather than mutating bound collections.
- Don't reintroduce hardcoded `Background`/`Foreground`/`FontFamily` overrides in `MainPage.xaml` — the app deliberately moved off the earlier green-screen look to a theme-driven Fluent one; new XAML should keep inheriting theme resources instead of hardcoding colors/fonts.

## Ignore

Everything under `bin/` and `obj/` is build output (including generated `*.g.cs`, `*.g.i.cs`, and a large tree of Windows App SDK DLLs) — never edit these.
