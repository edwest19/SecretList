// ============================================================================
// SecretList
// Copyright (c) 2026 edwest19. Licensed under the MIT License (see LICENSE).
// AI-GENERATED CODE: This file was written by Claude (Anthropic).
// ============================================================================

using SecretList.Services;
using static SecretList.Core.Tests.TestHelper;

namespace SecretList.Core.Tests;

/// <summary>
/// SchemaFileStore as it behaves today: reading schema.md, seeding it from the
/// packaged default, the 12-tag limit, Import Schema and Export Schema.
/// </summary>
[TestClass]
public sealed class SchemaFileStoreTests
{
    private static readonly string NL = Environment.NewLine;

    [TestMethod]
    public void Load_MissingFile_SeedsItFromTheDefaultAndReportsValid()
    {
        var folder = NewTempFolder();
        var defaultPath = WriteFile(folder, "DefaultSchema.md", "# Default", "", "## Category A", "", "- TAG X", "- TAG Y");
        var schemaPath = Path.Combine(folder, "schema.md");

        var schema = new SchemaFileStore(() => defaultPath).Load(schemaPath, out bool isValid);

        Assert.IsTrue(isValid);
        Assert.HasCount(1, schema);
        Assert.AreEqual("A", schema[0].Category);
        CollectionAssert.AreEqual(new[] { "X", "Y" }, TagNames(schema[0]));
        Assert.AreEqual($"# Category A{NL}{NL}## TAG X{NL}{NL}## TAG Y{NL}{NL}", File.ReadAllText(schemaPath));
    }

