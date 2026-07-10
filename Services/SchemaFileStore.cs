// SecretList
// Copyright (c) 2026 edwest19
//
// AI Disclaimer: This code was generated with the assistance of
// Claude (Anthropic AI), under the direction and review of edwest19.

using SecretList.Models;
using System.Text;
using System.Text.RegularExpressions;
using Windows.ApplicationModel;

namespace SecretList.Services;

// Loads the schema (categories + their tags) from a plain-text markdown
// file, schema.md, sitting next to records.txt. This makes the schema
// hand-editable in any text editor - no rebuild needed - the same way
// records.txt already is. Also supports importing a schema file from
// anywhere on disk (see TryImport), for the Import Schema menu item.
//
// Parsing is deliberately lenient about markdown style, since hand-written
// files (or ones written elsewhere, e.g. by an AI assistant) reasonably
// vary in heading level and list style. Recognized forms:
//   # Category NAME       any heading level (#, ##, ###...) works
//   ## Category NAME
//   ## TAG TagName         a tag as a heading, any level
//   - TAG TagName           ...or as a bullet-list item ('-' or '*')
//   * TAG TagName
// A line that doesn't match either pattern (e.g. a plain "# My Schema"
// document title) is simply ignored, same as a blank line - it doesn't
// need to be a category or tag to coexist in the file.
//
// If schema.md doesn't exist yet, the packaged DefaultSchema.md is parsed
// and written out as a starting point (in the app's own simple heading
// style - see Save). If it exists but fails to parse into at least one
// category (e.g. a bad hand-edit), the same default is used in-memory
// without touching the file, so a typo can't leave the app with an empty
// schema or crash it.
public class SchemaFileStore
{
    // Hard cap: a category can have at most this many tags. Keeps every
    // category's fields on-screen without scrolling, and keeps a printed
    // record to a single A5 half-page. A schema.md (or imported file) that
    // violates this is treated exactly like an unparseable one - rejected
    // outright, with a fall back to the immutable packaged default. No
    // partial acceptance: either the whole category list is valid, or none
    // of it is used.
    private const int MaxTagsPerCategory = 12;

    // ^ optional leading #'s (1+) OR a single -/* bullet marker, then
    // whitespace, then the literal keyword, then whitespace, then the name.
    private static readonly Regex CategoryLineRegex =
        new(@"^(?:#{1,6}\s*|[-*]\s+)?Category\s+(.+)$", RegexOptions.IgnoreCase);

    private static readonly Regex TagLineRegex =
        new(@"^(?:#{1,6}\s*|[-*]\s+)?TAG\s+(.+)$", RegexOptions.IgnoreCase);

    // isValid is true whenever the returned schema actually came from
    // reading/parsing schema.md successfully (including the very first
    // run, where it's freshly seeded and then immediately valid). It's
    // false only when schema.md exists but failed to parse into anything -
    // in that case the file is left untouched and the in-memory default
    // is used instead. MainPage uses this to color the "..." menu button.
    public List<EntityDefinition> Load(string filePath, out bool isValid)
    {
        if (!File.Exists(filePath))
        {
            var seeded = LoadDefaultSchema();
            Save(filePath, seeded);
            isValid = true; // just wrote a good file - it's in place now
            return seeded;
        }

        var parsed = ParseLines(File.ReadAllLines(filePath));

        if (parsed.Count > 0)
        {
            isValid = true;
            return parsed;
        }

        isValid = false;
        return LoadDefaultSchema();
    }

