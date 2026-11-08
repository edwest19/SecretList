// ============================================================================
// SecretList
// Copyright (c) 2026 edwest19. Licensed under the MIT License (see LICENSE).
// AI-GENERATED CODE: This file was written by Claude (Anthropic). The method
// body was moved unchanged from SchemaFileStore.GetDefaultSchemaPath when
// SchemaFileStore moved to SecretList.Core.
// ============================================================================

using Windows.ApplicationModel;

namespace SecretList.Services;

// Finds the packaged, read-only Assets/DefaultSchema.md. Lives in the app,
// not SecretList.Core, because Package.Current is a Windows API.
internal static class DefaultSchemaLocation
{
    // Resolves the packaged, read-only DefaultSchema.md. Packaged apps
    // can't write to their own install folder, so this file is naturally
    // immutable at runtime - no ACL tricks needed. Falls back to the
    // build output folder when running unpackaged (Package.Current
    // throws InvalidOperationException without a package identity).
    public static string GetPath()
    {
        string baseDir;
        try
        {
            baseDir = Package.Current.InstalledLocation.Path;
        }
        catch (InvalidOperationException)
        {
            baseDir = AppContext.BaseDirectory;
        }

        return Path.Combine(baseDir, "Assets", "DefaultSchema.md");
    }
}