    [TestMethod]
    public void Load_AcceptsAnyHeadingLevelAndBulletTags_AndIgnoresOtherLines()
    {
        var folder = NewTempFolder();
        var schemaPath = WriteFile(folder, "schema.md",
            "# My schema",
            "Some notes about this file.",
            "### Category LEGAL",
            "- TAG Name",
            "* TAG Phone",
            "## TAG Email",
            "# category home",
            "#### tag Address");

        var schema = new SchemaFileStore(() => throw new InvalidOperationException("default not needed")).Load(schemaPath, out bool isValid);

        Assert.IsTrue(isValid);
        Assert.HasCount(2, schema);
        Assert.AreEqual("LEGAL", schema[0].Category);
        CollectionAssert.AreEqual(new[] { "Name", "Phone", "Email" }, TagNames(schema[0]));
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, schema[0].Tags.Select(t => t.Order).ToArray());
        Assert.AreEqual("home", schema[1].Category);
        CollectionAssert.AreEqual(new[] { "Address" }, TagNames(schema[1]));
    }

    [TestMethod]
    public void Load_DuplicateTagInACategory_IsSkipped()
    {
        var folder = NewTempFolder();
        var schemaPath = WriteFile(folder, "schema.md", "# Category A", "## TAG Name", "## TAG name", "## TAG Phone");

        var schema = new SchemaFileStore(() => throw new InvalidOperationException("default not needed")).Load(schemaPath, out _);

        CollectionAssert.AreEqual(new[] { "Name", "Phone" }, TagNames(schema[0]));
    }

    [TestMethod]
    public void Load_TwelveTagsInACategory_IsAccepted()
    {
        var folder = NewTempFolder();
        var lines = new List<string> { "# Category A" };
        lines.AddRange(Enumerable.Range(1, 12).Select(i => $"## TAG T{i}"));
        var schemaPath = WriteFile(folder, "schema.md", [.. lines]);

        var schema = new SchemaFileStore(() => throw new InvalidOperationException("default not needed")).Load(schemaPath, out bool isValid);

        Assert.IsTrue(isValid);
        Assert.HasCount(12, schema[0].Tags);
    }

    [TestMethod]
    public void Load_ThirteenTagsInACategory_FallsBackToTheDefault_AndLeavesTheFileAlone()
    {
        var folder = NewTempFolder();
        var defaultPath = WriteFile(folder, "DefaultSchema.md", "## Category DEFAULT", "- TAG Name");
        var lines = new List<string> { "# Category OK", "## TAG Name", "# Category A" };
        lines.AddRange(Enumerable.Range(1, 13).Select(i => $"## TAG T{i}"));
        var schemaPath = WriteFile(folder, "schema.md", [.. lines]);
        var before = File.ReadAllBytes(schemaPath);

        var schema = new SchemaFileStore(() => defaultPath).Load(schemaPath, out bool isValid);

        Assert.IsFalse(isValid);
        Assert.HasCount(1, schema);
        Assert.AreEqual("DEFAULT", schema[0].Category);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(schemaPath));
    }

    [TestMethod]
    public void Load_FileWithNoCategories_FallsBackToTheDefault_AndLeavesTheFileAlone()
    {
        var folder = NewTempFolder();
        var defaultPath = WriteFile(folder, "DefaultSchema.md", "## Category DEFAULT", "- TAG Name");
        var schemaPath = WriteFile(folder, "schema.md", "just some text", "## TAG Orphan");
        var before = File.ReadAllBytes(schemaPath);

        var schema = new SchemaFileStore(() => defaultPath).Load(schemaPath, out bool isValid);

        Assert.IsFalse(isValid);
        Assert.AreEqual("DEFAULT", schema[0].Category);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(schemaPath));
    }

    [TestMethod]
    public void TryImport_InvalidFile_ReturnsFalse_AndLeavesTheDestinationAlone()
    {
        var folder = NewTempFolder();
        var defaultPath = WriteFile(folder, "DefaultSchema.md", "## Category DEFAULT", "- TAG Name");
        var destPath = WriteFile(folder, "schema.md", "# Category CURRENT", "## TAG Name");
        var before = File.ReadAllBytes(destPath);
        var sourcePath = WriteFile(folder, "import.md", "no categories here");

        bool ok = new SchemaFileStore(() => defaultPath).TryImport(sourcePath, destPath, out _);

        Assert.IsFalse(ok);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(destPath));
    }

    [TestMethod]
    public void TryImport_ValidFile_CopiesItByteForByte_AndReturnsItsCategories()
    {
        var folder = NewTempFolder();
        var destPath = WriteFile(folder, "schema.md", "# Category CURRENT", "## TAG Name");
        var sourcePath = WriteFile(folder, "import.md", "# My import", "", "## Category NEW", "- TAG Name", "- TAG Phone");

        bool ok = new SchemaFileStore(() => throw new InvalidOperationException("default not needed")).TryImport(sourcePath, destPath, out var schema);

        Assert.IsTrue(ok);
        CollectionAssert.AreEqual(File.ReadAllBytes(sourcePath), File.ReadAllBytes(destPath));
        Assert.AreEqual("NEW", schema[0].Category);
        CollectionAssert.AreEqual(new[] { "Name", "Phone" }, TagNames(schema[0]));
    }

    [TestMethod]
    public void ExportDefaultSchema_CopiesThePackagedDefaultByteForByte()
    {
        var folder = NewTempFolder();
        var destPath = Path.Combine(folder, "exported.md");

        new SchemaFileStore(ShippedDefaultSchemaPath).ExportDefaultSchema(destPath);

        CollectionAssert.AreEqual(File.ReadAllBytes(ShippedDefaultSchemaPath()), File.ReadAllBytes(destPath));
    }

    [TestMethod]
    public void ShippedDefaultSchema_HasExactlyTheCategoriesAndTagsTheReadmeLists()
    {
        var expected = new (string Category, string[] Tags)[]
        {
            ("LEGAL", ["Name", "Organization", "URL", "AddressLine1", "AddressLine2", "AddressLine3", "Phone", "Email", "Notes1", "Notes2", "Notes3"]),
            ("FINANCIAL", ["Name", "AccountNumber", "RoutingNumber", "Advisor", "URL", "AddressLine1", "AddressLine2", "AddressLine3", "Phone", "Email", "Notes1", "Notes2"]),
            ("MEDICAL", ["Name", "AccountNumber", "Organization", "URL", "AddressLine1", "AddressLine2", "AddressLine3", "Phone", "Email", "Notes1", "Notes2", "Notes3"]),
            ("FAMILY", ["Name", "Relationship", "AddressLine1", "AddressLine2", "AddressLine3", "Phone", "Email", "Notes1", "Notes2", "Notes3"]),
            ("DIGITAL", ["Name", "UserName", "URL", "Organization", "Email", "Notes1", "Notes2", "Notes3"]),
            ("HOME", ["Name", "AccountNumber", "Organization", "URL", "AddressLine1", "AddressLine2", "AddressLine3", "Phone", "Email", "Notes1", "Notes2", "Notes3"]),
            ("PETS", ["Name", "Breed", "Vet", "Organization", "AddressLine1", "AddressLine2", "AddressLine3", "Phone", "Email", "Notes1", "Notes2", "Notes3"]),
        };
        var readme = ReadRepoFile("README.md");

        var schema = new SchemaFileStore(ShippedDefaultSchemaPath).Load(Path.Combine(NewTempFolder(), "schema.md"), out _);

        Assert.HasCount(expected.Length, schema);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.AreEqual(expected[i].Category, schema[i].Category);
            CollectionAssert.AreEqual(expected[i].Tags, TagNames(schema[i]), expected[i].Category);
            Assert.Contains($"| `{expected[i].Category}` | {expected[i].Tags.Length} | {string.Join(", ", expected[i].Tags)} |", readme);
        }
    }

    [TestMethod]
    public void ShippedDefaultSchema_HasSevenCategories_EachWithinTheTwelveTagLimit_AndNoPasswordField()
    {
        var folder = NewTempFolder();

        var schema = new SchemaFileStore(ShippedDefaultSchemaPath).Load(Path.Combine(folder, "schema.md"), out bool isValid);

        Assert.IsTrue(isValid);
        CollectionAssert.AreEqual(
            new[] { "LEGAL", "FINANCIAL", "MEDICAL", "FAMILY", "DIGITAL", "HOME", "PETS" },
            schema.Select(c => c.Category).ToArray());
        foreach (var category in schema)
        {
            Assert.IsLessThanOrEqualTo(12, category.Tags.Count, category.Category);
            Assert.IsFalse(category.Tags.Any(t => t.Name.Contains("password", StringComparison.OrdinalIgnoreCase)), category.Category);
        }
    }
}
