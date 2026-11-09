// ============================================================================
// SecretList
// Copyright (c) 2026 edwest19. Licensed under the MIT License (see LICENSE).
// AI-GENERATED CODE: This file was written by Claude (Anthropic).
// ============================================================================

using SecretList.Models;
using SecretList.Services;
using static SecretList.Core.Tests.TestHelper;

namespace SecretList.Core.Tests;

/// <summary>
/// The data-loss bugs found in the 2026-11-08 evaluation, each reproduced first:
/// values outside the current schema erased on save, a tag name with a period losing its
/// value, line breaks breaking records.txt, and records or fields left off the printout.
/// </summary>
[TestClass]
public sealed class DataLossTests
{
    private static readonly string NL = Environment.NewLine;

    private static SchemaFileStore Schemas(string defaultPath) => new(() => defaultPath);

    // ---- values outside the current schema ----

    [TestMethod]
    public void Save_KeepsValuesOfTagsTheSchemaDoesNotList()
    {
        var path = Path.Combine(NewTempFolder(), "records.txt");
        var store = new RecordFileStore();
        store.Save(path,
            [new() { Category = "LEGAL", NickName = "Lawyer", Values = new() { ["Name"] = "Jane", ["Phone"] = "555-0100", ["Executor"] = "Bob" } }],
            [Category("LEGAL", "Name", "Phone", "Executor")]);

        // The schema loses "Executor" (a bad hand-edit falls back to a default, or an Import);
        // the record is saved again.
        store.Save(path, store.Load(path), [Category("LEGAL", "Name", "Phone")]);

        Assert.AreEqual("Bob", store.Load(path)[0].Values.GetValueOrDefault("Executor"));
    }

    [TestMethod]
    public void Save_KeepsEveryValueOfARecordWhoseCategoryIsNotInTheSchema()
    {
        var path = Path.Combine(NewTempFolder(), "records.txt");
        var store = new RecordFileStore();
        store.Save(path,
            [new() { Category = "BOATS", NickName = "Boat", Values = new() { ["Name"] = "Sea Ray", ["Marina"] = "Huntington" } }],
            [Category("BOATS", "Name", "Marina")]);

        store.Save(path, store.Load(path), [Category("LEGAL", "Name", "Phone")]);

        var boat = store.Load(path)[0];
        Assert.AreEqual("Sea Ray", boat.Values.GetValueOrDefault("Name"));
        Assert.AreEqual("Huntington", boat.Values.GetValueOrDefault("Marina"));
    }

    [TestMethod]
    public void Save_WritesValuesOutsideTheSchema_AfterTheSchemaTags_InNameOrder_LeavingOutEmptyOnes()
    {
        var path = Path.Combine(NewTempFolder(), "records.txt");
        var record = new EntityRecord
        {
            Category = "LEGAL",
            NickName = "Lawyer",
            Values = new() { ["Zip"] = "11743", ["Name"] = "Jane", ["Fax"] = "", ["Executor"] = "Bob" },
        };

        new RecordFileStore().Save(path, [record], [Category("LEGAL", "Name", "Phone")]);

        Assert.AreEqual(
            $":cat.LEGAL{NL}:nick.Lawyer{NL}:tag.Name.Jane{NL}:tag.Phone.{NL}:tag.Executor.Bob{NL}:tag.Zip.11743{NL}{NL}",
            File.ReadAllText(path));
    }

    // ---- a tag name with a period ----

