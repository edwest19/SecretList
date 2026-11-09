// ============================================================================
// SecretList
// Copyright (c) 2026 edwest19. Licensed under the MIT License (see LICENSE).
// AI-GENERATED CODE: This file was written by Claude (Anthropic), following
// RepositoryRulesTests in edwest19/SecretLead.
// ============================================================================

using System.Text.RegularExpressions;
using System.Xml.Linq;
using static SecretList.Core.Tests.TestHelper;

namespace SecretList.Core.Tests;

/// <summary>
/// Enforces the repository's rules: Microsoft packages only, no network API anywhere in the
/// app's code, only the runFullTrust permission, and data kept only in the MSIX package's own
/// data folder. If one of these fails, a change broke a rule; fix the change, don't loosen the
/// test without the owner's approval.
/// </summary>
[TestClass]
public sealed class RepositoryRulesTests
{
    // Every NuGet package the repo may use. All are published by Microsoft.
    private static readonly string[] AllowedPackages =
    [
        "Microsoft.WindowsAppSDK",                 // WinUI 3 runtime for SecretList.UI
        "Microsoft.Windows.SDK.BuildTools",        // Windows SDK build tools for SecretList.UI (MSIX packaging)
        "Microsoft.Windows.SDK.BuildTools.WinApp", // `dotnet run` with package identity for SecretList.UI
        "MSTest",                                  // Microsoft's test framework (tests only)
    ];

    // The app's code. The test project is left out because it names these APIs itself.
    private static readonly string[] AppProjectFolders = ["SecretList.Core", "SecretList.UI"];

    [TestMethod]
    public void OnlyAllowedMicrosoftPackagesAreReferenced()
    {
        var root = RepoRoot();
        var used = new List<string>();

        foreach (var file in BuildFiles(root))
        {
            foreach (var reference in XDocument.Load(file).Descendants().Where(e => e.Name.LocalName == "PackageReference"))
            {
                var id = (string?)reference.Attribute("Include") ?? (string?)reference.Attribute("Update") ?? "";
                used.Add($"{Path.GetRelativePath(root, file)}: {id}");
                Assert.Contains(id, AllowedPackages, $"Package '{id}' in {Path.GetRelativePath(root, file)} is not on the allowed list.");
            }
        }

        Assert.IsNotEmpty(used, "Expected to find the project's package references.");
    }

    [TestMethod]
    public void AppCodeHasNoNetworkAccess()
    {
        var network = new Regex(
            @"\bSystem\.Net\.(Http|Sockets|WebSockets|NetworkInformation|Mail|Quic|Security)\b" +
            @"|\bHttpClient\b|\bWebClient\b|\bWebRequest\b|\bSockets?\b|\bTcpClient\b|\bUdpClient\b|\bWebSocket\b" +
            @"|\bWebView2?\b|\bWindows\.Web\b|\bWindows\.Networking\b");
        var root = RepoRoot();
        var filesChecked = 0;

        foreach (var file in AppSourceFiles(root, ".cs", ".xaml"))
        {
            filesChecked++;
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                Assert.IsFalse(network.IsMatch(lines[i]),
                    $"Network API found in {Path.GetRelativePath(root, file)} line {i + 1}: {lines[i].Trim()}");
            }
        }

        Assert.IsGreaterThan(0, filesChecked, "Expected to scan the app's source files.");
    }

    [TestMethod]
    public void AppManifest_DeclaresOnlyRunFullTrust()
    {
        // No network, device or AI capabilities. runFullTrust is required for a WinUI 3 desktop app
        // packaged as full trust.
        var capabilities = XDocument.Parse(ReadRepoFile("SecretList.UI", "Package.appxmanifest")).Descendants()
            .Where(e => e.Name.LocalName is "Capability" or "DeviceCapability")
            .Select(e => (string?)e.Attribute("Name") ?? "")
            .ToArray();

        CollectionAssert.AreEqual(new[] { "runFullTrust" }, capabilities);
    }

    [TestMethod]
    public void AppCode_KeepsDataOnlyInThePackageDataFolder()
    {
        // records.txt and schema.md live in the MSIX package's own data folder,
        // ApplicationData.Current.LocalFolder. Nothing may build its own path under AppData.
        var root = RepoRoot();
        var ownAppDataPath = new Regex(@"\bLocalApplicationData\b|\bGetFolderPath\b|%LOCALAPPDATA%|\bLOCALAPPDATA\b");
        var localFolderUses = new List<string>();

        foreach (var file in AppSourceFiles(root, ".cs"))
        {
            var code = string.Join("\n", File.ReadAllLines(file).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
            Assert.IsFalse(ownAppDataPath.IsMatch(code), $"{Path.GetRelativePath(root, file)} builds a path under AppData itself.");
            localFolderUses.AddRange(Regex.Matches(code, @"ApplicationData\.Current\.LocalFolder\.Path, ""[^""]+""")
                .Select(m => $"{Path.GetFileName(file)}: {m.Value}"));
        }

        // The app opens exactly two files there: records.txt and schema.md, both in MainPage.
        CollectionAssert.AreEquivalent(
            new[]
            {
                @"MainPage.xaml.cs: ApplicationData.Current.LocalFolder.Path, ""records.txt""",
                @"MainPage.xaml.cs: ApplicationData.Current.LocalFolder.Path, ""schema.md""",
            },
            localFolderUses);
    }

    private static IEnumerable<string> AppSourceFiles(string root, params string[] extensions) =>
        AppProjectFolders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "*.*", SearchOption.AllDirectories))
            .Where(f => extensions.Any(ext => f.EndsWith(ext, StringComparison.Ordinal)))
            .Where(f => !IsBuildOutput(root, f));

    private static IEnumerable<string> BuildFiles(string root) =>
        Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".csproj", StringComparison.Ordinal) || f.EndsWith(".props", StringComparison.Ordinal) || f.EndsWith(".targets", StringComparison.Ordinal))
            .Where(f => !IsBuildOutput(root, f));

    private static bool IsBuildOutput(string root, string file)
    {
        var parts = Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => p is "bin" or "obj" or ".git" or ".vs" or "TestResults");
    }
}
