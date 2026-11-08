namespace SecretList.Models;

public class TagDefinition
{
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsRequired { get; set; }
}
