// ============================================================================
// SecretList
// Copyright (c) 2026 edwest19. Licensed under the MIT License (see LICENSE).
// AI-GENERATED CODE: This file was written by Claude (Anthropic).
// ============================================================================

using System.Globalization;

namespace SecretList.Services;

/// <summary>
/// Back Up and Restore: copies SecretList's two files out of its package data folder
/// (which uninstalling deletes) and back in. See docs/file-format.md, "Backups".
/// </summary>
public sealed class BackupStore
{
    /// <summary>The records file's name, in the data folder and in a backup.</summary>
    public const string RecordsFileName = "records.txt";

    /// <summary>The schema file's name, in the data folder and in a backup.</summary>
    public const string SchemaFileName = "schema.md";

    private readonly SchemaFileStore _schemaStore;

    public BackupStore(SchemaFileStore schemaStore)
    {
        _schemaStore = schemaStore;
    }

    /// <summary>
    /// Creates a new folder "SecretList backup yyyy-MM-dd HHmmss" (local time; " (2)", " (3)"…
    /// added if that name is taken) inside <paramref name="destinationParent"/>, and copies
    /// records.txt and schema.md into it byte for byte. A records.txt that doesn't exist yet
    /// (no records saved) is backed up as an empty file, which means no records.
    /// Returns the new folder's path.
    /// </summary>
    public string Backup(string recordsPath, string schemaPath, string destinationParent, DateTime now)
    {
        string baseName = "SecretList backup " + now.ToString("yyyy-MM-dd HHmmss", CultureInfo.InvariantCulture);
        string folder = Path.Combine(destinationParent, baseName);
        for (int n = 2; Directory.Exists(folder) || File.Exists(folder); n++)
        {
            folder = Path.Combine(destinationParent, $"{baseName} ({n})");
        }

        Directory.CreateDirectory(folder);
        File.Copy(schemaPath, Path.Combine(folder, SchemaFileName));
        if (File.Exists(recordsPath))
            File.Copy(recordsPath, Path.Combine(folder, RecordsFileName));
        else
            File.WriteAllBytes(Path.Combine(folder, RecordsFileName), []);

        return folder;
    }

    /// <summary>
    /// Replaces records.txt and schema.md with byte-for-byte copies of the ones in
    /// <paramref name="backupFolder"/>, each written safely (<see cref="SafeFile"/>).
    /// Refuses, changing nothing, unless the folder holds both files and its schema.md is
    /// valid by the same rules as Import Schema. Returns null when restored, or the reason
    /// it refused.
    /// </summary>
    public string? TryRestore(string backupFolder, string recordsPath, string schemaPath)
    {
        string backupSchema = Path.Combine(backupFolder, SchemaFileName);
        string backupRecords = Path.Combine(backupFolder, RecordsFileName);

        if (!File.Exists(backupSchema) || !File.Exists(backupRecords))
            return $"The folder must contain both {SchemaFileName} and {RecordsFileName}.";

        if (!_schemaStore.TryRead(backupSchema, out _))
            return $"Its {SchemaFileName} isn't valid: it has no category, a category with more than 12 tags, or a tag name containing a period.";

        byte[] schemaBytes = File.ReadAllBytes(backupSchema);
        byte[] recordsBytes = File.ReadAllBytes(backupRecords);

        SafeFile.WriteAllBytes(schemaPath, schemaBytes);
        SafeFile.WriteAllBytes(recordsPath, recordsBytes);
        return null;
    }
}
