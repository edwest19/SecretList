using SecretList.Models;
using System.Text;

namespace SecretList.Services;

public class RecordFileStore
{
    public void Save(string filePath, List<EntityRecord> records, List<EntityDefinition> schema)
    {
        var sb = new StringBuilder();

        foreach (var record in records)
        {
            sb.AppendLine($":cat.{record.Category}");
            sb.AppendLine($":nick.{record.NickName}");

            var entityDef = schema.FirstOrDefault(e => e.Category == record.Category);
            if (entityDef != null)
            {
                foreach (var tag in entityDef.Tags.OrderBy(t => t.Order))
                {
                    string value = record.Values.TryGetValue(tag.Name, out var v) ? v : string.Empty;
                    sb.AppendLine($":tag.{tag.Name}.{value}");
                }
            }

            sb.AppendLine();
        }

        File.WriteAllText(filePath, sb.ToString());
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
