// ============================================================================
// SecretList
// Copyright (c) 2026 edwest19. Licensed under the MIT License (see LICENSE).
// AI-GENERATED CODE: This file was written by Claude (Anthropic). It holds the
// safe write that RecordFileStore.WriteReplacing did, so Restore can use it too.
// ============================================================================

using System.Text;

namespace SecretList.Services;

/// <summary>Replaces a file so that an interrupted write leaves the old file as it was.</summary>
public static class SafeFile
{
    /// <summary>
    /// Writes <paramref name="bytes"/> to "&lt;file&gt;.tmp" beside the file, flushes them to disk,
    /// then swaps it in for the old file (<see cref="File.Replace(string, string, string?)"/>),
    /// or renames it when there is no old file yet. A leftover ".tmp" from an earlier
    /// interrupted write is overwritten.
    /// </summary>
    public static void WriteAllBytes(string filePath, byte[] bytes)
    {
        string tempPath = filePath + ".tmp";

        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(filePath))
            File.Replace(tempPath, filePath, destinationBackupFileName: null);
        else
            File.Move(tempPath, filePath);
    }

    /// <summary>The same, for text written as UTF-8 without a byte-order mark.</summary>
    public static void WriteAllText(string filePath, string text) =>
        WriteAllBytes(filePath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text));
}
