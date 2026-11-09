// ============================================================================
// SecretList
// Copyright (c) 2026 edwest19. Licensed under the MIT License (see LICENSE).
// AI-GENERATED CODE: This file was written by Claude (Anthropic), following
// FileFormatTests in edwest19/SecretLead.
// ============================================================================

using System.Text;
using SecretList.Models;
using SecretList.Services;
using static SecretList.Core.Tests.TestHelper;

namespace SecretList.Core.Tests;

/// <summary>
/// Known-answer tests that lock format version 1 of schema.md and records.txt, as written in
/// docs/file-format.md. Never change an expected value here to make a test pass: a format change
/// needs the owner's approval, a new version of the spec, and a plan for files already on disk.
/// </summary>
[TestClass]
public sealed class FileFormatTests
{
    // docs/file-format.md, schema.md worked example: the hand-written input.
    private const string SchemaExampleInput =
        "# My categories\n" +
        "Notes about this file are ignored.\n" +
        "\n" +
        "### Category LEGAL\n" +
        "- TAG Name\n" +
        "* TAG Phone\n" +
        "## tag Notes1\n" +
        "\n" +
        "category PETS\n" +
        "TAG Name\n" +
        "TAG Vet\n" +
        "TAG name\n";

    // docs/file-format.md, schema.md worked example: what SchemaFileStore.Save writes (with LF here).
    private const string SchemaExampleWritten =
        "# Category LEGAL\n\n## TAG Name\n\n## TAG Phone\n\n## TAG Notes1\n\n" +
        "# Category PETS\n\n## TAG Name\n\n## TAG Vet\n\n";

    // docs/file-format.md, records.txt worked example: the schema.
    private const string RecordsExampleSchema =
        "# Category LEGAL\n## TAG Name\n## TAG Phone\n## TAG Notes1\n# Category PETS\n## TAG Name\n## TAG Vet\n";

    // docs/file-format.md, records.txt worked example: the file (with LF here).
    private const string RecordsExampleFile =
        ":cat.LEGAL\n" +
        ":nick.Lawyer\n" +
        ":tag.Name.Jane Doe, Esq.\n" +
        ":tag.Phone.631-555-0100\n" +
        ":tag.Notes1.Will is in the safe. Copy at www.example.com/a.b\n" +
        "\n" +
        ":cat.PETS\n" +
        ":nick.Rex\n" +
        ":tag.Name.Rex\n" +
        ":tag.Vet.Dr. Zoë Müller\n" +
        "\n" +
        ":cat.LEGAL\n" +
        ":nick.Accountant\n" +
        ":tag.Name.\n" +
        ":tag.Phone.\n" +
        ":tag.Notes1.\n" +
        "\n";

    /// <summary>The text as the app writes it on this OS (CRLF on Windows).</summary>
    private static string AsWritten(string lfText) => lfText.Replace("\n", Environment.NewLine);

    private static SchemaFileStore Schemas() => new(() => throw new InvalidOperationException("The packaged default isn't used here."));

    private static List<EntityDefinition> ReadSchema(string text)
    {
        var path = WriteFile(NewTempFolder(), "schema.md", text);
        var schema = Schemas().Load(path, out bool isValid);
        Assert.IsTrue(isValid);
        return schema;
    }

    private static List<EntityRecord> ExampleRecords() =>
    [
        new() { Category = "LEGAL", NickName = "Lawyer", Values = new() { ["Name"] = "Jane Doe, Esq.", ["Phone"] = "631-555-0100", ["Notes1"] = "Will is in the safe. Copy at www.example.com/a.b" } },
        new() { Category = "PETS", NickName = "Rex", Values = new() { ["Name"] = "Rex", ["Vet"] = "Dr. Zoë Müller" } },
        new() { Category = "LEGAL", NickName = "Accountant" },
    ];

