// ============================================================================
// SecretList
// Copyright (c) 2026 edwest19. Licensed under the MIT License (see LICENSE).
// AI-GENERATED CODE: This file was written by Claude (Anthropic). It moves the
// choice of what to print out of SecretList.UI's PrintService, so it can be tested.
// ============================================================================

using SecretList.Models;

namespace SecretList.Services;

/// <summary>One field as printed: <c>Name: Value</c>.</summary>
public sealed record PrintedField(string Name, string Value);

/// <summary>One record as printed on half an A4 sheet.</summary>
public sealed record PrintedRecord(string Category, string NickName, IReadOnlyList<PrintedField> Fields);

/// <summary>Decides which records are printed, in what order, and which of their fields.</summary>
public static class PrintContent
{
    /// <summary>
    /// The records to print, in order. Today's behavior, as PrintService.BuildAllPages and
    /// BuildRecordHalf choose it: for each category in the schema, its records in stored order,
    /// each with the schema's fields that have a value.
    /// </summary>
    public static List<PrintedRecord> Build(List<EntityDefinition> schema, List<EntityRecord> records)
    {
        var printed = new List<PrintedRecord>();

        foreach (var entityDef in schema)
        {
            foreach (var record in records.Where(r => r.Category == entityDef.Category))
            {
                var fields = new List<PrintedField>();
                foreach (var tag in entityDef.Tags.OrderBy(t => t.Order))
                {
                    string value = record.Values.TryGetValue(tag.Name, out var v) ? v : string.Empty;
                    if (!string.IsNullOrWhiteSpace(value))
                        fields.Add(new PrintedField(tag.Name, value));
                }

                printed.Add(new PrintedRecord(entityDef.Category, record.NickName, fields));
            }
        }

        return printed;
    }
}
