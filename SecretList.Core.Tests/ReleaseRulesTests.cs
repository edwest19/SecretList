// ============================================================================
// SecretList
// Copyright (c) 2026 edwest19. Licensed under the MIT License (see LICENSE).
// AI-GENERATED CODE: This file was written by Claude (Anthropic), following
// ReleaseRulesTests in edwest19/SecretLead.
// ============================================================================

using System.Text.RegularExpressions;
using System.Xml.Linq;
using static SecretList.Core.Tests.TestHelper;

namespace SecretList.Core.Tests;

/// <summary>
/// Keeps the release settings consistent, so a broken release is found in a pull request
/// instead of after a tag is pushed: the MSIX publisher, the version, the package name,
/// pinned workflow actions, x64 only, and the solution file. The checks that need the
/// release workflow (signer, signing subject, dated changelog entry) are added with it.
/// </summary>
[TestClass]
public sealed class ReleaseRulesTests
{
    private static XElement ManifestIdentity() =>
        XDocument.Parse(ReadRepoFile("SecretList.UI", "Package.appxmanifest"))
            .Descendants().Single(e => e.Name.LocalName == "Identity");

    private static string ProjectVersion() =>
        XDocument.Parse(ReadRepoFile("Directory.Build.props")).Descendants("Version").Single().Value;

    [TestMethod]
    public void ManifestPublisher_IsTheNameReleasesAreSignedAs()
    {
        // Case-sensitive: Windows refuses to sign an MSIX whose Publisher differs from the
        // certificate subject (Azure Artifact Signing, account edwest19, profile CodeSigning-Apps).
        Assert.AreEqual("CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US", (string?)ManifestIdentity().Attribute("Publisher"));
    }

    [TestMethod]
    public void ManifestVersion_IsTheProjectVersionPlusZero()
    {
        var version = ProjectVersion();

        Assert.IsTrue(Regex.IsMatch(version, @"^\d+\.\d+\.\d+$"), $"Directory.Build.props Version '{version}' must be X.Y.Z.");
        Assert.AreEqual(version + ".0", (string?)ManifestIdentity().Attribute("Version"));
    }

    [TestMethod]
    public void ManifestPackageName_StaysTheSame()
    {
        // Windows keeps an MSIX app's data under its package name and publisher. A new name
        // would make an update a different app, with a new, empty data folder.
        Assert.AreEqual("C63ABE6F-95F6-41FC-A615-D59429F044E4", (string?)ManifestIdentity().Attribute("Name"));
    }

    [TestMethod]
    public void WorkflowActions_ArePinnedToExactCommits()
    {
        var uses = new Regex(@"^\s*(?:-\s*)?uses:\s*(\S+)");
        var pinned = new Regex(@"^[A-Za-z0-9_.-]+/[A-Za-z0-9_./-]+@[0-9a-f]{40}$");
        var checkedCount = 0;

        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot(), ".github", "workflows"), "*.yml"))
        {
            foreach (var line in File.ReadLines(file))
            {
                var match = uses.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                checkedCount++;
                Assert.IsTrue(pinned.IsMatch(match.Groups[1].Value),
                    $"{Path.GetFileName(file)}: '{match.Groups[1].Value}' must be pinned to a full commit SHA.");
            }
        }

        Assert.IsGreaterThan(0, checkedCount, "Expected to find actions in the workflows.");
    }

    [TestMethod]
    public void App_IsBuiltAndPackagedForX64Only()
    {
        var project = XDocument.Parse(ReadRepoFile("SecretList.UI", "SecretList.UI.csproj"));
        Assert.AreEqual("x64", project.Descendants("Platforms").Single().Value);

        var build = ReadRepoFile(".github", "workflows", "build.yml");
        Assert.Contains("winapp pack SecretList.UI/SecretList.UI.csproj -c Release --arch x64", build, "build.yml must package x64.");
        Assert.DoesNotContain("arm64", build, "build.yml must not build arm64.");
    }

    [TestMethod]
    public void SolutionFile_ListsOnlyFilesThatExist()
    {
        var solution = XDocument.Parse(ReadRepoFile("SecretList.slnx"));
        var paths = solution.Descendants()
            .Where(e => e.Name.LocalName is "File" or "Project")
            .Select(e => (string?)e.Attribute("Path") ?? "")
            .ToArray();

        Assert.IsNotEmpty(paths);
        foreach (var path in paths)
        {
            Assert.IsTrue(File.Exists(Path.Combine(RepoRoot(), path)), $"SecretList.slnx lists '{path}', which doesn't exist.");
        }
    }
}