    private static void AssertSameRecords(List<EntityRecord> expected, List<EntityRecord> actual)
    {
        Assert.HasCount(expected.Count, actual);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.AreEqual(expected[i].Category, actual[i].Category, $"record {i + 1}");
            Assert.AreEqual(expected[i].NickName, actual[i].NickName, $"record {i + 1}");
            var expectedValues = expected[i].Values.Where(kv => kv.Value.Length > 0).OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
            var actualValues = actual[i].Values.Where(kv => kv.Value.Length > 0).OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
            CollectionAssert.AreEqual(expectedValues, actualValues, $"record {i + 1}");
        }
    }

    // ---- schema.md ----

    [TestMethod]
    public void SchemaFile_WorkedExample_Reads_WithLfOrCrlf()
    {
        foreach (var text in new[] { SchemaExampleInput, SchemaExampleInput.Replace("\n", "\r\n") })
        {
            var schema = ReadSchema(text);

            CollectionAssert.AreEqual(new[] { "LEGAL", "PETS" }, schema.Select(c => c.Category).ToArray());
            CollectionAssert.AreEqual(new[] { "Name", "Phone", "Notes1" }, TagNames(schema[0]));
            CollectionAssert.AreEqual(new[] { "Name", "Vet" }, TagNames(schema[1]));
        }
    }

    [TestMethod]
    public void SchemaFile_WorkedExample_IsWrittenExactly()
    {
        var path = Path.Combine(NewTempFolder(), "schema.md");

        Schemas().Save(path, ReadSchema(SchemaExampleInput));

        var bytes = File.ReadAllBytes(path);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(AsWritten(SchemaExampleWritten)), bytes);
        Assert.HasCount(OperatingSystem.IsWindows() ? 116 : 102, bytes);
    }

    // ---- records.txt ----

    [TestMethod]
    public void RecordsFile_WorkedExample_IsWrittenExactly_AsUtf8WithoutByteOrderMark()
    {
        var path = Path.Combine(NewTempFolder(), "records.txt");

        new RecordFileStore().Save(path, ExampleRecords(), ReadSchema(RecordsExampleSchema));

        var bytes = File.ReadAllBytes(path);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(AsWritten(RecordsExampleFile)), bytes);
        Assert.HasCount(OperatingSystem.IsWindows() ? 278 : 261, bytes);
        Assert.AreNotEqual((byte)0xEF, bytes[0], "records.txt must not start with a byte-order mark.");
    }

    [TestMethod]
    public void RecordsFile_WorkedExample_Reads_WithLfOrCrlf()
    {
        foreach (var text in new[] { RecordsExampleFile, RecordsExampleFile.Replace("\n", "\r\n") })
        {
            var path = WriteFile(NewTempFolder(), "records.txt", text);

            AssertSameRecords(ExampleRecords(), new RecordFileStore().Load(path));
        }
    }

    [TestMethod]
    public void RecordsFile_WorkedExample_Reads_AsUtf16WithByteOrderMark()
    {
        var path = Path.Combine(NewTempFolder(), "records.txt");
        File.WriteAllText(path, RecordsExampleFile, Encoding.Unicode);

        AssertSameRecords(ExampleRecords(), new RecordFileStore().Load(path));
    }

    [TestMethod]
    public void RecordsFile_ReadingRules()
    {
        var path = WriteFile(NewTempFolder(), "records.txt",
            ":nick.BeforeAnyCategory",
            ":tag.Name.BeforeAnyCategory",
            ":cat.LEGAL",
            ":nick.First",
            "a line the reader ignores",
            ":tag.NoPeriodHere",
            ":tag.Name.Old",
            ":tag.Name.New",
            "   ",
            ":cat.PETS",
            ":nick.Last",
            ":tag.Vet.Dr. Who");

        var records = new RecordFileStore().Load(path);

        Assert.HasCount(2, records);
        Assert.AreEqual("LEGAL", records[0].Category);
        Assert.AreEqual("First", records[0].NickName);
        Assert.HasCount(1, records[0].Values);
        Assert.AreEqual("New", records[0].Values["Name"], "the later value wins");
        Assert.AreEqual("PETS", records[1].Category);
        Assert.AreEqual("Last", records[1].NickName);
        Assert.AreEqual("Dr. Who", records[1].Values["Vet"], "the value keeps its periods; no empty line is needed after the last record");
    }

    // ---- the spec and these tests say the same thing ----

    [TestMethod]
    public void Spec_ShowsTheseWorkedExamplesWordForWord()
    {
        var spec = ReadRepoFile("docs", "file-format.md").Replace("\r\n", "\n");

        foreach (var example in new[] { SchemaExampleInput, SchemaExampleWritten, RecordsExampleSchema, RecordsExampleFile })
        {
            Assert.Contains("```text\n" + example + "```", spec);
        }
    }
}
