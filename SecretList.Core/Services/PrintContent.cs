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
    /// The records to print, in order, so the printout holds everything records.txt holds:
    /// <list type="number">
    /// <item>For each category in the schema (a repeated category name only once), its records
    /// in stored order. Each shows the schema's fields that have a value, in schema order, then
    /// the record's other values (tags the schema doesn't list), in tag-name order.</item>
    /// <item>Then the records whose category isn't in the schema, grouped by category in the
    /// order the categories first appear, each record in stored order, with all its values in
    /// tag-name order.</item>
    /// </list>
    /// A value that is empty or only spaces isn't printed.
    /// </summary>
    public static List<PrintedRecord> Build(List<EntityDefinition> schema, List<EntityRecord> records)
    {
        var printed = new List<PrintedRecord>();
        var schemaCategories = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entityDef in schema)
        {
            if (!schemaCategories.Add(entityDef.Category))
                continue; // the same category name again: its records are already listed

            var tagNames = entityDef.Tags.OrderBy(t => t.Order).Select(t => t.Name).ToList();
            foreach (var record in records.Where(r => r.Category == entityDef.Category))
            {
                var fields = new List<PrintedField>();
                foreach (var tagName in tagNames)
                {
                    string value = record.Values.TryGetValue(tagName, out var v) ? v : string.Empty;
                    if (!string.IsNullOrWhiteSpace(value))
                        fields.Add(new PrintedField(tagName, value));
                }

                fields.AddRange(OtherFields(record, tagNames));
                printed.Add(new PrintedRecord(entityDef.Category, record.NickName, fields));
            }
        }

        foreach (var category in records.Where(r => !schemaCategories.Contains(r.Category)).GroupBy(r => r.Category))
        {
            foreach (var record in category)
            {
                printed.Add(new PrintedRecord(record.Category, record.NickName, OtherFields(record, []).ToList()));
            }
        }

        return printed;
    }

    private static IEnumerable<PrintedField> OtherFields(EntityRecord record, IEnumerable<string> schemaTagNames) =>
        RecordValues.OutsideSchema(record, schemaTagNames)
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => new PrintedField(kv.Key, kv.Value));
}
