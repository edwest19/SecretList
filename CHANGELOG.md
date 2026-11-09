# Changelog

> AI-generated: this file was written by Claude (Anthropic). SecretList is a project built with AI-generated code; see [README.md](README.md).

Every release has a heading `## [X.Y.Z] - YYYY-MM-DD`. Changes not yet released are under `## [Unreleased]`.

## [Unreleased]

### Changed
- **The package is published by Edwin West** (`CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`, the name releases are signed as) instead of the template's `CN=AppPublisher`, and its version is 0.1.0.0. Windows treats it as a different package from earlier development builds, so records typed into those builds stay in their own data folder and aren't carried over.
- SecretList no longer asks for the `systemAIModels` permission (Windows AI), which it never used. It asks only for `runFullTrust`.
- SecretList is built for x64 only.
- The package includes the Windows App SDK runtime (`WindowsAppSDKSelfContained`), now version 2.3.1 (was 2.2.0). Release builds are no longer trimmed.
- Printing no longer writes `print-debug.log`, which recorded every record's category and nickname in the app's data folder.

### Project
- The code is split into three projects. `SecretList.Core` holds the models and the `records.txt` and `schema.md` code, and has no Windows dependencies. `SecretList.Core.Tests` holds MSTest tests for it. `SecretList.UI` is the WinUI app, which calls `SecretList.Core`. The app behaves the same as before.
- The version is set once, in `Directory.Build.props` (0.1.0). `global.json` selects a .NET 10 SDK. `SecretList.slnx` holds the three projects.
- `.gitignore` also ignores signing files (`*.pfx`, `*.cer`).
