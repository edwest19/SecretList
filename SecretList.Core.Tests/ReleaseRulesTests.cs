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
/// instead of after a tag is pushed: the signer, the version in three places, the package
/// name, the signing subject, the README's release section, pinned workflow actions, x64
/// only, and the solution file. (AI-generated (Claude): release.yml checks added with it.)
/// </summary>
[TestClass]
public sealed class ReleaseRulesTests
{
    private static XElement ManifestIdentity() =>
        XDocument.Parse(ReadRepoFile("SecretList.UI", "Package.appxmanifest"))
            .Descendants().Single(e => e.Name.LocalName == "Identity");

    private static string ReleaseWorkflow() => ReadRepoFile(".github", "workflows", "release.yml");

    private static string ProjectVersion() =>
        XDocument.Parse(ReadRepoFile("Directory.Build.props")).Descendants("Version").Single().Value;

    [TestMethod]
    public void ManifestPublisher_IsTheNameReleasesAreSignedAs()
    {
        var signer = Regex.Match(ReleaseWorkflow(), @"EXPECTED_SIGNER:\s*'([^']+)'");
        Assert.IsTrue(signer.Success, "release.yml must set EXPECTED_SIGNER.");

        // Case-sensitive: Windows refuses to sign an MSIX whose Publisher differs from the
        // certificate subject (Azure Artifact Signing, account edwest19, profile CodeSigning-Apps).
        Assert.AreEqual("CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US", signer.Groups[1].Value);
        Assert.AreEqual(signer.Groups[1].Value, (string?)ManifestIdentity().Attribute("Publisher"));
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
        // Scripts/Find-SecretListData.ps1 finds the data folders by this name.
        const string packageName = "C63ABE6F-95F6-41FC-A615-D59429F044E4";

        Assert.AreEqual(packageName, (string?)ManifestIdentity().Attribute("Name"));
        Assert.Contains($"$packageName = '{packageName}'", ReadRepoFile("Scripts", "Find-SecretListData.ps1"));
    }

    [TestMethod]
    public void Changelog_HasAnEntryForTheProjectVersion()
    {
        var version = ProjectVersion();
        var lines = ReadRepoFile("CHANGELOG.md").Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var heading = new Regex(@"^## \[" + Regex.Escape(version) + @"\] - \d{4}-\d{2}-\d{2}\s*$");

        var starts = Enumerable.Range(0, lines.Length).Where(i => heading.IsMatch(lines[i])).ToArray();
        Assert.HasCount(1, starts, $"CHANGELOG.md needs exactly one heading '## [{version}] - YYYY-MM-DD'.");

        var notes = lines.Skip(starts[0] + 1).TakeWhile(l => !l.StartsWith("## [", StringComparison.Ordinal));
        Assert.IsTrue(notes.Any(l => !string.IsNullOrWhiteSpace(l)), $"The CHANGELOG.md entry for {version} is empty.");
    }

    [TestMethod]
    public void SigningSubject_IsStatedTheSameEverywhere_ForTheReleaseEnvironment()
    {
        // The Azure federated credential trusts exactly this immutable OIDC subject.
        var subject = new Regex(@"repo:[^@\s/]+@\d+/[^@\s:]+@\d+:environment:[A-Za-z0-9_-]+");
        var workflow = ReleaseWorkflow();
        var stated = new[] { ReadRepoFile("README.md"), ReadRepoFile("CLAUDE.md"), workflow }
            .Select(text => subject.Match(text).Value).ToArray();

        Assert.IsTrue(stated.All(s => s.Length > 0), "README.md, CLAUDE.md and release.yml must each state the signing subject.");
        Assert.HasCount(1, stated.Distinct(), $"The signing subject differs between files: {string.Join(" | ", stated)}");
        Assert.Contains("/SecretList@1296666450:", stated[0], "The subject must name this repository by its ID.");

        var environment = Regex.Match(workflow, @"^\s*environment:\s*(\S+)\s*$", RegexOptions.Multiline).Groups[1].Value;
        Assert.IsTrue(stated[0].EndsWith(":environment:" + environment, StringComparison.Ordinal),
            $"The subject {stated[0]} must name the sign job's environment '{environment}'.");
    }

    [TestMethod]
    public void Readme_ReleaseSection_MatchesTheWorkflow()
    {
        var workflow = ReleaseWorkflow();
        var readme = ReadRepoFile("README.md");
        string Setting(string name) => Regex.Match(workflow, name + @":\s*'?([^'\r\n]+?)'?\s*$", RegexOptions.Multiline).Groups[1].Value;

        Assert.Contains($"made by `{Setting("EXPECTED_SIGNER")}`", readme);
        Assert.Contains($"(account `{Setting("signing-account-name")}`, profile `{Setting("certificate-profile-name")}`, East US)", readme);
        Assert.Contains("https://eus.codesigning.azure.net/", workflow, "README says East US; the endpoint must be East US's.");
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

        foreach (var workflow in new[] { "build.yml", "release.yml" })
        {
            var text = ReadRepoFile(".github", "workflows", workflow);
            Assert.Contains("winapp pack SecretList.UI/SecretList.UI.csproj -c Release --arch x64", text, $"{workflow} must package x64.");
            Assert.DoesNotContain("arm64", text, $"{workflow} must not build arm64.");
        }
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
