# Privacy Policy

**SecretList collects nothing. No information is retained by the developer,
by Anthropic, or by any third party.**

## Summary

SecretList is an offline, local-only application. It has no network code,
no analytics, no telemetry, no crash reporting, no update-check pings, and
no cloud sync of any kind. Nothing you type into the app ever leaves your
computer.

## What data exists, and where it lives

- **Your records** (`records.txt`) and **your schema** (`schema.md`) are
  stored only in this app's local Windows app-data folder on your own
  machine (`ApplicationData.Current.LocalFolder`).
- Nothing is uploaded, synced, backed up, or transmitted anywhere by the
  app itself. If you choose to back up these files yourself (e.g. copying
  them to a flash drive, OneDrive, or another drive), that is a decision
  you make outside the app, using tools of your own choosing — not
  something SecretList does on your behalf.
- The app does not require, request, or use an internet connection at any
  point during normal operation.

## No accounts, no passwords

SecretList has no login, no account creation, and does not store
passwords or credentials of any kind — by design. See the README for more
on why password-style data is intentionally excluded from the app's
scope.

## No AI processing of your data

Your record data is never sent to any AI model, cloud service, or
third party — during use of the finished app. (The app's source code was
written with AI assistance during development, as noted in the README's
AI Disclaimer, but that is a statement about how the software was built,
not about what the software does with your data at runtime.)

## Third-party components

SecretList uses no third-party libraries, SDKs, or NuGet packages beyond
the standard .NET Base Class Library and the Windows App SDK. There are
no third-party analytics or advertising SDKs embedded in the app.

## Changes to this policy

If this policy ever changes (for example, if a future version adds an
optional feature that requires network access), that change will be
described here, and any such feature will be off by default and clearly
disclosed before it collects or transmits anything.

## Contact

Questions about this policy can be directed to the developer via the
GitHub repository for this project.
