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
/// RecordFileStore for records whose category and tags are all in the schema.
/// Values outside the schema, line breaks and replacing the file are in DataLossTests.
/// </summary>
[TestClass]
public sealed class RecordFileStoreTests
{
    private static readonly string NL = Environment.NewLine;

    private static readonly List<EntityDefinition> Schema =
    [
        Category("LEGAL", "Name", "Phone", "Notes"),
        Category("FINANCIAL", "Name", "AccountNumber"),
    ];

    [TestMethod]
    public void Save_WritesEveryTagOfTheCategory_InSchemaOrder_EvenWhenEmpty()
    {
        var path = Path.Combine(NewTempFolder(), "records.txt");
        var records = new List<EntityRecord>
        {
            new() { Category = "LEGAL", NickName = "MyLawyer", Values = new() { ["Notes"] = "Handles the estate", ["Name"] = "Jane Doe, Esq." } },
            new() { Category = "FINANCIAL", NickName = "MyBank", Values = new() { ["AccountNumber"] = "AC-4471" } },
        };

        new RecordFileStore().Save(path, records, Schema);

        Assert.AreEqual(
            $":cat.LEGAL{NL}:nick.MyLawyer{NL}:tag.Name.Jane Doe, Esq.{NL}:tag.Phone.{NL}:tag.Notes.Handles the estate{NL}{NL}" +
            $":cat.FINANCIAL{NL}:nick.MyBank{NL}:tag.Name.{NL}:tag.AccountNumber.AC-4471{NL}{NL}",
            File.ReadAllText(path));
    }

    [TestMethod]
    public void SaveThenLoad_RoundTripsRecords_IncludingPeriodsInValues()
    {
        var path = Path.Combine(NewTempFolder(), "records.txt");
        var records = new List<EntityRecord>
        {
            new() { Category = "LEGAL", NickName = "MyLawyer", Values = new() { ["Name"] = "J. Doe", ["Phone"] = "631.555.0100", ["Notes"] = "See www.example.com." } },
            new() { Category = "FINANCIAL", NickName = "MyBank", Values = new() { ["Name"] = "First National", ["AccountNumber"] = "AC-4471" } },
        };

        var store = new RecordFileStore();
        store.Save(path, records, Schema);
        var loaded = store.Load(path);

        Assert.HasCount(2, loaded);
        for (int i = 0; i < records.Count; i++)
        {
            Assert.AreEqual(records[i].Category, loaded[i].Category);
            Assert.AreEqual(records[i].NickName, loaded[i].NickName);
            CollectionAssert.AreEquivalent(records[i].Values.ToList(), loaded[i].Values.ToList());
        }
    }

    [TestMethod]
    public void Load_MissingFile_ReturnsNoRecords()
    {
        var loaded = new RecordFileStore().Load(Path.Combine(NewTempFolder(), "records.txt"));

        Assert.IsEmpty(loaded);
    }

    [TestMethod]
    public void Load_ReadsTheReadmeExample()
    {
        var path = WriteFile(NewTempFolder(), "records.txt",
            ":cat.LEGAL", ":nick.MyLawyer", ":tag.Name.Jane Doe, Esq.", ":tag.Phone.631-555-0000", "",
            ":cat.FINANCIAL", ":nick.MyBank", ":tag.AccountNumber.AC-4471");

        var loaded = new RecordFileStore().Load(path);

        Assert.HasCount(2, loaded);
        Assert.AreEqual("MyLawyer", loaded[0].NickName);
        Assert.AreEqual("Jane Doe, Esq.", loaded[0].Values["Name"]);
        Assert.AreEqual("631-555-0000", loaded[0].Values["Phone"]);
        Assert.AreEqual("FINANCIAL", loaded[1].Category);
        Assert.AreEqual("AC-4471", loaded[1].Values["AccountNumber"]);
    }
}
