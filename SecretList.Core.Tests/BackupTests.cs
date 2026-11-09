// ============================================================================
// SecretList
// Copyright (c) 2026 edwest19. Licensed under the MIT License (see LICENSE).
// AI-GENERATED CODE: This file was written by Claude (Anthropic).
// ============================================================================

using SecretList.Services;
using static SecretList.Core.Tests.TestHelper;

namespace SecretList.Core.Tests;

/// <summary>Back Up and Restore (BackupStore), as README "Backup and Restore" and docs/file-format.md "Backups" describe.</summary>
[TestClass]
public sealed class BackupTests
{
    private static readonly DateTime When = new(2026, 11, 8, 23, 5, 9);

    private static BackupStore Store() => new(new SchemaFileStore(() => throw new InvalidOperationException("The packaged default isn't used here.")));

    /// <summary>A data folder with a schema.md and, unless null, a records.txt.</summary>
    private static (string Records, string Schema) DataFolder(string schemaText, string? recordsText)
    {
        var folder = NewTempFolder();
        var schema = WriteFile(folder, "schema.md", schemaText);
        var records = Path.Combine(folder, "records.txt");
        if (recordsText != null)
            File.WriteAllText(records, recordsText);
        return (records, schema);
    }

    [TestMethod]
    public void Backup_CopiesBothFilesByteForByte_IntoANewDatedFolder()
    {
        var (records, schema) = DataFolder("# Category LEGAL\r\n## TAG Name\r\n", ":cat.LEGAL\r\n:nick.Zoë\r\n:tag.Name.Jane\r\n\r\n");
        var destination = NewTempFolder();

        var backup = Store().Backup(records, schema, destination, When);

        Assert.AreEqual(Path.Combine(destination, "SecretList backup 2026-11-08 230509"), backup);
        CollectionAssert.AreEqual(File.ReadAllBytes(schema), File.ReadAllBytes(Path.Combine(backup, "schema.md")));
        CollectionAssert.AreEqual(File.ReadAllBytes(records), File.ReadAllBytes(Path.Combine(backup, "records.txt")));
        CollectionAssert.AreEquivalent(new[] { "records.txt", "schema.md" }, Directory.GetFiles(backup).Select(Path.GetFileName).ToArray());
    }

    [TestMethod]
    public void Backup_TwiceInTheSameSecond_MakesASecondFolder_AndKeepsTheFirst()
    {
        var (records, schema) = DataFolder("# Category LEGAL\n## TAG Name\n", ":cat.LEGAL\n:nick.A\n");
        var destination = NewTempFolder();

        var first = Store().Backup(records, schema, destination, When);
        var second = Store().Backup(records, schema, destination, When);

        Assert.AreEqual(Path.Combine(destination, "SecretList backup 2026-11-08 230509 (2)"), second);
        Assert.IsTrue(File.Exists(Path.Combine(first, "records.txt")));
        Assert.IsTrue(File.Exists(Path.Combine(second, "records.txt")));
    }

    [TestMethod]
    public void Backup_BeforeAnyRecordIsSaved_WritesAnEmptyRecordsFile()
    {
        var (records, schema) = DataFolder("# Category LEGAL\n## TAG Name\n", recordsText: null);

        var backup = Store().Backup(records, schema, NewTempFolder(), When);

        Assert.HasCount(0, File.ReadAllBytes(Path.Combine(backup, "records.txt")));
    }

    [TestMethod]
    public void Restore_ReplacesBothFilesByteForByte_AndLeavesNoTemporaryFile()
    {
        var (records, schema) = DataFolder("# Category OLD\n## TAG Name\n", ":cat.OLD\n:nick.Old\n");
        var backup = NewTempFolder();
        WriteFile(backup, "schema.md", "# Category LEGAL", "## TAG Name", "## TAG Phone");
        WriteFile(backup, "records.txt", ":cat.LEGAL", ":nick.Lawyer", ":tag.Name.Jane", "");

        var problem = Store().TryRestore(backup, records, schema);

        Assert.IsNull(problem);
        CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(backup, "schema.md")), File.ReadAllBytes(schema));
        CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(backup, "records.txt")), File.ReadAllBytes(records));
        CollectionAssert.AreEquivalent(new[] { "records.txt", "schema.md" }, Directory.GetFiles(Path.GetDirectoryName(records)!).Select(Path.GetFileName).ToArray());
    }

    [TestMethod]
    public void Restore_WhenTheAppHasNoRecordsFileYet_CreatesIt()
    {
        var (records, schema) = DataFolder("# Category OLD\n## TAG Name\n", recordsText: null);
        var backup = NewTempFolder();
        WriteFile(backup, "schema.md", "# Category LEGAL", "## TAG Name");
        WriteFile(backup, "records.txt", ":cat.LEGAL", ":nick.Lawyer", "");

        Assert.IsNull(Store().TryRestore(backup, records, schema));
        Assert.AreEqual("Lawyer", new RecordFileStore().Load(records)[0].NickName);
    }

    [TestMethod]
    public void Restore_FolderMissingAFile_IsRefused_AndChangesNothing()
    {
        var (records, schema) = DataFolder("# Category OLD\n## TAG Name\n", ":cat.OLD\n:nick.Old\n");
        var before = (File.ReadAllBytes(records), File.ReadAllBytes(schema));
        var backup = NewTempFolder();
        WriteFile(backup, "schema.md", "# Category LEGAL", "## TAG Name");

        var problem = Store().TryRestore(backup, records, schema);

        Assert.AreEqual("The folder must contain both schema.md and records.txt.", problem);
        CollectionAssert.AreEqual(before.Item1, File.ReadAllBytes(records));
        CollectionAssert.AreEqual(before.Item2, File.ReadAllBytes(schema));
    }

    [TestMethod]
    public void Restore_InvalidSchema_IsRefused_AndChangesNothing()
    {
        var (records, schema) = DataFolder("# Category OLD\n## TAG Name\n", ":cat.OLD\n:nick.Old\n");
        var before = (File.ReadAllBytes(records), File.ReadAllBytes(schema));
        var backup = NewTempFolder();
        WriteFile(backup, "schema.md", "# Category LEGAL", "## TAG Lic. No");
        WriteFile(backup, "records.txt", ":cat.LEGAL", ":nick.Lawyer", "");

        var problem = Store().TryRestore(backup, records, schema);

        Assert.Contains("isn't valid", problem ?? "(restored)");
        CollectionAssert.AreEqual(before.Item1, File.ReadAllBytes(records));
        CollectionAssert.AreEqual(before.Item2, File.ReadAllBytes(schema));
    }

    [TestMethod]
    public void BackupThenRestore_BringsBackTheSameFiles()
    {
        var (records, schema) = DataFolder("# Category LEGAL\r\n## TAG Name\r\n", ":cat.LEGAL\r\n:nick.Lawyer\r\n:tag.Name.Jane\r\n\r\n");
        var original = (File.ReadAllBytes(records), File.ReadAllBytes(schema));
        var backup = Store().Backup(records, schema, NewTempFolder(), When);
        File.WriteAllText(records, ":cat.LEGAL\r\n:nick.Changed\r\n\r\n");
        File.WriteAllText(schema, "# Category OTHER\r\n## TAG X\r\n");

        Assert.IsNull(Store().TryRestore(backup, records, schema));

        CollectionAssert.AreEqual(original.Item1, File.ReadAllBytes(records));
        CollectionAssert.AreEqual(original.Item2, File.ReadAllBytes(schema));
    }
}
