namespace SecretList.Models;

public class EntityDefinition
{
    public string Category { get; set; } = string.Empty;
    public List<TagDefinition> Tags { get; set; } = new();
}