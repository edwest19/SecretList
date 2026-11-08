// ============================================================================
// SecretList
// Copyright (c) 2026 edwest19. Licensed under the MIT License (see LICENSE).
// AI-GENERATED CODE: This file was written by Claude (Anthropic), following
// TestHelper in edwest19/SecretLead.
// ============================================================================

using SecretList.Models;

namespace SecretList.Core.Tests;

/// <summary>Shared helpers.</summary>
internal static class TestHelper
{
    /// <summary>Finds the repository root (the folder containing SecretList.slnx) from the test's output folder.</summary>
    public static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SecretList.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Could not find SecretList.slnx above " + AppContext.BaseDirectory);
    }

    /// <summary>The full path of a file in the repository, given its path parts from the root.</summary>
    public static string RepoPath(params string[] path) => Path.Combine([RepoRoot(), .. path]);

    /// <summary>The default schema shipped in the app package.</summary>
    public static string ShippedDefaultSchemaPath() => RepoPath("SecretList.UI", "Assets", "DefaultSchema.md");

    /// <summary>A new, empty temporary folder for one test.</summary>
    public static string NewTempFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), "SecretList.Tests." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Writes lines joined with "\n" to a file in the folder and returns its path.</summary>
    public static string WriteFile(string folder, string name, params string[] lines)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, string.Join("\n", lines));
        return path;
    }

    /// <summary>A category with tags numbered 1, 2, 3... in the order given.</summary>
    public static EntityDefinition Category(string name, params string[] tags) => new()
    {
        Category = name,
        Tags = tags.Select((tag, i) => new TagDefinition { Name = tag, Order = i + 1 }).ToList(),
    };

    /// <summary>A category's tag names, in order.</summary>
    public static string[] TagNames(EntityDefinition category) =>
        category.Tags.OrderBy(t => t.Order).Select(t => t.Name).ToArray();
}
