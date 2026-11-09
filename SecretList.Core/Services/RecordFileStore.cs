using SecretList.Models;
using System.Text;
using System.Text.RegularExpressions; // AI-generated (Claude): for OneLine

namespace SecretList.Services;

public class RecordFileStore
{
    // AI-generated (Claude): the line breaks File.ReadAllLines splits on (CRLF, CR, LF).
    private static readonly Regex LineBreak = new(@"\r\n|\r|\n");

    public void Save(string filePath, List<EntityRecord> records, List<EntityDefinition> schema)
    {
        var sb = new StringBuilder();

        foreach (var record in records)
        {
            sb.AppendLine($":cat.{record.Category}");
            sb.AppendLine($":nick.{OneLine(record.NickName)}"); // AI-generated (Claude): line breaks become spaces

            var schemaTagNames = new List<string>(); // AI-generated (Claude)
            var entityDef = schema.FirstOrDefault(e => e.Category == record.Category);
            if (entityDef != null)
            {
                foreach (var tag in entityDef.Tags.OrderBy(t => t.Order))
                {
                    string value = record.Values.TryGetValue(tag.Name, out var v) ? v : string.Empty;
                    sb.AppendLine($":tag.{tag.Name}.{OneLine(value)}"); // AI-generated (Claude): line breaks become spaces
                    schemaTagNames.Add(tag.Name); // AI-generated (Claude)
                }
            }

            // AI-generated (Claude): values the current schema doesn't list (a tag removed from
            // the category, or every tag of a category the schema doesn't have) are kept, after
            // the schema's tags, instead of being erased by this save.
            foreach (var extra in RecordValues.OutsideSchema(record, schemaTagNames))
            {
                sb.AppendLine($":tag.{extra.Key}.{OneLine(extra.Value)}");
            }

            sb.AppendLine();
        }

        WriteReplacing(filePath, sb.ToString()); // AI-generated (Claude): was File.WriteAllText(filePath, ...)
    }

    // AI-generated (Claude): records.txt has one line per tag, so a line break inside a
    // value would cut the record short when read. Each one becomes a single space.
    private static string OneLine(string value) => LineBreak.Replace(value, " ");

    // AI-generated (Claude): writes the new contents to "<file>.tmp" beside the file,
    // flushes them to disk, then swaps it in for the old file (File.Replace), or renames
    // it when there is no old file yet. A save interrupted before the swap leaves the old
    // file as it was. UTF-8 without a byte-order mark, as File.WriteAllText wrote before.
    private static void WriteReplacing(string filePath, string text)
    {
        string tempPath = filePath + ".tmp";
        byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text);

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
    public List<EntityRecord> Load(string filePath)
{
    var records = new List<EntityRecord>();

    if (!File.Exists(filePath))
        return records;

    var lines = File.ReadAllLines(filePath);
    EntityRecord? current = null;

    foreach (var line in lines)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            if (current != null)
            {
                records.Add(current);
                current = null;
            }
            continue;
        }

        if (line.StartsWith(":cat."))
        {
           current = new EntityRecord
            {
               Category = line.Substring(":cat.".Length)
            };
        }
        else if (line.StartsWith(":nick.") && current != null)
        {
            current.NickName = line.Substring(":nick.".Length);
        }
        else if (line.StartsWith(":tag.") && current != null)
        {
            var rest = line.Substring(":tag.".Length);   // e.g. "Phone.631-555-0000"
            int dotIndex = rest.IndexOf('.');
            if (dotIndex >= 0)
            {
                string tagName = rest.Substring(0, dotIndex);
                string value = rest.Substring(dotIndex + 1);
                current.Values[tagName] = value;
            }
        }
    }

    if (current != null)
        records.Add(current);

    return records;
}
}
