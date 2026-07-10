// SecretList
// Copyright (c) 2026 edwest19
//
// AI Disclaimer: This code was generated with the assistance of
// Claude (Anthropic AI), under the direction and review of edwest19.

namespace SecretList.ViewModels;

// Represents one label/value row on the record panel screen -
// e.g. "Phone:" and its current text. One of these exists per
// tag defined for the current entity type.
public class FieldRow
{
    public string TagName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    // True while the screen is in "locked" (browse) mode - the
    // TextBox becomes read-only and gets skipped by Tab, so
    // arrow-key navigation always reaches the page instead of
    // being consumed by a focused text field.
    public bool IsReadOnly { get; set; } = true;

    // Mirrors IsReadOnly - kept as a separate property because it
    // binds to a different XAML property (IsTabStop) than IsReadOnly does.
    public bool IsTabStop => !IsReadOnly;
}
