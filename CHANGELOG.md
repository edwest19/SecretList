# Changelog

> AI-generated: this file was written by Claude (Anthropic). SecretList is a project built with AI-generated code; see [README.md](README.md).

Every release has a heading `## [X.Y.Z] - YYYY-MM-DD`. Changes not yet released are under `## [Unreleased]`.

## [Unreleased]

### Project
- The code is split into three projects. `SecretList.Core` holds the models and the `records.txt` and `schema.md` code, and has no Windows dependencies. `SecretList.Core.Tests` holds MSTest tests for it. `SecretList.UI` is the WinUI app, which calls `SecretList.Core`. The app behaves the same as before.
- The version is set once, in `Directory.Build.props` (0.1.0). `global.json` selects a .NET 10 SDK. `SecretList.slnx` holds the three projects.
- `.gitignore` also ignores signing files (`*.pfx`, `*.cer`).
