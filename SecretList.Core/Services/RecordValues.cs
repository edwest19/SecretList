// ============================================================================
// SecretList
// Copyright (c) 2026 edwest19. Licensed under the MIT License (see LICENSE).
// AI-GENERATED CODE: This file was written by Claude (Anthropic).
// ============================================================================

using SecretList.Models;

namespace SecretList.Services;

/// <summary>Helpers for a record's stored values.</summary>
public static class RecordValues
{
    /// <summary>
    /// The record's values whose tag isn't one of <paramref name="schemaTagNames"/> (compared
    /// exactly, case-sensitive) and that aren't empty, ordered by tag name (ordinal). These are
    /// values left over from an earlier schema; records.txt keeps them and the printout shows them.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string>> OutsideSchema(EntityRecord record, IEnumerable<string> schemaTagNames)
    {
        var inSchema = new HashSet<string>(schemaTagNames, StringComparer.Ordinal);
        return record.Values
            .Where(kv => !inSchema.Contains(kv.Key) && kv.Value.Length > 0)
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToList();
    }
}