    [TestMethod]
    public void Load_SchemaWithATagNameContainingAPeriod_IsRefused_AndLeftAlone()
    {
        var folder = NewTempFolder();
        var defaultPath = WriteFile(folder, "DefaultSchema.md", "# Category DEFAULT", "## TAG Name");
        var schemaPath = WriteFile(folder, "schema.md", "# Category LEGAL", "## TAG Name", "## TAG Lic. No");
        var before = File.ReadAllBytes(schemaPath);

        var schema = Schemas(defaultPath).Load(schemaPath, out bool isValid);

        Assert.IsFalse(isValid);
        Assert.AreEqual("DEFAULT", schema[0].Category);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(schemaPath));
    }

    [TestMethod]
    public void TryImport_SchemaWithATagNameContainingAPeriod_IsRefused_AndLeavesSchemaMdAlone()
    {
        var folder = NewTempFolder();
        var defaultPath = WriteFile(folder, "DefaultSchema.md", "# Category DEFAULT", "## TAG Name");
        var destPath = WriteFile(folder, "schema.md", "# Category CURRENT", "## TAG Name");
        var before = File.ReadAllBytes(destPath);
        var sourcePath = WriteFile(folder, "import.md", "# Category LEGAL", "## TAG Name", "## TAG Lic. No");

        bool ok = Schemas(defaultPath).TryImport(sourcePath, destPath, out _);

        Assert.IsFalse(ok);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(destPath));
    }

    // ---- line breaks ----

    [TestMethod]
    public void Save_TurnsLineBreaksInValuesAndNicknamesIntoSpaces_SoTheRestOfTheRecordSurvives()
    {
        var path = Path.Combine(NewTempFolder(), "records.txt");
        var store = new RecordFileStore();
        var record = new EntityRecord
        {
            Category = "LEGAL",
            NickName = "My\r\nLawyer",
            Values = new() { ["Name"] = "Line one\r\n\r\nLine two\rThree\nFour", ["Phone"] = "555-0100" },
        };

        store.Save(path, [record], [Category("LEGAL", "Name", "Phone")]);
        var loaded = store.Load(path);

        Assert.HasCount(1, loaded);
        Assert.AreEqual("My Lawyer", loaded[0].NickName);
        Assert.AreEqual("Line one  Line two Three Four", loaded[0].Values["Name"]);
        Assert.AreEqual("555-0100", loaded[0].Values["Phone"]);
    }

    // ---- saving over the old file ----

    [TestMethod]
    public void Save_ReplacesAnExistingFile_AndLeavesNoTemporaryFile()
    {
        var folder = NewTempFolder();
        var path = WriteFile(folder, "records.txt", ":cat.OLD", ":nick.Old", "");

        new RecordFileStore().Save(path, [new() { Category = "LEGAL", NickName = "New" }], [Category("LEGAL", "Name")]);

        Assert.AreEqual($":cat.LEGAL{NL}:nick.New{NL}:tag.Name.{NL}{NL}", File.ReadAllText(path));
        CollectionAssert.AreEqual(new[] { "records.txt" }, Directory.GetFiles(folder).Select(Path.GetFileName).ToArray());
    }

    // ---- printing ----

    [TestMethod]
    public void Print_ListsSchemaCategoriesInSchemaOrder_RecordsInStoredOrder_AndOnlyFieldsWithAValue()
    {
        List<EntityDefinition> schema = [Category("LEGAL", "Name", "Phone"), Category("PETS", "Name", "Vet")];
        List<EntityRecord> records =
        [
            new() { Category = "PETS", NickName = "Rex", Values = new() { ["Name"] = "Rex", ["Vet"] = " " } },
            new() { Category = "LEGAL", NickName = "Lawyer", Values = new() { ["Phone"] = "555-0100", ["Name"] = "Jane" } },
            new() { Category = "LEGAL", NickName = "Accountant" },
        ];

        var printed = PrintContent.Build(schema, records);

        CollectionAssert.AreEqual(new[] { "Lawyer", "Accountant", "Rex" }, printed.Select(p => p.NickName).ToArray());
        CollectionAssert.AreEqual(new[] { "LEGAL", "LEGAL", "PETS" }, printed.Select(p => p.Category).ToArray());
        CollectionAssert.AreEqual(new[] { new PrintedField("Name", "Jane"), new PrintedField("Phone", "555-0100") }, printed[0].Fields.ToArray());
        Assert.IsEmpty(printed[1].Fields);
        CollectionAssert.AreEqual(new[] { new PrintedField("Name", "Rex") }, printed[2].Fields.ToArray());
    }

    [TestMethod]
    public void Print_IncludesValuesOfTagsTheSchemaDoesNotList_AfterTheSchemaFields_InNameOrder()
    {
        List<EntityRecord> records =
        [
            new() { Category = "LEGAL", NickName = "Lawyer", Values = new() { ["Zip"] = "11743", ["Name"] = "Jane", ["Executor"] = "Bob", ["Fax"] = "" } },
        ];

        var printed = PrintContent.Build([Category("LEGAL", "Name", "Phone")], records);

        CollectionAssert.AreEqual(
            new[] { new PrintedField("Name", "Jane"), new PrintedField("Executor", "Bob"), new PrintedField("Zip", "11743") },
            printed[0].Fields.ToArray());
    }

    [TestMethod]
    public void Print_IncludesRecordsWhoseCategoryIsNotInTheSchema_AfterTheOthers()
    {
        List<EntityRecord> records =
        [
            new() { Category = "BOATS", NickName = "Boat", Values = new() { ["Name"] = "Sea Ray", ["Marina"] = "Huntington" } },
            new() { Category = "LEGAL", NickName = "Lawyer", Values = new() { ["Name"] = "Jane" } },
            new() { Category = "CARS", NickName = "Car", Values = new() { ["Make"] = "Ford" } },
            new() { Category = "BOATS", NickName = "Dinghy" },
        ];

        var printed = PrintContent.Build([Category("LEGAL", "Name")], records);

        CollectionAssert.AreEqual(new[] { "Lawyer", "Boat", "Dinghy", "Car" }, printed.Select(p => p.NickName).ToArray());
        CollectionAssert.AreEqual(new[] { "LEGAL", "BOATS", "BOATS", "CARS" }, printed.Select(p => p.Category).ToArray());
        CollectionAssert.AreEqual(new[] { new PrintedField("Marina", "Huntington"), new PrintedField("Name", "Sea Ray") }, printed[1].Fields.ToArray());
    }

    [TestMethod]
    public void Print_ListsEachRecordOnce_WhenTheSchemaRepeatsACategoryName()
    {
        List<EntityRecord> records = [new() { Category = "LEGAL", NickName = "Lawyer", Values = new() { ["Name"] = "Jane" } }];

        var printed = PrintContent.Build([Category("LEGAL", "Name"), Category("LEGAL", "Phone")], records);

        Assert.HasCount(1, printed);
    }
}