    // Validates a candidate schema file picked from anywhere on disk
    // (via the Import Schema menu item) without touching the app's real
    // schema.md unless the candidate actually parses successfully.
    // Returns false (and leaves destFilePath alone) if the file couldn't
    // be read or didn't contain any valid categories.
    public bool TryImport(string sourceFilePath, string destFilePath, out List<EntityDefinition> schema)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(sourceFilePath);
        }
        catch
        {
            schema = LoadDefaultSchema();
            return false;
        }

        var parsed = ParseLines(lines);
        if (parsed.Count == 0)
        {
            schema = LoadDefaultSchema();
            return false;
        }

        File.Copy(sourceFilePath, destFilePath, overwrite: true);
        schema = parsed;
        return true;
    }

    // Copies the packaged, read-only Assets/DefaultSchema.md to a location
    // the user chooses (Desktop, Documents, a flash drive), for the Export
    // Schema menu item. Copied byte-for-byte, preserving whatever comments
    // or formatting the packaged file was written with - not round-tripped
    // through ParseLines/Save, so nothing is normalized or lost.
    public void ExportDefaultSchema(string destFilePath)
    {
        File.Copy(GetDefaultSchemaPath(), destFilePath, overwrite: true);
    }

    // Resolves the packaged, read-only DefaultSchema.md. Packaged apps
    // can't write to their own install folder, so this file is naturally
    // immutable at runtime - no ACL tricks needed. Falls back to the
    // build output folder when running unpackaged (Package.Current
    // throws InvalidOperationException without a package identity).
    private static string GetDefaultSchemaPath()
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

    // Reads and parses the packaged DefaultSchema.md through the same
    // lenient parser used for schema.md, so there's exactly one parser
    // and one format to maintain. The empty-list return is a last-resort
    // safety net (a corrupted deployment) and should never happen in
    // practice, since DefaultSchema.md ships inside the package itself.
    private List<EntityDefinition> LoadDefaultSchema()
    {
        try
        {
            var lines = File.ReadAllLines(GetDefaultSchemaPath());
            var parsed = ParseLines(lines);
            if (parsed.Count > 0)
                return parsed;
        }
        catch
        {
            // Fall through to the empty-list safety net below.
        }

        return new List<EntityDefinition>();
    }

    private List<EntityDefinition> ParseLines(string[] lines)
    {
        var schema = new List<EntityDefinition>();
        EntityDefinition? current = null;
        int tagOrder = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var categoryMatch = CategoryLineRegex.Match(line);
            if (categoryMatch.Success)
            {
                string typeName = categoryMatch.Groups[1].Value.Trim();
                if (typeName.Length == 0)
                    continue;

                current = new EntityDefinition { Category = typeName, Tags = new List<TagDefinition>() };
                schema.Add(current);
                tagOrder = 0;
                continue;
            }

            var tagMatch = TagLineRegex.Match(line);
            if (tagMatch.Success && current != null)
            {
                string tagName = tagMatch.Groups[1].Value.Trim();
                if (tagName.Length == 0)
                    continue;

                // Skip an exact-duplicate tag name within the same category
                // rather than silently creating two fields with the same label.
                if (current.Tags.Any(t => t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                tagOrder++;
                current.Tags.Add(new TagDefinition { Name = tagName, Order = tagOrder });
            }
        }

        // Reject the whole file if any category exceeds the tag cap, rather
        // than silently truncating to the first 12. Same treatment as a
        // file with zero parseable categories: the caller sees an empty
        // list, which triggers the existing "invalid schema" fallback in
        // Load() and TryImport() - the default schema is used, and (in
        // TryImport's case) nothing is written to disk.
        if (schema.Any(e => e.Tags.Count > MaxTagsPerCategory))
            return new List<EntityDefinition>();

        return schema;
    }

    // Writes in the app's own simple style: one heading per category, one
    // heading per tag. TryImport never calls this - an imported file is
    // copied byte-for-byte, preserving whatever style the person wrote it
    // in - this is only used to seed schema.md the very first time.
    public void Save(string filePath, List<EntityDefinition> schema)
    {
        var sb = new StringBuilder();

        foreach (var entityDef in schema)
        {
            sb.AppendLine($"# Category {entityDef.Category}");
            sb.AppendLine();

            foreach (var tag in entityDef.Tags.OrderBy(t => t.Order))
            {
                sb.AppendLine($"## TAG {tag.Name}");
                sb.AppendLine();
            }
        }

        File.WriteAllText(filePath, sb.ToString());
    }
}
